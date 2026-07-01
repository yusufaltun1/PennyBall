using UnityEngine;

/// <summary>
/// Rakip bot beyni.
/// Öncelik 1: Player kalesine yaklaş (gate geçişi zorunlu).
/// Öncelik 2: Gate geçişi mümkün değilse sonraki hamle için pozisyon kur.
/// </summary>
public static class OpponentBotBrain
{
    public struct ShotPlan
    {
        public CoinIdentity Coin;
        public Vector3      Direction;
        public float        PullDistance;
        public bool         RespectsRules;
        public float        Score;
        public ShotKind     Kind;
        public float        GoalAdvanceMeters;
    }

    public enum ShotKind
    {
        Opening,
        MandatoryGatePass,
        GoalFinish,
        Advance,
        SetupSeparate,
        SetupClearBlocker,
        SetupReposition,
        Fallback
    }

    const float kStopDistPerPull     = 7.2f;
    const float kTravelDistanceScale = 0.9f;   // CoinDragController._travelDistanceScale ile aynı
    const float kEffectiveStopPerPull = kStopDistPerPull * kTravelDistanceScale;
    const float kNarrowGateWidth     = 0.20f;
    const float kParallelDot         = 0.80f;
    const float kParallelPerp        = 0.10f;
    const float kMinAdvancePrefer    = 0.06f;
    const float kGoalFinishBonus     = 8f;
    const float kGoalFinishPriority  = 200f;

    static readonly float[] kGoalBlends       = { 0f, 0.08f, 0.18f, 0.30f, 0.45f };
    static readonly float[] kPullRatios         = { 0.55f, 0.70f, 0.85f, 0.94f, 1.00f };
    static readonly float[] kGoalFinishBlends   = { 0f, 0.15f, 0.30f, 0.50f, 0.70f, 1.00f };
    static readonly float[] kGoalFinishPulls    = { 0.92f, 1.00f };
    const float kGatePassScoreBonus      = 4f;
    const float kRearShooterBonus        = 2.5f;

    // ── Ana giriş ────────────────────────────────────────────────────────────

    public static bool TryChooseShot(
        TeamRoundState        state,
        OpponentBotDifficulty difficulty,
        bool                  isResolvingMove,
        float                 gateMargin,
        int                   shotNumber,
        float                 coinBlockRadius,
        out ShotPlan          plan,
        out bool              pathBlocked)
    {
        plan        = default;
        pathBlocked = false;

        Vector3 goal = ResolvePlayerGoalCenter();
        if (goal == Vector3.zero)
        {
            goal = new Vector3(1.52f, 0.14f, 2.235f);
        }

        bool ok;
        if (shotNumber == 1)
        {
            ok = BuildOpeningShot(state, isResolvingMove, false, goal, out plan);
        }
        else
        {
            ok = TryBuildGoalFinishShot(
                    state,
                    isResolvingMove,
                    goal,
                    gateMargin,
                    coinBlockRadius,
                    difficulty,
                    out plan)
                || TryBuildDirectGatePassShot(
                    state,
                    isResolvingMove,
                    goal,
                    gateMargin,
                    out plan)
                || ChooseBestStrategicShot(
                    state,
                    isResolvingMove,
                    goal,
                    gateMargin,
                    coinBlockRadius,
                    difficulty,
                    out plan,
                    out pathBlocked);
        }

        if (ok && shotNumber >= 2
            && plan.Kind != ShotKind.MandatoryGatePass
            && plan.Kind != ShotKind.GoalFinish)
        {
            plan = ApplyNoise(plan, difficulty);
        }

        if (ok && shotNumber >= 2 && plan.Kind != ShotKind.GoalFinish)
        {
            plan = SnapGatePassPlan(state, plan);
        }

        return ok;
    }

