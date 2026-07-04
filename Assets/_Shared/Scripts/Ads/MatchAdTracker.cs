using UnityEngine;

/// <summary>
/// Tamamlanan maç sayısını tutar; her N maçta bir interstitial gösterilir.
/// </summary>
public static class MatchAdTracker
{
    const string MatchCountKey = "pennyball.match.completed.count";
    public const int InterstitialEveryNMatches = 3;

    public static int CompletedMatchCount => PlayerPrefs.GetInt(MatchCountKey, 0);

    public static void RegisterMatchCompleted()
    {
        int count = CompletedMatchCount + 1;
        PlayerPrefs.SetInt(MatchCountKey, count);
        PlayerPrefs.Save();
        Debug.Log($"[Ads] Match completed count={count}");
    }

    public static bool ShouldShowInterstitial()
    {
        int count = CompletedMatchCount;
        return count > 0 && count % InterstitialEveryNMatches == 0;
    }
}
