using UnityEngine;

/// <summary>
/// Oyuncunun tamamladığı maç sayısına göre bot hamle bekleme süresini hesaplar.
/// İlk 10 maç: 2s, her sonraki 10 maçta -0.1s, minimum 1.5s.
/// </summary>
public static class BotTurnThinkDelay
{
    public const float BaseDelaySeconds = 2f;
    public const float MinDelaySeconds = 1.5f;
    public const float DecrementPerTenMatchesSeconds = 0.1f;
    public const int MatchesPerTier = 10;

    public static float GetDelayForCompletedMatches(int completedMatches)
    {
        int tiers = Mathf.Max(0, completedMatches / MatchesPerTier);
        float delay = BaseDelaySeconds - tiers * DecrementPerTenMatchesSeconds;
        return Mathf.Max(MinDelaySeconds, delay);
    }

    public static int GetCompletedMatchCount()
    {
        LeagueSaveData save = LeagueService.Instance != null
            ? LeagueService.Instance.Save
            : LeagueRepository.Load();

        if (save?.standings == null)
        {
            return 0;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                return save.standings[i].played;
            }
        }

        return 0;
    }

    public static float GetDelayForCurrentPlayer()
    {
        return GetDelayForCompletedMatches(GetCompletedMatchCount());
    }
}
