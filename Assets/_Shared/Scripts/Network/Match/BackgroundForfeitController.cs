using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Online maçta bir oyuncu uygulamayı arka plana alırsa (ekranı kapatır, başka uygulamaya
/// geçer vb.) KARŞI tarafın ekranında 10 saniyelik görünür bir geri sayım başlatır. Süre
/// dolmadan geri dönülmezse arka plana alan taraf hükmen mağlup, bekleyen taraf galip sayılır.
///
/// HOST/MİSAFİR AYRIMI YOK — host-only MatchEnd akışına (OnlineMatchCoordinator.
/// OnLocalMatchCompleted SADECE host ise RPC_MatchEnd gönderir) bağımlı olmamak için her iki
/// taraf da KENDİ sonucunu (Win/Loss) yerel olarak zorunlu uygular; RPC_BackgroundForfeit
/// sadece karşı tarafa "ben seni hükmen mağlup ilan ettim, sen de Loss uygula" demek için
/// kullanılır — var olan "Çık" (ForfeitActiveMatch) + SendForfeit akışının simetriği, ama bu
/// oturumda güvenilir olduğu doğrulanmış Fusion [Rpc] kanalı (MatchShotNetworkRelay) üzerinden
/// (custom reliable-data kanalının gerçek 2-cihaz testinde %0 teslimat verdiği biliniyor).
/// </summary>
public class BackgroundForfeitController : MonoBehaviour
{
    public static BackgroundForfeitController Instance { get; private set; }

    const float ForfeitCountdownSeconds = 10f;

    MatchShotNetworkRelay _boundRelay;
    bool _localBackgrounded;
    Coroutine _countdownRoutine;

    GameObject _pausedPanel;
    TextMeshProUGUI _counterText;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Ensure()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("BackgroundForfeitController");
        Instance = go.AddComponent<BackgroundForfeitController>();
        DontDestroyOnLoad(go);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        OnlineMatchSession.SessionStarted += ResetState;
        OnlineMatchSession.SessionCleared += ResetState;
    }

    void OnDisable()
    {
        OnlineMatchSession.SessionStarted -= ResetState;
        OnlineMatchSession.SessionCleared -= ResetState;
    }

    void ResetState()
    {
        _localBackgrounded = false;
        StopCountdown();
    }

    void Update()
    {
        if (!OnlineMatchSession.IsOnlineMatch)
        {
            return;
        }

        EnsureRelayBound();
    }

    /// <summary>MatchShotNetworkRelay.Instance her maçta YENİ bir nesne — ReferenceEquals ile
    /// gerçek referansı karşılaştırıp maç değiştiğinde yeniden abone oluyoruz (bu oturumda
    /// birkaç kez karşılaşılan bir bug sınıfı — bkz. HostPhysicsAuthority._boundRelay).</summary>
    void EnsureRelayBound()
    {
        if (ReferenceEquals(_boundRelay, MatchShotNetworkRelay.Instance))
        {
            return;
        }

        if (_boundRelay != null)
        {
            _boundRelay.OpponentBackgroundStateReceived -= OnOpponentBackgroundStateReceived;
            _boundRelay.BackgroundForfeitReceived -= OnBackgroundForfeitReceived;
        }

        _boundRelay = MatchShotNetworkRelay.Instance;

        if (_boundRelay != null)
        {
            _boundRelay.OpponentBackgroundStateReceived += OnOpponentBackgroundStateReceived;
            _boundRelay.BackgroundForfeitReceived += OnBackgroundForfeitReceived;
        }
    }

    void OnApplicationPause(bool paused)
    {
        SetLocalBackgroundState(paused);
    }

    void OnApplicationFocus(bool hasFocus)
    {
        SetLocalBackgroundState(!hasFocus);
    }

    void SetLocalBackgroundState(bool backgrounded)
    {
        if (_localBackgrounded == backgrounded)
        {
            return;
        }

        _localBackgrounded = backgrounded;

        if (!CanParticipate())
        {
            return;
        }

        MatchShotNetworkRelay.Instance?.TrySendBackgroundState(backgrounded);
        Debug.Log($"[BackgroundForfeit] Kendi durumum bildirildi: backgrounded={backgrounded}");
    }

    static bool CanParticipate()
    {
        return OnlineMatchSession.IsOnlineMatch
            && LeagueMatchController.Instance != null
            && LeagueMatchController.Instance.IsMatchActive;
    }

    // ===================== Bekleyen taraf: rakip bildirimini al, geri sayımı yönet =====================

    void OnOpponentBackgroundStateReceived(bool backgrounded)
    {
        if (backgrounded)
        {
            StartCountdown();
        }
        else
        {
            Debug.Log("[BackgroundForfeit] Rakip geri döndü — geri sayım iptal.");
            StopCountdown();
        }
    }

    void StartCountdown()
    {
        if (_countdownRoutine != null)
        {
            return;
        }

        Debug.Log("[BackgroundForfeit] Rakip arka plana alındı — 10sn geri sayım başlıyor.");
        _countdownRoutine = StartCoroutine(CountdownRoutine());
    }

    void StopCountdown()
    {
        if (_countdownRoutine != null)
        {
            StopCoroutine(_countdownRoutine);
            _countdownRoutine = null;
        }

        HideOverlay();
    }

    IEnumerator CountdownRoutine()
    {
        ShowOverlay();

        float remaining = ForfeitCountdownSeconds;
        UpdateCountdownText(remaining);

        while (remaining > 0f)
        {
            if (!CanParticipate())
            {
                // Maç başka bir yoldan (gol/zaman/manuel çıkış) zaten bitti — hükmen mağlup ilan etmeye gerek yok.
                _countdownRoutine = null;
                HideOverlay();
                yield break;
            }

            yield return null;
            remaining -= Time.unscaledDeltaTime;
            UpdateCountdownText(Mathf.Max(0f, remaining));
        }

        HideOverlay();
        _countdownRoutine = null;

        if (!CanParticipate())
        {
            yield break;
        }

        Debug.Log("[BackgroundForfeit] Rakip 10sn arka planda kaldı — hükmen mağlup ilan ediliyor.");
        MatchShotNetworkRelay.Instance?.TrySendBackgroundForfeit();
        LeagueMatchController.Instance?.EndMatchByOpponentDisconnect("background_timeout");
    }

    // ===================== Arka plana alan taraf: karşı tarafın ilanını al, Loss uygula =====================

    void OnBackgroundForfeitReceived()
    {
        if (!CanParticipate())
        {
            return;
        }

        Debug.Log("[BackgroundForfeit] Karşı taraf bizi hükmen mağlup ilan etti (arka planda kalma).");
        LeagueMatchController.Instance?.ForfeitActiveMatch("background_timeout");
    }

    // ===================== UI (GameUI sahnesinde hazır "GamePaused" paneli) =====================

    /// <summary>
    /// GameUI.unity'de zaten var olan ama hiçbir script tarafından kullanılmayan "GamePaused"
    /// paneli (Container/Title "Game paused!", Desc "Please wait...", Sayac/Placeholder_Txt
    /// sayı metni) — bu özellik için ayrı bir UI icat etmek yerine onu buluyoruz. Diğer
    /// panellerle (ExitMatchPanelController/HukmenMaglupController) aynı FindDeepChild +
    /// lazy-resolve deseni.
    /// </summary>
    void EnsurePanelResolved()
    {
        if (_pausedPanel != null)
        {
            return;
        }

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, "GamePaused");
            if (found == null)
            {
                continue;
            }

            _pausedPanel = found.gameObject;

            Transform counterTransform = FindDeepChild(found, "Placeholder_Txt");
            _counterText = counterTransform != null ? counterTransform.GetComponent<TextMeshProUGUI>() : null;
            break;
        }

        if (_pausedPanel == null)
        {
            Debug.LogWarning("[BackgroundForfeit] 'GamePaused' paneli sahnede bulunamadı.");
        }
    }

    void ShowOverlay()
    {
        EnsurePanelResolved();

        if (_pausedPanel == null)
        {
            return;
        }

        _pausedPanel.transform.SetAsLastSibling();
        _pausedPanel.SetActive(true);
    }

    void HideOverlay()
    {
        if (_pausedPanel != null)
        {
            _pausedPanel.SetActive(false);
        }
    }

    void UpdateCountdownText(float remainingSeconds)
    {
        if (_counterText == null)
        {
            return;
        }

        int whole = Mathf.CeilToInt(remainingSeconds);
        _counterText.text = whole.ToString();
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
