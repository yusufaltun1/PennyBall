using UnityEngine;

/// <summary>
/// Unity Ads Monetization kimlikleri.
/// Dashboard: Monetization → Project → Ad units / Project settings
/// </summary>
public static class AdsConfig
{
#if UNITY_IOS
    public const string GameId = "6148132";
    public const string RewardedAdUnitId = "Rewarded_iOS";
    public const string InterstitialAdUnitId = "Interstitial_iOS";
#else
    // Dashboard'da Android Game ID'yi buraya yaz (iOS ile farklıdır)
    public const string GameId = "YOUR_ANDROID_GAME_ID";
    public const string RewardedAdUnitId = "Rewarded_Android";
    public const string InterstitialAdUnitId = "Interstitial_Android";
#endif

    /// <summary>
    /// Editor'da gerçek video yok; mock ekran gösterilir.
    /// </summary>
    public static bool UseEditorMockAd = true;

    public static bool TestMode =>
        Application.isEditor || Debug.isDebugBuild;

    public static bool HasValidKeys =>
        !string.IsNullOrWhiteSpace(GameId)
        && !GameId.StartsWith("YOUR_", System.StringComparison.OrdinalIgnoreCase);
}
