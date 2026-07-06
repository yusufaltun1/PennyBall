using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Advertisements;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Unity Ads: rewarded (Claim x2) + skippable interstitial (her 3 maçta bir).
/// </summary>
public class AdsService : MonoBehaviour,
    IUnityAdsInitializationListener,
    IUnityAdsLoadListener,
    IUnityAdsShowListener
{
    enum PendingShowType
    {
        None,
        Rewarded,
        Interstitial
    }

    public static AdsService Instance { get; private set; }

    public bool IsInitialized { get; private set; }
    public bool IsRewardedReady { get; private set; }
    public bool IsInterstitialReady { get; private set; }

    Action _onRewarded;
    Action _onFailed;
    Action _onInterstitialClosed;
    PendingShowType _pendingShow;
    bool _showInProgress;
    static bool _mainMenuNavigationPending;
    GameObject _editorMockRoot;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureService()
    {
        if (Instance != null)
        {
            return;
        }

        var go = new GameObject("AdsService");
        Instance = go.AddComponent<AdsService>();
        DontDestroyOnLoad(go);
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _mainMenuNavigationPending = false;
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
        Initialize();
    }

    void OnDestroy()
    {
        DestroyEditorMock();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Initialize()
    {
        if (!AdsConfig.HasValidKeys)
        {
            Debug.LogWarning(
                "[Ads] Game ID eksik. AdsConfig.cs içine Unity Ads Project settings Game ID yaz.");
            return;
        }

        if (Advertisement.isInitialized)
        {
            IsInitialized = true;
            LoadAllAds();
            return;
        }

        Debug.Log($"[Ads] Unity Ads init... gameId={AdsConfig.GameId}, testMode={AdsConfig.TestMode}");
        Advertisement.Initialize(AdsConfig.GameId, AdsConfig.TestMode, this);
    }

    public void OnInitializationComplete()
    {
        IsInitialized = true;
        Debug.Log("[Ads] Unity Ads init OK.");
        LoadAllAds();
    }

    public void OnInitializationFailed(UnityAdsInitializationError error, string message)
    {
        IsInitialized = false;
        Debug.LogError($"[Ads] Init failed: {error} - {message}");
    }

    void LoadAllAds()
    {
        LoadRewardedAd();
        LoadInterstitialAd();
    }

    void LoadRewardedAd()
    {
        IsRewardedReady = false;
        Advertisement.Load(AdsConfig.RewardedAdUnitId, this);
    }

    void LoadInterstitialAd()
    {
        IsInterstitialReady = false;
        Advertisement.Load(AdsConfig.InterstitialAdUnitId, this);
    }

    public void OnUnityAdsAdLoaded(string placementId)
    {
        if (placementId == AdsConfig.RewardedAdUnitId)
        {
            IsRewardedReady = true;
            Debug.Log("[Ads] Rewarded loaded.");
        }
        else if (placementId == AdsConfig.InterstitialAdUnitId)
        {
            IsInterstitialReady = true;
            Debug.Log("[Ads] Interstitial loaded.");
        }
    }

    public void OnUnityAdsFailedToLoad(string placementId, UnityAdsLoadError error, string message)
    {
        if (placementId == AdsConfig.RewardedAdUnitId)
        {
            IsRewardedReady = false;
            Debug.LogWarning($"[Ads] Rewarded load failed: {error} - {message}");
        }
        else if (placementId == AdsConfig.InterstitialAdUnitId)
        {
            IsInterstitialReady = false;
            Debug.LogWarning($"[Ads] Interstitial load failed: {error} - {message}");
        }
    }

    /// <summary>
    /// Rewarded video. Tam izlenince onRewarded.
    /// </summary>
    public void ShowRewarded(Action onRewarded, Action onFailed = null)
    {
        if (_showInProgress)
        {
            onFailed?.Invoke();
            return;
        }

#if UNITY_EDITOR
        if (AdsConfig.UseEditorMockAd)
        {
            StartCoroutine(PlayEditorMockAd("Rewarded (Editor Mock)", 3f, onRewarded, onFailed));
            return;
        }
#endif

        if (!IsInitialized || !IsRewardedReady)
        {
            Debug.LogWarning("[Ads] Rewarded hazır değil.");
            if (IsInitialized)
            {
                LoadRewardedAd();
            }

            onFailed?.Invoke();
            return;
        }

        _onRewarded = onRewarded;
        _onFailed = onFailed;
        _pendingShow = PendingShowType.Rewarded;
        _showInProgress = true;
        IsRewardedReady = false;

        Debug.Log($"[Ads] Show rewarded: {AdsConfig.RewardedAdUnitId}");
        Advertisement.Show(AdsConfig.RewardedAdUnitId, this);
    }

    /// <summary>
    /// Skippable interstitial. Kapanınca / skip / fail fark etmeksizin onClosed çağrılır.
    /// </summary>
    public void ShowInterstitial(Action onClosed = null)
    {
        if (_showInProgress)
        {
            onClosed?.Invoke();
            return;
        }

#if UNITY_EDITOR
        if (AdsConfig.UseEditorMockAd)
        {
            StartCoroutine(PlayEditorMockAd("Interstitial (Editor Mock)\nSkipable", 2f, onClosed, onClosed));
            return;
        }
#endif

        if (!IsInitialized || !IsInterstitialReady)
        {
            Debug.LogWarning("[Ads] Interstitial hazır değil, devam ediliyor.");
            if (IsInitialized)
            {
                LoadInterstitialAd();
            }

            onClosed?.Invoke();
            return;
        }

        _onInterstitialClosed = onClosed;
        _pendingShow = PendingShowType.Interstitial;
        _showInProgress = true;
        IsInterstitialReady = false;

        Debug.Log($"[Ads] Show interstitial: {AdsConfig.InterstitialAdUnitId}");
        Advertisement.Show(AdsConfig.InterstitialAdUnitId, this);
    }

    /// <summary>
    /// Her 3 maçta bir interstitial gösterir, sonra ana menüye döner.
    /// Reklam yoksa / fail olursa direkt menüye gider.
    /// </summary>
    public static void GoToMainMenuMaybeWithInterstitial()
    {
        if (_mainMenuNavigationPending)
        {
            return;
        }

        _mainMenuNavigationPending = true;

        if (Instance != null && MatchAdTracker.ShouldShowInterstitial())
        {
            Debug.Log($"[Ads] Interstitial gösterilecek (match #{MatchAdTracker.CompletedMatchCount}).");
            Instance.StartCoroutine(Instance.NavigateToMainMenuWithInterstitial());
            return;
        }

        SceneManager.LoadScene(GameSceneNames.MainMenu);
    }

    IEnumerator NavigateToMainMenuWithInterstitial()
    {
        yield return WaitUntilCanShowAd();
        yield return WaitForInterstitialReady(4f);
        ShowInterstitial(() => SceneManager.LoadScene(GameSceneNames.MainMenu));
    }

    IEnumerator WaitUntilCanShowAd()
    {
        const float maxWait = 12f;
        float elapsed = 0f;

        while (_showInProgress && elapsed < maxWait)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    IEnumerator WaitForInterstitialReady(float maxWaitSeconds)
    {
#if UNITY_EDITOR
        if (AdsConfig.UseEditorMockAd)
        {
            yield break;
        }
#endif

        if (IsInterstitialReady)
        {
            yield break;
        }

        float elapsed = 0f;
        while (!IsInterstitialReady && elapsed < maxWaitSeconds)
        {
            if (!IsInitialized)
            {
                Debug.LogWarning("[Ads] Interstitial beklenemedi: Unity Ads henüz init olmadı.");
                yield break;
            }

            LoadInterstitialAd();
            yield return new WaitForSecondsRealtime(0.5f);
            elapsed += 0.5f;
        }

        if (!IsInterstitialReady)
        {
            Debug.LogWarning("[Ads] Interstitial süre içinde yüklenemedi, menüye devam ediliyor.");
        }
    }

    public void OnUnityAdsShowStart(string placementId)
    {
        Debug.Log($"[Ads] Show start: {placementId}");
        TrackAdEvent("ad_show_start", placementId);
    }

    public void OnUnityAdsShowClick(string placementId)
    {
    }

    public void OnUnityAdsShowComplete(string placementId, UnityAdsShowCompletionState showCompletionState)
    {
        Debug.Log($"[Ads] Show complete: {placementId}, state={showCompletionState}");

        PendingShowType type = _pendingShow;
        Action rewarded = _onRewarded;
        Action failed = _onFailed;
        Action interstitialClosed = _onInterstitialClosed;

        ClearPending();

        if (type == PendingShowType.Rewarded)
        {
            if (showCompletionState == UnityAdsShowCompletionState.COMPLETED)
            {
                TrackAdEvent("rewarded_ad_completed", placementId);
                rewarded?.Invoke();
            }
            else
            {
                TrackAdEvent("rewarded_ad_skipped", placementId);
                failed?.Invoke();
            }

            LoadRewardedAd();
            return;
        }

        if (type == PendingShowType.Interstitial)
        {
            TrackAdEvent("interstitial_ad_shown", placementId);
            // Skip veya tamamla — ikisi de menüye devam
            interstitialClosed?.Invoke();
            LoadInterstitialAd();
        }
    }

    public void OnUnityAdsShowFailure(string placementId, UnityAdsShowError error, string message)
    {
        Debug.LogWarning($"[Ads] Show failed: {error} - {message}");

        PendingShowType type = _pendingShow;
        Action failed = _onFailed;
        Action interstitialClosed = _onInterstitialClosed;

        ClearPending();

        if (type == PendingShowType.Rewarded)
        {
            failed?.Invoke();
            LoadRewardedAd();
            return;
        }

        if (type == PendingShowType.Interstitial)
        {
            interstitialClosed?.Invoke();
            LoadInterstitialAd();
        }
    }

    void ClearPending()
    {
        _pendingShow = PendingShowType.None;
        _onRewarded = null;
        _onFailed = null;
        _onInterstitialClosed = null;
        _showInProgress = false;
    }

    static void TrackAdEvent(string eventName, string placementId)
    {
        MetaAppEventsService.TrackEvent(eventName, new Dictionary<string, string>
        {
            { "placement", placementId ?? "unknown" },
            { "network", "unity_ads" }
        });
    }

    IEnumerator PlayEditorMockAd(string title, float duration, Action onSuccess, Action onFail)
    {
        _showInProgress = true;
        Debug.Log($"[Ads] Editor mock: {title}");

        TextMeshProUGUI label = CreateEditorMockUi(title);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            int left = Mathf.CeilToInt(duration - elapsed);
            if (label != null)
            {
                label.SetText($"{title}\n{Mathf.Max(0, left)}");
            }

            yield return null;
        }

        DestroyEditorMock();
        _showInProgress = false;
        onSuccess?.Invoke();
    }

    TextMeshProUGUI CreateEditorMockUi(string title)
    {
        DestroyEditorMock();

        _editorMockRoot = new GameObject("EditorMockAd");
        DontDestroyOnLoad(_editorMockRoot);

        Canvas canvas = _editorMockRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        _editorMockRoot.AddComponent<CanvasScaler>();
        _editorMockRoot.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(_editorMockRoot.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.94f);

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        RectTransform textRect = textGo.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0.1f, 0.35f);
        textRect.anchorMax = new Vector2(0.9f, 0.65f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 42f;
        label.color = Color.white;
        label.SetText(title);
        return label;
    }

    void DestroyEditorMock()
    {
        if (_editorMockRoot != null)
        {
            Destroy(_editorMockRoot);
            _editorMockRoot = null;
        }
    }
}
