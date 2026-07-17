using UnityEngine;

/// <summary>
/// Oyuncu maç sonunda level atlayacaksa tier'a göre bot AI gücünü ve think süresini yükseltir:
/// Lv 1-4 → tier 5 ayarları | Lv 5-9 → tier 10 ayarları | Lv 10-14 → tier 15 ayarları.
/// Diğer durumlarda varsayılan strength (7) + maç sayısına göre think algoritması kullanılır.
/// </summary>
public static class BotLevelUpBoostPolicy
{
    public const float BoostThinkDelaySeconds = 2f;

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
        public LevelUpBoostTier BoostTier { get; }
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
        public int MilestoneTargetLevel { get; }
        public bool WillLevelUpAfterMatch { get; }

        public AiConfig(
            AiConfigMode mode,
            LevelUpBoostTier boostTier,
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
            int milestoneTargetLevel,
            bool willLevelUpAfterMatch)
        {
            Mode = mode;
            BoostTier = boostTier;
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
            MilestoneTargetLevel = milestoneTargetLevel;
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

        if (!willLevelUp)
        {
            return BuildConfig(
                AiConfigMode.Normal,
                LevelUpBoostTier.None,
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
                0,
                false);
        }

        LevelUpBoostTier tier = GetBoostTierForLevel(levelBefore);
        if (tier == LevelUpBoostTier.None)
        {
            return BuildConfig(
                AiConfigMode.Normal,
                LevelUpBoostTier.None,
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
                0,
                true);
        }

        int boostStrength = GetStrengthForTier(tier);
        return BuildConfig(
            AiConfigMode.LevelUpBoost,
            tier,
            boostStrength,
            BoostThinkDelaySeconds,
            normalStrength,
            normalThink,
            levelBefore,
            levelAfter,
            totalXp,
            matchXp,
            xpInLevel,
            xpToNext,
            GetMilestoneTargetForTier(tier),
            true);
    }

    public static int GetNormalStrength() => DefaultBaseStrength;

    static AiConfig BuildConfig(
        AiConfigMode mode,
        LevelUpBoostTier boostTier,
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
        int milestoneTargetLevel,
        bool willLevelUpAfterMatch)
    {
        return new AiConfig(
            mode,
            boostTier,
            appliedStrength,
            appliedThinkDelaySeconds,
            normalStrength,
            normalThinkDelaySeconds,
            playerLevelBefore,
            predictedLevelAfterMatch,
            totalXpBefore,
            matchXpReward,
            xpInCurrentLevel,
            xpRequiredForNextLevel,
            milestoneTargetLevel,
            willLevelUpAfterMatch);
    }

    static LevelUpBoostTier GetBoostTierForLevel(int playerLevelBefore)
    {
        if (playerLevelBefore < 5)
        {
            return LevelUpBoostTier.UntilLevel5;
        }

        if (playerLevelBefore < 10)
        {
            return LevelUpBoostTier.UntilLevel10;
        }

        if (playerLevelBefore < 15)
        {
            return LevelUpBoostTier.UntilLevel15;
        }

        return LevelUpBoostTier.None;
    }

    static int GetStrengthForTier(LevelUpBoostTier tier)
    {
        return tier switch
        {
            LevelUpBoostTier.UntilLevel5 => TierStrengthUntil5,
            LevelUpBoostTier.UntilLevel10 => TierStrengthUntil10,
            LevelUpBoostTier.UntilLevel15 => TierStrengthUntil15,
            _ => DefaultBaseStrength
        };
    }

    static int GetMilestoneTargetForTier(LevelUpBoostTier tier)
    {
        return tier switch
        {
            LevelUpBoostTier.UntilLevel5 => 5,
            LevelUpBoostTier.UntilLevel10 => 10,
            LevelUpBoostTier.UntilLevel15 => 15,
            _ => 0
        };
    }

    public static void LogAiConfig(int strength, float thinkSeconds, in AiConfig config, int shotNumber = 0)
    {
        // string context = shotNumber > 0 ? $"atış#{shotNumber}" : "maç";
        // string mod = config.Mode == AiConfigMode.LevelUpBoost
        //     ? $"BOOST→Lv{config.MilestoneTargetLevel}"
        //     : "Normal";
        //
        // Debug.Log(
        //     $"[Bot AI] {context} | strength={strength} | think={thinkSeconds:F2}s | mod={mod} | " +
        //     $"oyuncuLv={config.PlayerLevelBefore} | maç#{BotTurnThinkDelay.GetCompletedMatchCount()}");
    }

    public static void LogAiConfigInspector(int strength, float thinkSeconds)
    {
        // Debug.Log($"[Bot AI] maç | strength={strength} | think={thinkSeconds:F2}s | mod=Inspector");
    }
}
