using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Online maçta rakip gözden kaybolursa (uygulamayı arka plana alır, bağlantısı kopar vb.)
/// KARŞI tarafın ekranında 10 saniyelik görünür bir geri sayım başlatır. Süre dolmadan geri
/// dönülmezse kaybolan taraf hükmen mağlup, bekleyen taraf galip sayılır.
///
/// İKİ AYRI TETİKLEYİCİ, TEK ORTAK SAYAÇ/UI:
/// 1) OpponentBackgroundStateReceived (RPC_PlayerBackground) — arka plana alan tarafın KENDİ
///    OnApplicationPause/Focus'undan gönderdiği açık bir sinyal. HIZLI ama GÜVENİLMEZ: gerçek
///    cihazda uygulama arka plana alınınca işletim sistemi networking'i neredeyse ANINDA
///    askıya alabiliyor, bu da RPC'nin hiç gönderilememesine yol açabiliyor (Unity Editör'de
///    pencere odağı değişimi ise süreci asla askıya almadığı için orada her zaman çalışıyordu
///    — "Editör'de çalışıyor ama gerçek cihazda geri sayım hiç başlamıyor" bug'ının sebebi
///    tam olarak buydu).
/// 2) OnlineMatchSession.Channel.OpponentLeft (RemoteOpponentController üzerinden) — Photon'un
///    KENDİ taşıma-katmanı (transport-level) bağlantı-kopması tespiti. Bu, karşı tarafın kendi
///    kodunun hiç çalışma şansı bulamadığı durumlarda (OS tarafından askıya alınma, gerçek
///    bağlantı kaybı) bile sunucu/relay tarafından fark edilip ANINDA bize bildiriliyor — bu
///    yüzden gerçek cihaz arka-plan senaryosunu güvenilir şekilde yakalayan asıl mekanizma bu.
///    Önceden RemoteOpponentController bunu kendi başına, UI'sız, 6sn'lik ayrı bir zamanlayıcıyla
///    işliyordu — iki ayrı paralel sayaç (6sn sessiz + 10sn görünür) birbirini yarışıp tutarsız
///    sonuçlara yol açabileceği için tek bir otoriteye (burası) birleştirildi.
///
/// Geri sayım HANGİ tetikleyiciyle başladıysa, iptal koşulu da ona göre değişir (bkz.
/// CountdownTrigger): 1) için karşı tarafın "geri döndüm" RPC'si, 2) için Photon'un
/// RemotePlayerCount'unun tekrar &gt;=1 olması (her ikisini karıştırmak yanlış-pozitif iptale
/// yol açabilir — örn. sadece arka plana alınmış ama Photon'un hâlâ "bağlı" saydığı bir oyuncu
/// için RemotePlayerCount hiç değişmeyebilir).
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

    enum CountdownTrigger
    {
        ExplicitBackground,
        TransportDisconnect,
    }

    MatchShotNetworkRelay _boundRelay;
    bool _localBackgrounded;
    Coroutine _countdownRoutine;
    CountdownTrigger _countdownTrigger;

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
            StartCountdown(CountdownTrigger.ExplicitBackground);
        }
        else if (_countdownTrigger == CountdownTrigger.ExplicitBackground)
        {
            Debug.Log("[BackgroundForfeit] Rakip geri döndü (açık sinyal) — geri sayım iptal.");
            StopCountdown();
        }
    }

    /// <summary>RemoteOpponentController, Photon'un transport-level OpponentLeft'ini alınca
    /// çağırır — gerçek cihazda arka plana alınan bir uygulamanın kendi RPC'sini hiç
    /// gönderemediği durumları bile yakalayan asıl güvenilir yol (bkz. sınıf yorumu).</summary>
    public void NotifyOpponentConnectionLost(string reason)
    {
        StartCountdown(CountdownTrigger.TransportDisconnect);
    }

    void StartCountdown(CountdownTrigger trigger)
    {
        if (_countdownRoutine != null)
        {
            return;
        }

        _countdownTrigger = trigger;
        Debug.Log($"[BackgroundForfeit] Rakip kayboldu ({trigger}) — 10sn geri sayım başlıyor.");
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

            // SADECE TransportDisconnect tetikleyicisi için RemotePlayerCount'u "geri döndü"
            // sinyali olarak kabul ediyoruz. ExplicitBackground tetikleyicisinde bu kontrolü
            // yapmak YANLIŞ olurdu: Photon, sadece arka plana alınmış (bağlantısı kopmamış) bir
            // oyuncuyu hâlâ "bağlı" sayabiliyor — RemotePlayerCount hiç değişmeden 1'de kalabilir,
            // bu da geri sayımı başladığı anda yanlışlıkla iptal ederdi.
            if (_countdownTrigger == CountdownTrigger.TransportDisconnect
                && OnlineMatchSession.Channel != null
                && OnlineMatchSession.Channel.RemotePlayerCount >= 1)
            {
                Debug.Log("[BackgroundForfeit] Rakip bağlantı seviyesinde geri döndü — geri sayım iptal.");
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

        Debug.Log("[BackgroundForfeit] Rakip 10sn kayıp kaldı — hükmen mağlup ilan ediliyor.");
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