    /// <summary>
    /// Kapıdan geçip kaleye ulaşma ihtimali varsa gol atmayı öncelikle.
    /// </summary>
    static bool TryBuildGoalFinishShot(
        TeamRoundState        state,
        bool                  isResolvingMove,
        Vector3               goal,
        float                 gateMargin,
        float                 coinBlockRadius,
        OpponentBotDifficulty difficulty,
        out ShotPlan          plan)
    {
        plan = default;
        Vector3 goalFlat = Flat(goal);
        float goalFocus = difficulty.GoalFocus;

        ShotPlan best = default;
        float    bestScore = float.MinValue;
        bool     found = false;

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity shooter = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, shooter, isResolvingMove, shooter.IsPassive))
            {
                continue;
            }

            if (!TeamRulesService.TryGetGateCoins(state, shooter, out CoinIdentity gateA, out CoinIdentity gateB))
            {
                continue;
            }

            CoinDragController dc = shooter.DragController;
            if (dc == null)
            {
                continue;
            }

            Vector3 origin = Flat(shooter.transform.position);
            Vector3 gateMid = GateMidpoint(gateA, gateB);
            Vector3 gateDir = SafeDir(gateMid - origin);
            Vector3 goalDir = SafeDir(goalFlat - origin);
            float distGoal = Vector3.Distance(origin, goalFlat);
            float distGate = Vector3.Distance(origin, gateMid);
            float gateWidth = GateWidth(gateA, gateB);
            float maxTravel = EffectiveTravelDistance(dc.MaxPullDistance);

            if (distGoal > maxTravel - 0.08f || IsPathBlocked(origin, goalFlat, shooter, coinBlockRadius))
            {
                continue;
            }

            float shooterRear = ScoreRearShooter(origin, gateMid, goalFlat);
            float minPull = PullForGatePass(dc, distGate, gateWidth);
            float goalPull = Mathf.Clamp(PullForDistance(dc, distGoal + 0.22f), minPull, dc.MaxPullDistance);

            for (int b = kGoalFinishBlends.Length - 1; b >= 0; b--)
            {
                float blend = kGoalFinishBlends[b];
                Vector3 dir = blend < 0.01f
                    ? gateDir
                    : Vector3.Lerp(gateDir, goalDir, blend).normalized;

                for (int p = 0; p < kGoalFinishPulls.Length; p++)
                {
                    float pull = Mathf.Max(minPull, goalPull * kGoalFinishPulls[p]);
                    pull = Mathf.Clamp(pull, dc.MinPullDistance, dc.MaxPullDistance);

                    float travel = EffectiveTravelDistance(pull);
                    if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
                    {
                        continue;
                    }

                    float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                    if (advance < distGoal - 0.18f)
                    {
                        continue;
                    }

                    float score = ScoreAdvancePlan(
                        origin, dir, goalFlat, gateWidth, advance, goalFocus,
                        ShotKind.GoalFinish, shooterRear, blend);
                    score += kGoalFinishPriority;
                    score += Mathf.Max(0f, Vector3.Dot(dir, goalDir)) * 3f;
                    score += (advance - distGoal) * 6f;

                    if (score <= bestScore)
                    {
                        continue;
                    }

                    bestScore = score;
                    found = true;
                    best = new ShotPlan
                    {
                        Coin = shooter,
                        Direction = dir,
                        PullDistance = pull,
                        RespectsRules = true,
                        Score = score,
                        Kind = ShotKind.GoalFinish,
                        GoalAdvanceMeters = advance
                    };
                }
            }
        }

        if (!found)
        {
            return false;
        }

        plan = best;
        Debug.Log($"[Bot] GOL-FIRSATI | {plan.Coin.name} | pull={plan.PullDistance:F3} | " +
                  $"mesafe={Vector3.Distance(Flat(plan.Coin.transform.position), goalFlat):F2}m | skor={plan.Score:F1}");
        return true;
    }

    /// <summary>
    /// En arkadaki coin → diğer ikisinin tam ortası, %100 güç. Kolay gate pozisyonları için.
    /// </summary>
    static bool TryBuildDirectGatePassShot(
        TeamRoundState state,
        bool           isResolvingMove,
        Vector3        goal,
        float          gateMargin,
        out ShotPlan   plan)
    {
        plan = default;
        Vector3 goalFlat = Flat(goal);

        CoinIdentity bestShooter = null;
        float        bestRear    = float.MinValue;
        CoinIdentity bestGateA   = null;
        CoinIdentity bestGateB   = null;

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity shooter = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, shooter, isResolvingMove, shooter.IsPassive))
            {
                continue;
            }

            if (!TeamRulesService.TryGetGateCoins(state, shooter, out CoinIdentity gateA, out CoinIdentity gateB))
            {
                continue;
            }

            Vector3 origin = Flat(shooter.transform.position);
            Vector3 gateMid = GateMidpoint(gateA, gateB);
            Vector3 gateDir = SafeDir(gateMid - origin);
            Vector3 goalDir = SafeDir(goalFlat - origin);

            if (Vector3.Dot(gateDir, goalDir) < 0.55f)
            {
                continue;
            }

            float gateWidth = GateWidth(gateA, gateB);
            if (gateWidth < 0.10f)
            {
                continue;
            }

            float rear = ScoreRearShooter(origin, gateMid, goalFlat);
            if (rear <= bestRear)
            {
                continue;
            }

            CoinDragController dc = shooter.DragController;
            if (dc == null)
            {
                continue;
            }

            bestRear = rear;
            bestShooter = shooter;
            bestGateA = gateA;
            bestGateB = gateB;
        }

        if (bestShooter == null)
        {
            return false;
        }

        CoinDragController shooterDc = bestShooter.DragController;
        Vector3 shooterOrigin = Flat(bestShooter.transform.position);
        Vector3 mid = GateMidpoint(bestGateA, bestGateB);
        Vector3 dir = SafeDir(mid - shooterOrigin);
        float maxPull = shooterDc.MaxPullDistance;
        float travel = EffectiveTravelDistance(maxPull);
        bool plannerGateOk = WillPassGate(shooterOrigin, dir, travel, bestGateA, bestGateB, gateMargin);
        float advance = EstimateGoalAdvance(shooterOrigin, dir, travel, goalFlat);

        plan = new ShotPlan
        {
            Coin = bestShooter,
            Direction = dir,
            PullDistance = maxPull,
            RespectsRules = true,
            Score = 100f,
            Kind = ShotKind.MandatoryGatePass,
            GoalAdvanceMeters = advance
        };

        Debug.Log($"[Bot] ZORUNLU-KAPI | {bestShooter.name} → [{bestGateA.name},{bestGateB.name}] | " +
                  $"pull={maxPull:F3} (max) | gateW={GateWidth(bestGateA, bestGateB):F2} | planOK={plannerGateOk}");
        return true;
    }

    static ShotPlan SnapGatePassPlan(TeamRoundState state, ShotPlan plan)
    {
        if (plan.Coin == null || plan.Coin.DragController == null)
        {
            return plan;
        }

        if (!TeamRulesService.TryGetGateCoins(state, plan.Coin, out CoinIdentity gateA, out CoinIdentity gateB))
        {
            return plan;
        }

        Vector3 origin = Flat(plan.Coin.transform.position);
        Vector3 gateMid = GateMidpoint(gateA, gateB);
        Vector3 gateDir = SafeDir(gateMid - origin);
        float gateDot = Vector3.Dot(plan.Direction, gateDir);

        if (plan.Kind == ShotKind.MandatoryGatePass || gateDot > 0.80f)
        {
            plan.Direction = gateDir;
            plan.PullDistance = plan.Coin.DragController.MaxPullDistance;
        }

        return plan;
    }

    // ── Açılış (1): orta para, kaleye doğru ilerle ───────────────────────────

    static bool BuildOpeningShot(
        TeamRoundState state,
        bool           isResolvingMove,
        bool           preferRightSide,
        Vector3        goal,
        out ShotPlan   plan)
    {
        plan = default;

        CoinIdentity coin = null;
        float bestX = preferRightSide ? float.MinValue : float.MaxValue;
        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity c = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive))
            {
                continue;
            }

            float x = c.transform.position.x;
            if (preferRightSide ? x > bestX : x < bestX)
            {
                bestX = x;
                coin = c;
            }
        }

        if (coin == null || coin.DragController == null)
        {
            return false;
        }

        CoinDragController dc = coin.DragController;
        Vector3 origin = Flat(coin.transform.position);
        Vector3 goalDir = (Flat(goal) - origin).normalized;
        float spread = preferRightSide ? 14f : -14f;
        Vector3 direction = (Quaternion.Euler(0f, spread, 0f) * goalDir).normalized;
        float pull = dc.MaxPullDistance * 0.78f;
        float advance = EstimateGoalAdvance(origin, direction, EffectiveTravelDistance(pull), Flat(goal));

        plan = new ShotPlan
        {
            Coin = coin,
            Direction = direction,
            PullDistance = pull,
            RespectsRules = true,
            Score = advance,
            Kind = ShotKind.Opening,
            GoalAdvanceMeters = advance
        };
        return true;
    }

    // ── Stratejik seçim (3+) ─────────────────────────────────────────────────

    static bool ChooseBestStrategicShot(
        TeamRoundState        state,
        bool                  isResolvingMove,
        Vector3               goal,
        float                 gateMargin,
        float                 coinBlockRadius,
        OpponentBotDifficulty difficulty,
        out ShotPlan          plan,
        out bool              pathBlocked)
    {
        plan        = default;
        pathBlocked = false;

        Vector3 goalFlat = Flat(goal);

        ShotPlan bestAdvance = default;
        float    bestAdvanceScore = float.MinValue;
        bool     hasAdvance = false;

        ShotPlan bestSetup = default;
        float    bestSetupScore = float.MinValue;
        bool     hasSetup = false;

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity shooter = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, shooter, isResolvingMove, shooter.IsPassive))
            {
                continue;
            }

            if (!TeamRulesService.TryGetGateCoins(state, shooter, out CoinIdentity gateA, out CoinIdentity gateB))
            {
                continue;
            }

            EvaluateShooterCandidates(
                shooter,
                gateA,
                gateB,
                goalFlat,
                gateMargin,
                coinBlockRadius,
                difficulty,
                ref bestAdvance,
                ref bestAdvanceScore,
                ref hasAdvance,
                ref bestSetup,
                ref bestSetupScore,
                ref hasSetup);
        }

        if (hasAdvance && bestAdvanceScore >= kMinAdvancePrefer)
        {
            plan = bestAdvance;
        if (TeamRulesService.TryGetGateCoins(state, plan.Coin, out CoinIdentity logGA, out CoinIdentity logGB))
        {
            Vector3 o = Flat(plan.Coin.transform.position);
            Vector3 gMid = GateMidpoint(logGA, logGB);
            float gateDot = Vector3.Dot(plan.Direction, SafeDir(gMid - o));
            Debug.Log($"[Bot] İLERLEME | {plan.Coin.name} | {plan.Kind} | +{plan.GoalAdvanceMeters:F2}m kaleye | " +
                      $"skor={plan.Score:F2} | gateHizası={gateDot:F2} | pull={plan.PullDistance:F3} | gate=[{logGA.name},{logGB.name}]");
        }
        else
        {
            Debug.Log($"[Bot] İLERLEME | {plan.Coin.name} | {plan.Kind} | +{plan.GoalAdvanceMeters:F2}m kaleye | skor={plan.Score:F2}");
        }

        return true;
        }

        if (hasSetup)
        {
            plan = bestSetup;
            Debug.Log($"[Bot] SETUP | {plan.Coin.name} | {plan.Kind} | skor={plan.Score:F2}");
            return true;
        }

        if (hasAdvance)
        {
            plan = bestAdvance;
            Debug.Log($"[Bot] ZAYIF-İLERLEME | {plan.Coin.name} | +{plan.GoalAdvanceMeters:F2}m | skor={plan.Score:F2}");
            return true;
        }

        pathBlocked = true;
        return false;
    }

    static void EvaluateShooterCandidates(
        CoinIdentity          shooter,
        CoinIdentity          gateA,
        CoinIdentity          gateB,
        Vector3               goalFlat,
        float                 gateMargin,
        float                 coinBlockRadius,
        OpponentBotDifficulty difficulty,
        ref ShotPlan          bestAdvance,
        ref float             bestAdvanceScore,
        ref bool              hasAdvance,
        ref ShotPlan          bestSetup,
        ref float             bestSetupScore,
        ref bool              hasSetup)
    {
        CoinDragController dc = shooter.DragController;
        if (dc == null)
        {
            return;
        }

        Vector3 origin = Flat(shooter.transform.position);
        Vector3 gateMid = GateMidpoint(gateA, gateB);
        float gateWidth = GateWidth(gateA, gateB);
        float distGoal = Vector3.Distance(origin, goalFlat);
        float goalFocus = difficulty.GoalFocus;
        float maxTravel = dc.MaxPullDistance * kEffectiveStopPerPull;

        Vector3 gateDir = SafeDir(gateMid - origin);
        Vector3 goalDir = SafeDir(goalFlat - origin);
        float shooterRearScore = ScoreRearShooter(origin, gateMid, goalFlat);
        float distGate = Vector3.Distance(origin, gateMid);

        // ── Aday 0: ZORUNLU — tam kapı ortasından geçiş (PennyBall3d AIController) ──
        AddGatePassCandidates(
            shooter,
            gateA,
            gateB,
            origin,
            gateMid,
            gateDir,
            goalDir,
            goalFlat,
            gateWidth,
            distGate,
            distGoal,
            gateMargin,
            coinBlockRadius,
            dc,
            goalFocus,
            shooterRearScore,
            ref bestAdvance,
            ref bestAdvanceScore,
            ref hasAdvance);

        // ── Aday 1: hafif kale karışımı (kapı öncelikli blend) ──
        for (int b = 0; b < kGoalBlends.Length; b++)
        {
            float blend = kGoalBlends[b] * Mathf.Lerp(1f, 0.45f, goalFocus);
            Vector3 dir = Vector3.Lerp(gateDir, goalDir, blend).normalized;

            float minPull = PullForGatePass(dc, distGate, gateWidth);
            for (int p = 0; p < kPullRatios.Length; p++)
            {
                float pull = Mathf.Max(minPull, dc.MaxPullDistance * kPullRatios[p]);
                float travel = EffectiveTravelDistance(pull);
                if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
                {
                    continue;
                }

                float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                bool canFinish = distGoal <= maxTravel - 0.1f
                                 && advance >= distGoal - 0.15f
                                 && !IsPathBlocked(origin, goalFlat, shooter, coinBlockRadius);

                ShotKind kind = canFinish ? ShotKind.GoalFinish : ShotKind.Advance;
                float score = ScoreAdvancePlan(
                    origin, dir, goalFlat, gateWidth, advance, goalFocus, kind, shooterRearScore, blend);

                TryAdoptAdvance(shooter, dir, pull, kind, score, advance, ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);
            }
        }

        // ── Aday 2: kale bitirici (yakın mesafe) ──
        if (distGoal <= maxTravel - 0.08f && !IsPathBlocked(origin, goalFlat, shooter, coinBlockRadius))
        {
            for (int b = 0; b < 3; b++)
            {
                float blend = Mathf.Lerp(0.15f, 0.45f, b / 2f);
                Vector3 dir = Vector3.Lerp(gateDir, goalDir, blend).normalized;
                float pull = Mathf.Max(PullForGatePass(dc, distGate, gateWidth), PullForDistance(dc, distGoal + 0.25f));
                float travel = EffectiveTravelDistance(pull);
                if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
                {
                    continue;
                }

                float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                float score = ScoreAdvancePlan(
                    origin, dir, goalFlat, gateWidth, advance, goalFocus, ShotKind.GoalFinish, shooterRearScore, blend)
                              + kGoalFinishBonus * goalFocus;
                TryAdoptAdvance(shooter, dir, pull, ShotKind.GoalFinish, score, advance, ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);
            }
        }

        // ── Setup: dar kapı — paraları ayır ──
        if (gateWidth < kNarrowGateWidth)
        {
            Vector3 dir = gateDir;
            float pull = dc.MaxPullDistance;
            float travel = EffectiveTravelDistance(pull);
            if (WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
            {
                float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                float setup = ScoreSetupPlan(origin, dir, travel, gateMid, goalFlat, gateWidth, advance, SetupReason.NarrowGate);
                TryAdoptSetup(shooter, dir, pull, ShotKind.SetupSeparate, setup, advance, ref bestSetup, ref bestSetupScore, ref hasSetup);
            }
        }

        // ── Setup: player engeli ──
        CoinIdentity blocker = FindBlockingPlayerCoin(origin, gateMid, shooter, coinBlockRadius * 1.15f);
        if (blocker == null)
        {
            blocker = FindBlockingPlayerCoin(origin, goalFlat, shooter, coinBlockRadius * 1.15f);
        }

        if (blocker != null)
        {
            Vector3 blockerPos = Flat(blocker.transform.position);
            Vector3 toBlocker = SafeDir(blockerPos - origin);
            Vector3 dir = Vector3.Lerp(toBlocker, gateDir, 0.25f).normalized;
            float pull = dc.MaxPullDistance;
            float travel = EffectiveTravelDistance(pull);
            if (WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
            {
                float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                float setup = ScoreSetupPlan(origin, dir, travel, gateMid, goalFlat, gateWidth, advance, SetupReason.Blocker)
                              + 0.6f * goalFocus;
                TryAdoptSetup(shooter, dir, pull, ShotKind.SetupClearBlocker, setup, advance, ref bestSetup, ref bestSetupScore, ref hasSetup);
            }
        }

        // ── Setup: kötü gate açısı ──
        if (IsPoorGateAngle(origin, gateA, gateB, gateDir))
        {
            float pull = dc.MaxPullDistance;
            float travel = EffectiveTravelDistance(pull);
            if (WillPassGate(origin, gateDir, travel, gateA, gateB, gateMargin))
            {
                float advance = EstimateGoalAdvance(origin, gateDir, travel, goalFlat);
                float setup = ScoreSetupPlan(origin, gateDir, travel, gateMid, goalFlat, gateWidth, advance, SetupReason.PoorAngle);
                TryAdoptSetup(shooter, gateDir, pull, ShotKind.SetupReposition, setup, advance, ref bestSetup, ref bestSetupScore, ref hasSetup);
            }
        }

        // ── Setup fallback: saf gate ortası (geçerli ama az ilerleme) ──
        {
            float pull = dc.MaxPullDistance;
            float travel = EffectiveTravelDistance(pull);
            if (WillPassGate(origin, gateDir, travel, gateA, gateB, gateMargin))
            {
                float advance = EstimateGoalAdvance(origin, gateDir, travel, goalFlat);
                float setup = ScoreSetupPlan(origin, gateDir, travel, gateMid, goalFlat, gateWidth, advance, SetupReason.Fallback);
                TryAdoptSetup(shooter, gateDir, pull, ShotKind.Fallback, setup, advance, ref bestSetup, ref bestSetupScore, ref hasSetup);
            }
        }
    }

    enum SetupReason { NarrowGate, Blocker, PoorAngle, Fallback }

    static void AddGatePassCandidates(
        CoinIdentity shooter,
        CoinIdentity gateA,
        CoinIdentity gateB,
        Vector3      origin,
        Vector3      gateMid,
        Vector3      gateDir,
        Vector3      goalDir,
        Vector3      goalFlat,
        float        gateWidth,
        float        distGate,
        float        distGoal,
        float        gateMargin,
        float        coinBlockRadius,
        CoinDragController dc,
        float        goalFocus,
        float        shooterRearScore,
        ref ShotPlan bestAdvance,
        ref float    bestAdvanceScore,
        ref bool     hasAdvance)
    {
        float maxPull = dc.MaxPullDistance;
        float gatePassPull = PullForGatePass(dc, distGate, gateWidth);

        // Kapı geçişi: önce tam güç, sonra hesaplanan minimum
        TryGatePassDirection(
            shooter, gateA, gateB, origin, gateMid, gateDir, goalFlat, gateWidth, distGoal,
            gateMargin, coinBlockRadius, dc, goalFocus, shooterRearScore, maxPull, gateDir,
            ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);

        if (gatePassPull < maxPull - 0.008f)
        {
            TryGatePassDirection(
                shooter, gateA, gateB, origin, gateMid, gateDir, goalFlat, gateWidth, distGoal,
                gateMargin, coinBlockRadius, dc, goalFocus, shooterRearScore, gatePassPull, gateDir,
                ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);
        }

        for (float yaw = -4f; yaw <= 4f; yaw += 4f)
        {
            if (Mathf.Abs(yaw) < 0.01f)
            {
                continue;
            }

            Vector3 dir = (Quaternion.Euler(0f, yaw, 0f) * gateDir).normalized;
            TryGatePassDirection(
                shooter, gateA, gateB, origin, gateMid, dir, goalFlat, gateWidth, distGoal,
                gateMargin, coinBlockRadius, dc, goalFocus, shooterRearScore, maxPull, dir,
                ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);
        }
    }

    static void TryGatePassDirection(
        CoinIdentity shooter,
        CoinIdentity gateA,
        CoinIdentity gateB,
        Vector3      origin,
        Vector3      gateMid,
        Vector3      dir,
        Vector3      goalFlat,
        float        gateWidth,
        float        distGoal,
        float        gateMargin,
        float        coinBlockRadius,
        CoinDragController dc,
        float        goalFocus,
        float        shooterRearScore,
        float        pull,
        Vector3      gateDirForScore,
        ref ShotPlan bestAdvance,
        ref float    bestAdvanceScore,
        ref bool     hasAdvance)
    {
        float travel = EffectiveTravelDistance(pull);
        float distGate = Vector3.Distance(origin, gateMid);
        float requiredTravel = ComputeGatePassTravelTarget(distGate, gateWidth);
        bool isMaxPull = pull >= dc.MaxPullDistance - 0.008f;
        if (!isMaxPull && travel < requiredTravel * 0.92f)
        {
            return;
        }

        if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
        {
            return;
        }

        float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
        float maxTravel = EffectiveTravelDistance(dc.MaxPullDistance);
        bool canFinish = distGoal <= maxTravel - 0.1f
                         && advance >= distGoal - 0.15f
                         && !IsPathBlocked(origin, goalFlat, shooter, coinBlockRadius);

        ShotKind kind = canFinish ? ShotKind.GoalFinish : ShotKind.Advance;
        float gateAlign = Vector3.Dot(dir, gateDirForScore);
        float score = ScoreAdvancePlan(
            origin, dir, goalFlat, gateWidth, advance, goalFocus, kind, shooterRearScore, 0f);
        score += kGatePassScoreBonus;
        score += Mathf.Max(0f, gateAlign) * 1.5f;
        score += shooterRearScore;
        score += (pull / dc.MaxPullDistance) * 2f;

        TryAdoptAdvance(shooter, dir, pull, kind, score, advance, ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);
    }

    /// <summary>
    /// Kaleye en uzak (arkada kalan) atıcıyı tercih et — gate geçişi için ideal.
    /// </summary>
    static float ScoreRearShooter(Vector3 origin, Vector3 gateMid, Vector3 goalFlat)
    {
        float shooterToGoal = Vector3.Distance(origin, goalFlat);
        float gateToGoal = Vector3.Distance(gateMid, goalFlat);
        float score = shooterToGoal * 0.8f;

        if (shooterToGoal > gateToGoal + 0.04f)
        {
            score += kRearShooterBonus;
        }

        Vector3 toGate = SafeDir(gateMid - origin);
        Vector3 toGoal = SafeDir(goalFlat - origin);
        score += Mathf.Max(0f, Vector3.Dot(toGate, toGoal)) * 1.2f;

        return score;
    }

    static float ScoreAdvancePlan(
        Vector3 origin,
        Vector3 shotDir,
        Vector3 goalFlat,
        float   gateWidth,
        float   advanceMeters,
        float   goalFocus,
        ShotKind kind,
        float   shooterRearScore,
        float   goalBlend)
    {
        Vector3 goalDir = SafeDir(goalFlat - origin);
        float align = (Vector3.Dot(shotDir, goalDir) + 1f) * 0.5f;
        float gateScore = Mathf.Clamp01(gateWidth / 0.40f);

        float score = advanceMeters * 4.5f;
        score += align * 1.2f * goalFocus;
        score += gateScore * 0.25f;
        score += shooterRearScore * 0.65f;

        if (goalBlend < 0.05f)
        {
            score += kGatePassScoreBonus * 0.5f;
        }

        if (kind == ShotKind.GoalFinish)
        {
            score += kGoalFinishBonus * goalFocus;
        }

        return score;
    }

    static float ScoreSetupPlan(
        Vector3 origin,
        Vector3 shotDir,
        float   travel,
        Vector3 gateMid,
        Vector3 goalFlat,
        float   gateWidth,
        float   advanceMeters,
        SetupReason reason)
    {
        Vector3 land = origin + shotDir.normalized * travel;
        float futureAlign = FutureAttackAlignment(land, gateMid, goalFlat);
        float score = futureAlign * 2.5f;
        score += advanceMeters * 1.2f;

        switch (reason)
        {
            case SetupReason.NarrowGate:
                score += (1f - Mathf.Clamp01(gateWidth / kNarrowGateWidth)) * 1.4f;
                break;
            case SetupReason.Blocker:
                score += 1.1f;
                break;
            case SetupReason.PoorAngle:
                score += 0.9f;
                break;
            case SetupReason.Fallback:
                score += 0.2f;
                break;
        }

        return score;
    }

    static float FutureAttackAlignment(Vector3 landPos, Vector3 gateMid, Vector3 goalFlat)
    {
        Vector3 toGate = SafeDir(gateMid - landPos);
        Vector3 toGoal = SafeDir(goalFlat - landPos);
        return (Vector3.Dot(toGate, toGoal) + 1f) * 0.5f;
    }

    static float EstimateGoalAdvance(Vector3 origin, Vector3 direction, float travel, Vector3 goalFlat)
    {
        Vector3 land = origin + direction.normalized * travel;
        return Vector3.Distance(origin, goalFlat) - Vector3.Distance(land, goalFlat);
    }

    static void TryAdoptAdvance(
        CoinIdentity shooter,
        Vector3      direction,
        float        pull,
        ShotKind     kind,
        float        score,
        float        advance,
        ref ShotPlan bestPlan,
        ref float    bestScore,
        ref bool     found)
    {
        if (score <= bestScore)
        {
            return;
        }

        bestScore = score;
        bestPlan = BuildPlan(shooter, direction, pull, kind, score, advance);
        found = true;
    }

    static void TryAdoptSetup(
        CoinIdentity shooter,
        Vector3      direction,
        float        pull,
        ShotKind     kind,
        float        score,
        float        advance,
        ref ShotPlan bestPlan,
        ref float    bestScore,
        ref bool     found)
    {
        if (score <= bestScore)
        {
            return;
        }

        bestScore = score;
        bestPlan = BuildPlan(shooter, direction, pull, kind, score, advance);
        found = true;
    }

    static ShotPlan BuildPlan(
        CoinIdentity shooter,
        Vector3      direction,
        float        pull,
        ShotKind     kind,
        float        score,
        float        advance)
    {
        return new ShotPlan
        {
            Coin = shooter,
            Direction = direction.normalized,
            PullDistance = pull,
            RespectsRules = true,
            Score = score,
            Kind = kind,
            GoalAdvanceMeters = advance
        };
    }

    // ── Gate / yol analizi ───────────────────────────────────────────────────

    static bool WillPassGate(
        Vector3      origin,
        Vector3      direction,
        float        travelDistance,
        CoinIdentity gateA,
        CoinIdentity gateB,
        float        gateMargin)
    {
        if (direction.sqrMagnitude < 0.0001f || travelDistance <= 0.01f)
        {
            return false;
        }

        Vector3 end = origin + direction.normalized * travelDistance;
        return PassBetweenValidator.DidPassBetween(
            origin,
            end,
            gateA.transform.position,
            gateB.transform.position,
            gateMargin);
    }

    static bool IsPoorGateAngle(Vector3 shooterPos, CoinIdentity gateA, CoinIdentity gateB, Vector3 shotDir)
    {
        Vector3 a = Flat(gateA.transform.position);
        Vector3 b = Flat(gateB.transform.position);
        Vector3 s = Flat(shooterPos);
        Vector3 ab = b - a;
        float abLen = ab.magnitude;
        if (abLen < 0.001f)
        {
            return true;
        }

        Vector2 abDir = new Vector2(ab.x / abLen, ab.z / abLen);
        Vector2 perp = new Vector2(-abDir.y, abDir.x);
        Vector2 toShooter = new Vector2(s.x - a.x, s.z - a.z);
        float perpDist = Mathf.Abs(Vector2.Dot(toShooter, perp));

        Vector2 shot2 = new Vector2(shotDir.x, shotDir.z).normalized;
        float parallel = Mathf.Abs(Vector2.Dot(shot2, abDir));

        return perpDist < kParallelPerp && parallel > kParallelDot;
    }

    static bool IsPathBlocked(
        Vector3 origin,
        Vector3 goalPos,
        CoinIdentity shooter,
        float blockRadius)
    {
        CoinIdentity[] all = Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        Vector3 toGoal = goalPos - origin;
        float totalD = toGoal.magnitude;
        if (totalD < 0.001f)
        {
            return false;
        }

        Vector3 dir = toGoal / totalD;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == shooter)
            {
                continue;
            }

            Vector3 toBlocker = Flat(all[i].transform.position) - origin;
            float fwd = Vector3.Dot(toBlocker, dir);
            if (fwd < 0.05f || fwd > totalD)
            {
                continue;
            }

            float lateral = (toBlocker - dir * fwd).magnitude;
            if (lateral < blockRadius)
            {
                return true;
            }
        }

        return false;
    }

    static CoinIdentity FindBlockingPlayerCoin(
        Vector3 origin,
        Vector3 target,
        CoinIdentity shooter,
        float blockRadius)
    {
        CoinIdentity[] all = Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        Vector3 toTarget = target - origin;
        float totalD = toTarget.magnitude;
        if (totalD < 0.001f)
        {
            return null;
        }

        Vector3 dir = toTarget / totalD;
        CoinIdentity best = null;
        float bestFwd = float.MaxValue;

        for (int i = 0; i < all.Length; i++)
        {
            CoinIdentity coin = all[i];
            if (coin == shooter || coin.Team != CoinTeam.Player)
            {
                continue;
            }

            Vector3 toBlocker = Flat(coin.transform.position) - origin;
            float fwd = Vector3.Dot(toBlocker, dir);
            if (fwd < 0.05f || fwd > totalD)
            {
                continue;
            }

            float lateral = (toBlocker - dir * fwd).magnitude;
            if (lateral < blockRadius && fwd < bestFwd)
            {
                bestFwd = fwd;
                best = coin;
            }
        }

        return best;
    }

    // ── Gürültü ──────────────────────────────────────────────────────────────

    static ShotPlan ApplyNoise(ShotPlan plan, OpponentBotDifficulty difficulty)
    {
        if (plan.Coin == null || plan.Coin.DragController == null)
        {
            return plan;
        }

        CoinDragController dc = plan.Coin.DragController;
        float complianceScale = Mathf.Lerp(1f, 0.15f, difficulty.RuleCompliance);

        float yaw = Random.Range(-difficulty.AimNoiseDegrees, difficulty.AimNoiseDegrees) * complianceScale;
        plan.Direction = (Quaternion.Euler(0f, yaw, 0f) * plan.Direction).normalized;

        float pullNoise = Random.Range(-difficulty.PullNoise, difficulty.PullNoise) * complianceScale;
        plan.PullDistance = Mathf.Clamp(
            plan.PullDistance + pullNoise,
            dc.MinPullDistance,
            dc.MaxPullDistance);

        return plan;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    /// <summary>Kapı düzlemini geçip öteye taşınmak için gereken mesafe.</summary>
    static float ComputeGatePassTravelTarget(float distToGateMid, float gateWidth)
    {
        return distToGateMid + gateWidth * 0.5f + 0.90f;
    }

    static float PullForGatePass(CoinDragController dc, float distToGateMid, float gateWidth)
    {
        return PullForDistance(dc, ComputeGatePassTravelTarget(distToGateMid, gateWidth));
    }

    static float EffectiveTravelDistance(float pull)
    {
        return pull * kEffectiveStopPerPull;
    }

    static float EffectiveTravelDistance(CoinDragController dc, float pull)
    {
        return EffectiveTravelDistance(pull);
    }

    static float PullForDistance(CoinDragController dc, float meters)
        => Mathf.Clamp(meters / kEffectiveStopPerPull, dc.MinPullDistance, dc.MaxPullDistance);

    static Vector3 GateMidpoint(CoinIdentity gateA, CoinIdentity gateB)
        => (Flat(gateA.transform.position) + Flat(gateB.transform.position)) * 0.5f;

    static float GateWidth(CoinIdentity gateA, CoinIdentity gateB)
        => Vector3.Distance(Flat(gateA.transform.position), Flat(gateB.transform.position));

    static Vector3 SafeDir(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude < 0.0001f ? Vector3.forward : v.normalized;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static Vector3 ResolvePlayerGoalCenter()
    {
        GoalZone[] zones = Object.FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            GoalZone z = zones[i];
            if (z.transform.parent != null && z.transform.parent.name.Contains("_P"))
            {
                return z.transform.position;
            }
        }

        return Vector3.zero;
    }
}
