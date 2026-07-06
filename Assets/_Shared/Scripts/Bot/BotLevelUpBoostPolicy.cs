using UnityEngine;

/// <summary>
/// Oyuncu maç sonunda level atlayacaksa tier'a göre bot AI gücünü ve think süresini yükseltir:
/// Lv 1-4 → tier 5 ayarları | Lv 5-9 → tier 10 ayarları | Lv 10-14 → tier 15 ayarları.
/// Diğer durumlarda varsayılan strength (7) + maç sayısına göre think algoritması kullanılır.
/// </summary>
public static class BotLevelUpBoostPolicy
{
    public const float MilestoneThinkDelaySeconds = 2f;

    public const int TierStrengthUntil5 = 8;
    public const int TierStrengthUntil10 = 9;
    public const int TierStrengthUntil15 = 15;

    public const int DefaultBaseStrength = 7;

    public enum LevelUpBoostTier
    {
        None,
        UntilLevel5,
        UntilLevel10,
        UntilLevel15
    }

    public enum AiConfigMode
    {
        Normal,
        LevelUpBoost
    }

    public readonly struct AiConfig
    {
        public AiConfigMode Mode { get; }
        public int AppliedStrength { get; }
        public float AppliedThinkDelaySeconds { get; }
        public int NormalStrength { get; }
        public float NormalThinkDelaySeconds { get; }
        public int PlayerLevelBefore { get; }
        public int PredictedLevelAfterMatch { get; }
        public int TotalXpBefore { get; }
        public int MatchXpReward { get; }
        public int XpInCurrentLevel { get; }
        public int XpRequiredForNextLevel { get; }
        public LevelUpBoostTier BoostTier { get; }
        public bool WillLevelUpAfterMatch { get; }

        public AiConfig(
            AiConfigMode mode,
            int appliedStrength,
            float appliedThinkDelaySeconds,
            int normalStrength,
            float normalThinkDelaySeconds,
            int playerLevelBefore,
            int predictedLevelAfterMatch,
            int totalXpBefore,
            int matchXpReward,
            int xpInCurrentLevel,
            int xpRequiredForNextLevel,
            LevelUpBoostTier boostTier,
            bool willLevelUpAfterMatch)
        {
            Mode = mode;
            AppliedStrength = appliedStrength;
            AppliedThinkDelaySeconds = appliedThinkDelaySeconds;
            NormalStrength = normalStrength;
            NormalThinkDelaySeconds = normalThinkDelaySeconds;
            PlayerLevelBefore = playerLevelBefore;
            PredictedLevelAfterMatch = predictedLevelAfterMatch;
            TotalXpBefore = totalXpBefore;
            MatchXpReward = matchXpReward;
            XpInCurrentLevel = xpInCurrentLevel;
            XpRequiredForNextLevel = xpRequiredForNextLevel;
            BoostTier = boostTier;
            WillLevelUpAfterMatch = willLevelUpAfterMatch;
        }
    }

    public static AiConfig Evaluate(bool useInspectorTurnDelay, float inspectorTurnDelaySeconds)
    {
        int levelBefore = WalletService.Level;
        int totalXp = WalletService.TotalXp;
        int matchXp = WalletService.XpPerMatch;
        int xpInLevel = WalletService.XpInCurrentLevel;
        int xpToNext = WalletService.XpToNextLevel;
        int levelAfter = PlayerLevelProgression.GetLevelFromTotalXp(totalXp + matchXp);
        bool willLevelUp = levelAfter > levelBefore;

        int normalStrength = GetNormalStrength();
        float normalThink = useInspectorTurnDelay
            ? inspectorTurnDelaySeconds
            : BotTurnThinkDelay.GetDelayForCurrentPlayer();

        if (!willLevelUp || !TryGetBoostForLevel(levelBefore, out LevelUpBoostTier tier, out int boostStrength))
        {
            return new AiConfig(
                AiConfigMode.Normal,
                normalStrength,
                normalThink,
                normalStrength,
                normalThink,
                levelBefore,
                levelAfter,
                totalXp,
                matchXp,
                xpInLevel,
                xpToNext,
                LevelUpBoostTier.None,
                willLevelUp);
        }

        return new AiConfig(
            AiConfigMode.LevelUpBoost,
            boostStrength,
            MilestoneThinkDelaySeconds,
            normalStrength,
            normalThink,
            levelBefore,
            levelAfter,
            totalXp,
            matchXp,
            xpInLevel,
            xpToNext,
            tier,
            true);
    }

    public static int GetNormalStrength()
    {
        return DefaultBaseStrength;
    }

    /// <summary>
    /// Oyuncunun mevcut seviyesine göre level-up boost tier'ını döner.
    /// Lv 1-4 → 5'e kadar | Lv 5-9 → 10'a kadar | Lv 10-14 → 15'e kadar.
    /// </summary>
    public static bool TryGetBoostForLevel(int playerLevel, out LevelUpBoostTier tier, out int strength)
    {
        if (playerLevel < 5)
        {
            tier = LevelUpBoostTier.UntilLevel5;
            strength = TierStrengthUntil5;
            return true;
        }

        if (playerLevel < 10)
        {
            tier = LevelUpBoostTier.UntilLevel10;
            strength = TierStrengthUntil10;
            return true;
        }

        if (playerLevel < 15)
        {
            tier = LevelUpBoostTier.UntilLevel15;
            strength = TierStrengthUntil15;
            return true;
        }

        tier = LevelUpBoostTier.None;
        strength = 0;
        return false;
    }

    static string FormatBoostTier(LevelUpBoostTier tier)
    {
        return tier switch
        {
            LevelUpBoostTier.UntilLevel5 => "BOOST→5",
            LevelUpBoostTier.UntilLevel10 => "BOOST→10",
            LevelUpBoostTier.UntilLevel15 => "BOOST→15",
            _ => "Normal"
        };
    }

    public static void LogAiConfig(int strength, float thinkSeconds, in AiConfig config, int shotNumber = 0)
    {
        string context = shotNumber > 0 ? $"atış#{shotNumber}" : "maç";
        string mod = config.Mode == AiConfigMode.LevelUpBoost
            ? FormatBoostTier(config.BoostTier)
            : "Normal";

        Debug.Log(
            $"[Bot AI] {context} | strength={strength} | think={thinkSeconds:F2}s | mod={mod} | " +
            $"oyuncuLv={config.PlayerLevelBefore} | maç#{MatchAdTracker.CompletedMatchCount}");
    }

    public static void LogAiConfigInspector(int strength, float thinkSeconds)
    {
        Debug.Log($"[Bot AI] maç | strength={strength} | think={thinkSeconds:F2}s | mod=Inspector");
    }
}
