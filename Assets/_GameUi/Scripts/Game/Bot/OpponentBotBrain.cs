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
        SetupEnablePass,
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
    /// <summary>Aynı geçersiz kapı denemesinden sonra alternatif aramaya geç.</summary>
    const int   kInvalidGateFailEscape = 1;
    /// <summary>Planlama mesafesini fizikten biraz daha kötümser tut (iyimser yetişme → sonsuz retry).</summary>
    const float kReachTravelSafety   = 0.88f;

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
        out bool              pathBlocked,
        CoinIdentity          demoteShooter = null,
        int                   consecutiveInvalidGateFails = 0)
    {
        plan        = default;
        pathBlocked = false;

        Vector3 goal = ResolvePlayerGoalCenter();
        if (goal == Vector3.zero)
        {
            goal = new Vector3(1.52f, 0.14f, 2.235f);
        }

        bool skipMandatory = consecutiveInvalidGateFails >= kInvalidGateFailEscape;
        bool ok;
        if (shotNumber == 1)
        {
            ok = BuildOpeningShot(state, isResolvingMove, false, goal, difficulty, out plan);
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
                || (!skipMandatory && TryBuildDirectGatePassShot(
                    state,
                    isResolvingMove,
                    goal,
                    gateMargin,
                    difficulty,
                    demoteShooter,
                    consecutiveInvalidGateFails,
                    out plan))
                || ChooseBestStrategicShot(
                    state,
                    isResolvingMove,
                    goal,
                    gateMargin,
                    coinBlockRadius,
                    difficulty,
                    demoteShooter,
                    consecutiveInvalidGateFails,
                    out plan,
                    out pathBlocked);
        }

        // Fail sonrası aynı paraya yapışmayı son güvenlik ağıyla kes.
        if (ok
            && demoteShooter != null
            && consecutiveInvalidGateFails >= kInvalidGateFailEscape
            && plan.Coin == demoteShooter
            && plan.Kind != ShotKind.GoalFinish)
        {
            Debug.Log($"[Bot] FAIL-ESCAPE hard-block | {demoteShooter.name} tekrar seçildi, alternatif aranıyor");
            ok = ChooseBestStrategicShot(
                state,
                isResolvingMove,
                goal,
                gateMargin,
                coinBlockRadius,
                difficulty,
                demoteShooter,
                consecutiveInvalidGateFails,
                out plan,
                out pathBlocked,
                forceExcludeDemoted: true);
        }

        if (ok && shotNumber >= 2 && plan.Kind != ShotKind.GoalFinish)
        {
            plan = SnapGatePassPlan(state, plan, difficulty);
        }

        if (ok)
        {
            plan = FinalizePlanForDifficulty(plan, difficulty, shotNumber);
        }

        return ok;
    }

    /// <summary>
    /// Açık koridor (atış–kale arasında engel yok) ve fiziksel olarak yetişilebiliyorsa
    /// gol atışına kesin öncelik ver.
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
        float laneRadius = Mathf.Max(coinBlockRadius, 0.10f);

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
            float maxPull = StrengthMaxPull(dc, difficulty);
            float maxTravel = EffectiveTravelDistance(maxPull);

            // Bariz gol: kaleye yetiş + koridor boş.
            // Kapıyı oluşturan iki takım arkadaşı engel sayılmaz (koridorun kenar direkleri).
            if (distGoal > maxTravel + 0.05f)
            {
                continue;
            }

            if (!IsClearShotLane(origin, goalFlat, shooter, laneRadius, gateA, gateB))
            {
                continue;
            }

            float shooterRear = ScoreRearShooter(origin, gateMid, goalFlat);
            float minGatePull = PullForGatePass(dc, distGate, gateWidth);
            float goalPull = Mathf.Clamp(PullForDistance(dc, distGoal + 0.28f), dc.MinPullDistance, maxPull);

            // Kaleye hizalı blend'lerden başla; kapı kuralını da sağlayan ilk güçlü adayı al.
            for (int b = kGoalFinishBlends.Length - 1; b >= 0; b--)
            {
                float blend = kGoalFinishBlends[b];
                Vector3 dir = blend < 0.01f
                    ? gateDir
                    : Vector3.Lerp(gateDir, goalDir, blend).normalized;

                // Açık koridorda yan sapma da dene (dar kapı / hafif açı).
                float[] yaws = blend >= 0.5f ? new[] { 0f, -3f, 3f, -6f, 6f } : new[] { 0f };
                for (int yi = 0; yi < yaws.Length; yi++)
                {
                    Vector3 aim = (Quaternion.Euler(0f, yaws[yi], 0f) * dir).normalized;

                    for (int p = 0; p < kGoalFinishPulls.Length; p++)
                    {
                        float pull = Mathf.Max(
                            minGatePull,
                            Mathf.Max(goalPull * kGoalFinishPulls[p], maxPull * 0.94f));
                        pull = Mathf.Clamp(pull, dc.MinPullDistance, maxPull);

                        float travel = EffectiveTravelDistance(pull);
                        if (!WillPassGate(origin, aim, travel, gateA, gateB, gateMargin))
                        {
                            continue;
                        }

                        if (!ShotReachesGoal(origin, aim, travel, goalFlat, distGoal))
                        {
                            continue;
                        }

                        float advance = EstimateGoalAdvance(origin, aim, travel, goalFlat);
                        Vector3 land = origin + aim * travel;
                        float score = ScoreAdvancePlan(
                            origin, aim, goalFlat, gateWidth, advance, goalFocus,
                            ShotKind.GoalFinish, shooterRear, blend);
                        score += kGoalFinishPriority;
                        score += Mathf.Max(0f, Vector3.Dot(aim, goalDir)) * 5f;
                        score += blend * 4f;
                        score += (advance - distGoal) * 8f;
                        score += (1f - Mathf.Clamp01(Vector3.Distance(land, goalFlat) / 0.35f)) * 6f;

                        if (score <= bestScore)
                        {
                            continue;
                        }

                        bestScore = score;
                        found = true;
                        best = new ShotPlan
                        {
                            Coin = shooter,
                            Direction = aim,
                            PullDistance = pull,
                            RespectsRules = true,
                            Score = score,
                            Kind = ShotKind.GoalFinish,
                            GoalAdvanceMeters = advance
                        };
                    }
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

    static bool IsClearShotLane(
        Vector3 origin,
        Vector3 target,
        CoinIdentity shooter,
        float blockRadius,
        CoinIdentity ignoreGateA = null,
        CoinIdentity ignoreGateB = null)
    {
        return !IsPathBlocked(origin, target, shooter, blockRadius, ignoreGateA, ignoreGateB);
    }

    static bool ShotReachesGoal(
        Vector3 origin,
        Vector3 direction,
        float travel,
        Vector3 goalFlat,
        float distGoal)
    {
        Vector3 land = origin + direction.normalized * travel;
        float landToGoal = Vector3.Distance(land, goalFlat);
        if (landToGoal <= 0.28f)
        {
            return true;
        }

        float advance = distGoal - landToGoal;
        if (advance >= distGoal - 0.22f)
        {
            return true;
        }

        // Kaleyi geçip arkaya taşma: origin→goal doğrultusunda goal'ü geçtiyse gol say.
        Vector3 toGoal = goalFlat - origin;
        float goalDist = toGoal.magnitude;
        if (goalDist < 0.001f)
        {
            return true;
        }

        Vector3 goalAxis = toGoal / goalDist;
        float landProj = Vector3.Dot(land - origin, goalAxis);
        float lateral = (land - origin - goalAxis * landProj).magnitude;
        return landProj >= goalDist - 0.12f && lateral <= 0.30f;
    }

    /// <summary>
    /// En arkadaki coin → diğer ikisinin tam ortası, %100 güç. Kolay gate pozisyonları için.
    /// </summary>
    static bool TryBuildDirectGatePassShot(
        TeamRoundState        state,
        bool                  isResolvingMove,
        Vector3               goal,
        float                 gateMargin,
        OpponentBotDifficulty difficulty,
        CoinIdentity          demoteShooter,
        int                   consecutiveInvalidGateFails,
        out ShotPlan          plan)
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

            // Aynı başarısız atıcıyı tekrar zorunlu kapıya zorlama.
            if (demoteShooter != null
                && shooter == demoteShooter
                && consecutiveInvalidGateFails >= 1)
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

            if (Vector3.Dot(gateDir, goalDir) < 0.55f)
            {
                continue;
            }

            float gateWidth = GateWidth(gateA, gateB);
            if (gateWidth < 0.10f)
            {
                continue;
            }

            // Max güçte bile kapıya ulaşamıyorsa zorunlu aday değil.
            if (!CanReachGateAtMaxPull(origin, gateA, gateB, dc, gateMargin, difficulty))
            {
                continue;
            }

            float rear = ScoreRearShooter(origin, gateMid, goalFlat);
            if (rear <= bestRear)
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
        float maxPull = StrengthMaxPull(shooterDc, difficulty) * difficulty.GatePassPullScale;
        maxPull = ClampStrengthPull(shooterDc, difficulty, maxPull);
        float travel = EffectiveTravelDistance(maxPull);
        if (!WillPassGate(shooterOrigin, dir, travel, bestGateA, bestGateB, gateMargin))
        {
            Debug.Log($"[Bot] ZORUNLU-KAPI ATLANDI | {bestShooter.name} max güçte kapıya ulaşamaz");
            return false;
        }

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
                  $"pull={maxPull:F3} (max) | gateW={GateWidth(bestGateA, bestGateB):F2} | planOK=True");
        return true;
    }

    static ShotPlan SnapGatePassPlan(TeamRoundState state, ShotPlan plan, OpponentBotDifficulty difficulty)
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
        CoinDragController dc = plan.Coin.DragController;

        if (plan.Kind == ShotKind.MandatoryGatePass
            || (plan.Kind != ShotKind.SetupEnablePass
                && plan.Kind != ShotKind.SetupSeparate
                && plan.Kind != ShotKind.SetupClearBlocker
                && plan.Kind != ShotKind.SetupReposition
                && gateDot > 0.80f))
        {
            plan.Direction = gateDir;
            plan.PullDistance = ClampStrengthPull(
                dc,
                difficulty,
                StrengthMaxPull(dc, difficulty) * difficulty.GatePassPullScale);
        }

        return plan;
    }

    // ── Açılış (1): orta para, kaleye doğru ilerle ───────────────────────────

    static bool BuildOpeningShot(
        TeamRoundState        state,
        bool                  isResolvingMove,
        bool                  preferRightSide,
        Vector3               goal,
        OpponentBotDifficulty difficulty,
        out ShotPlan          plan)
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
        float spread = preferRightSide ? difficulty.OpeningAimSpreadDegrees : -difficulty.OpeningAimSpreadDegrees;
        Vector3 direction = (Quaternion.Euler(0f, spread, 0f) * goalDir).normalized;
        float pull = ClampStrengthPull(
            dc,
            difficulty,
            StrengthMaxPull(dc, difficulty) * difficulty.OpeningPowerScale);
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
        CoinIdentity          demoteShooter,
        int                   consecutiveInvalidGateFails,
        out ShotPlan          plan,
        out bool              pathBlocked,
        bool                  forceExcludeDemoted = false)
    {
        plan        = default;
        pathBlocked = false;

        Vector3 goalFlat = Flat(goal);
        bool excludeDemoted = forceExcludeDemoted
                              || (demoteShooter != null && consecutiveInvalidGateFails >= kInvalidGateFailEscape);

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

            // Fail streak: başarısız parayı tamamen bırak, başka coin dene.
            if (excludeDemoted && demoteShooter != null && shooter == demoteShooter)
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
                demoteShooter,
                consecutiveInvalidGateFails,
                excludeDemoted,
                ref bestAdvance,
                ref bestAdvanceScore,
                ref hasAdvance,
                ref bestSetup,
                ref bestSetupScore,
                ref hasSetup);
        }

        AddEnablePassSetupCandidates(
            state,
            isResolvingMove,
            goalFlat,
            gateMargin,
            difficulty,
            demoteShooter,
            consecutiveInvalidGateFails,
            excludeDemoted,
            ref bestSetup,
            ref bestSetupScore,
            ref hasSetup);

        // Fail sonrası: başka coin ile basit kapı geçişi yoksa zorla üret.
        if (excludeDemoted && demoteShooter != null && !hasAdvance && !hasSetup)
        {
            TryBuildForcedAlternateShot(
                state,
                isResolvingMove,
                goalFlat,
                gateMargin,
                difficulty,
                demoteShooter,
                ref bestAdvance,
                ref bestAdvanceScore,
                ref hasAdvance,
                ref bestSetup,
                ref bestSetupScore,
                ref hasSetup);
        }

        // Stratejik yoldan da çıksa bariz gol bitirici setup/escape'i ezer.
        if (hasAdvance && bestAdvance.Kind == ShotKind.GoalFinish)
        {
            plan = bestAdvance;
            Debug.Log($"[Bot] GOL-FIRSATI (stratejik) | {plan.Coin.name} | pull={plan.PullDistance:F3} | skor={plan.Score:F1}");
            return true;
        }

        // Fail streak'te demote zaten hariç; başka coin advance varsa onu al.
        // Setup yalnızca advance yoksa veya demote hâlâ sızdıysa.
        bool demotedStillSelected = hasAdvance
                                    && demoteShooter != null
                                    && bestAdvance.Coin == demoteShooter
                                    && bestAdvance.Kind != ShotKind.GoalFinish;
        if (hasSetup && (demotedStillSelected || (excludeDemoted && !hasAdvance)))
        {
            plan = bestSetup;
            Debug.Log($"[Bot] SETUP-ESCAPE | {plan.Coin.name} | {plan.Kind} | skor={plan.Score:F2} | " +
                      $"fails={consecutiveInvalidGateFails}");
            return true;
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

    /// <summary>
    /// Fail streak sonrası demote edilen para hariç, kapıya yetişebilen herhangi bir başka para ile atış zorla.
    /// </summary>
    static void TryBuildForcedAlternateShot(
        TeamRoundState        state,
        bool                  isResolvingMove,
        Vector3               goalFlat,
        float                 gateMargin,
        OpponentBotDifficulty difficulty,
        CoinIdentity          demoteShooter,
        ref ShotPlan          bestAdvance,
        ref float             bestAdvanceScore,
        ref bool              hasAdvance,
        ref ShotPlan          bestSetup,
        ref float             bestSetupScore,
        ref bool              hasSetup)
    {
        ShotPlan best = default;
        float bestScore = float.MinValue;
        bool found = false;

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity shooter = state.Coins[i];
            if (shooter == null || shooter == demoteShooter)
            {
                continue;
            }

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
            if (!CanReachGateAtMaxPull(origin, gateA, gateB, dc, gateMargin, difficulty))
            {
                continue;
            }

            Vector3 gateMid = GateMidpoint(gateA, gateB);
            Vector3 dir = SafeDir(gateMid - origin);
            float pull = ClampStrengthPull(dc, difficulty, StrengthMaxPull(dc, difficulty));
            float travel = EffectiveTravelDistance(pull);
            if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
            {
                continue;
            }

            float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
            float score = 40f + advance * 3f + ScoreRearShooter(origin, gateMid, goalFlat) * 0.25f;
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            found = true;
            best = BuildPlan(shooter, dir, pull, ShotKind.Advance, score, advance);
        }

        if (!found)
        {
            Debug.Log("[Bot] FAIL-ESCAPE | alternatif coin bulunamadı");
            return;
        }

        Debug.Log($"[Bot] FAIL-ESCAPE | zorunlu alternatif → {best.Coin.name} | skor={best.Score:F1}");
        TryAdoptAdvance(
            best.Coin, best.Direction, best.PullDistance, best.Kind, best.Score, best.GoalAdvanceMeters,
            ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);

        // Setup listesine de koy ki escape yolu boş kalmasın.
        TryAdoptSetup(
            best.Coin, best.Direction, best.PullDistance, ShotKind.SetupEnablePass, best.Score + 5f, best.GoalAdvanceMeters,
            ref bestSetup, ref bestSetupScore, ref hasSetup);
    }

    static void EvaluateShooterCandidates(
        CoinIdentity          shooter,
        CoinIdentity          gateA,
        CoinIdentity          gateB,
        Vector3               goalFlat,
        float                 gateMargin,
        float                 coinBlockRadius,
        OpponentBotDifficulty difficulty,
        CoinIdentity          demoteShooter,
        int                   consecutiveInvalidGateFails,
        bool                  excludeDemoted,
        ref ShotPlan          bestAdvance,
        ref float             bestAdvanceScore,
        ref bool              hasAdvance,
        ref ShotPlan          bestSetup,
        ref float             bestSetupScore,
        ref bool              hasSetup)
    {
        if (excludeDemoted && demoteShooter != null && shooter == demoteShooter)
        {
            return;
        }

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
        // Soft demote artık yedek: hard exclude asıl koruma.
        bool demoteThisShooter = !excludeDemoted
                                 && demoteShooter != null
                                 && shooter == demoteShooter
                                 && consecutiveInvalidGateFails >= 1;
        float demotePenalty = demoteThisShooter ? 25f : 0f;

        Vector3 gateDir = SafeDir(gateMid - origin);
        Vector3 goalDir = SafeDir(goalFlat - origin);
        float shooterRearScore = ScoreRearShooter(origin, gateMid, goalFlat);
        float distGate = Vector3.Distance(origin, gateMid);

        // Ulaşılamayan kapı adaylarını üretme — boşuna fail streak şişmesin.
        if (!CanReachGateAtMaxPull(origin, gateA, gateB, dc, gateMargin, difficulty))
        {
            return;
        }

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
            demotePenalty,
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
                                 && ShotReachesGoal(origin, dir, travel, goalFlat, distGoal)
                                 && IsClearShotLane(origin, goalFlat, shooter, Mathf.Max(coinBlockRadius, 0.10f), gateA, gateB);

                ShotKind kind = canFinish ? ShotKind.GoalFinish : ShotKind.Advance;
                float score = ScoreAdvancePlan(
                    origin, dir, goalFlat, gateWidth, advance, goalFocus, kind, shooterRearScore, blend);
                score -= demotePenalty;

                TryAdoptAdvance(shooter, dir, pull, kind, score, advance, ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);
            }
        }

        // ── Aday 2: kale bitirici (yakın mesafe) ──
        if (distGoal <= maxTravel - 0.08f
            && IsClearShotLane(origin, goalFlat, shooter, Mathf.Max(coinBlockRadius, 0.10f), gateA, gateB))
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

                if (!ShotReachesGoal(origin, dir, travel, goalFlat, distGoal))
                {
                    continue;
                }

                float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                float score = ScoreAdvancePlan(
                    origin, dir, goalFlat, gateWidth, advance, goalFocus, ShotKind.GoalFinish, shooterRearScore, blend)
                              + kGoalFinishBonus * goalFocus
                              - demotePenalty;
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

    enum SetupReason { NarrowGate, Blocker, PoorAngle, Fallback, EnablePass }

    /// <summary>
    /// Ulaşılabilir bir parayı fırlatıp, şu an kapıya yetişemeyen takım arkadaşının
    /// sonraki hamlede geçerli atış yapabileceği bir kapı/mesafe kurar.
    /// </summary>
    static void AddEnablePassSetupCandidates(
        TeamRoundState        state,
        bool                  isResolvingMove,
        Vector3               goalFlat,
        float                 gateMargin,
        OpponentBotDifficulty difficulty,
        CoinIdentity          demoteShooter,
        int                   consecutiveInvalidGateFails,
        bool                  excludeDemoted,
        ref ShotPlan          bestSetup,
        ref float             bestSetupScore,
        ref bool              hasSetup)
    {
        for (int m = 0; m < state.Coins.Count; m++)
        {
            CoinIdentity mover = state.Coins[m];
            if (!TeamRulesService.CanSelectCoin(state, mover, isResolvingMove, mover.IsPassive))
            {
                continue;
            }

            if (excludeDemoted && demoteShooter != null && mover == demoteShooter)
            {
                continue;
            }

            if (!TeamRulesService.TryGetGateCoins(state, mover, out CoinIdentity moverGateA, out CoinIdentity moverGateB))
            {
                continue;
            }

            CoinDragController moverDc = mover.DragController;
            if (moverDc == null)
            {
                continue;
            }

            Vector3 moverOrigin = Flat(mover.transform.position);
            if (!CanReachGateAtMaxPull(moverOrigin, moverGateA, moverGateB, moverDc, gateMargin, difficulty))
            {
                continue;
            }

            Vector3 moverGateMid = GateMidpoint(moverGateA, moverGateB);
            Vector3 moverGateDir = SafeDir(moverGateMid - moverOrigin);
            float moverGateWidth = GateWidth(moverGateA, moverGateB);
            float distGate = Vector3.Distance(moverOrigin, moverGateMid);
            float gatePassPull = PullForGatePass(moverDc, distGate, moverGateWidth);
            float maxPull = moverDc.MaxPullDistance;

            float[] pulls = { maxPull, gatePassPull };
            float[] yaws = { 0f, -4f, 4f, -8f, 8f };

            for (int yi = 0; yi < yaws.Length; yi++)
            {
                Vector3 dir = (Quaternion.Euler(0f, yaws[yi], 0f) * moverGateDir).normalized;
                for (int pi = 0; pi < pulls.Length; pi++)
                {
                    float pull = Mathf.Clamp(pulls[pi], moverDc.MinPullDistance, moverDc.MaxPullDistance);
                    float travel = EffectiveTravelDistance(pull);
                    float required = ComputeGatePassTravelTarget(distGate, moverGateWidth);
                    if (travel < required * 0.92f)
                    {
                        continue;
                    }

                    if (!WillPassGate(moverOrigin, dir, travel, moverGateA, moverGateB, gateMargin))
                    {
                        continue;
                    }

                    Vector3 land = moverOrigin + dir * travel;
                    float advance = EstimateGoalAdvance(moverOrigin, dir, travel, goalFlat);

                    for (int t = 0; t < state.Coins.Count; t++)
                    {
                        CoinIdentity future = state.Coins[t];
                        if (future == null || future == mover)
                        {
                            continue;
                        }

                        CoinDragController futureDc = future.DragController;
                        if (futureDc == null)
                        {
                            continue;
                        }

                        if (!TryGetTeammateGatePositions(
                                state, future, mover, land, out Vector3 virtualA, out Vector3 virtualB))
                        {
                            continue;
                        }

                        Vector3 futureOrigin = Flat(future.transform.position);
                        if (!TryGetTeammateGatePositions(
                                state, future, null, default, out Vector3 curA, out Vector3 curB))
                        {
                            continue;
                        }

                        bool canReachNow = CanReachGateAtPull(
                            futureOrigin, curA, curB, futureDc.MaxPullDistance, gateMargin);
                        bool canReachAfter = CanReachGateAtPull(
                            futureOrigin, virtualA, virtualB, futureDc.MaxPullDistance, gateMargin);

                        if (!canReachAfter)
                        {
                            continue;
                        }

                        // Sadece şu an yetişemeyen (veya demote edilen) para için setup değerli.
                        bool helpsStuck = !canReachNow || future == demoteShooter;
                        if (!helpsStuck)
                        {
                            continue;
                        }

                        float curDist = Vector3.Distance(futureOrigin, (curA + curB) * 0.5f);
                        float newDist = Vector3.Distance(futureOrigin, (virtualA + virtualB) * 0.5f);
                        float distImprove = curDist - newDist;
                        float widthImprove = Vector3.Distance(virtualA, virtualB) - Vector3.Distance(curA, curB);

                        float score = ScoreSetupPlan(
                            moverOrigin, dir, travel, moverGateMid, goalFlat, moverGateWidth, advance, SetupReason.EnablePass);
                        score += 6.5f;
                        if (!canReachNow && canReachAfter)
                        {
                            score += 8f;
                        }

                        score += Mathf.Max(0f, distImprove) * 3.5f;
                        score += Mathf.Max(0f, widthImprove) * 2f;
                        score += FutureAttackAlignment(futureOrigin, (virtualA + virtualB) * 0.5f, goalFlat) * 2f;

                        if (future == demoteShooter)
                        {
                            score += 4f + consecutiveInvalidGateFails * 2f;
                        }

                        if (consecutiveInvalidGateFails >= kInvalidGateFailEscape)
                        {
                            score += 5f;
                        }

                        TryAdoptSetup(
                            mover, dir, pull, ShotKind.SetupEnablePass, score, advance,
                            ref bestSetup, ref bestSetupScore, ref hasSetup);
                    }
                }
            }
        }
    }

    static bool TryGetTeammateGatePositions(
        TeamRoundState state,
        CoinIdentity   shooter,
        CoinIdentity   movedCoin,
        Vector3        movedLand,
        out Vector3    gateAPos,
        out Vector3    gateBPos)
    {
        gateAPos = default;
        gateBPos = default;
        int found = 0;

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity coin = state.Coins[i];
            if (coin == null || coin == shooter)
            {
                continue;
            }

            Vector3 pos = (movedCoin != null && coin == movedCoin)
                ? movedLand
                : Flat(coin.transform.position);

            if (found == 0)
            {
                gateAPos = pos;
                found = 1;
            }
            else
            {
                gateBPos = pos;
                return true;
            }
        }

        return false;
    }

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
        float        demotePenalty,
        ref ShotPlan bestAdvance,
        ref float    bestAdvanceScore,
        ref bool     hasAdvance)
    {
        float maxPull = dc.MaxPullDistance;
        float gatePassPull = PullForGatePass(dc, distGate, gateWidth);

        // Kapı geçişi: önce tam güç, sonra hesaplanan minimum
        TryGatePassDirection(
            shooter, gateA, gateB, origin, gateMid, gateDir, goalFlat, gateWidth, distGoal,
            gateMargin, coinBlockRadius, dc, goalFocus, shooterRearScore, maxPull, gateDir, demotePenalty,
            ref bestAdvance, ref bestAdvanceScore, ref hasAdvance);

        if (gatePassPull < maxPull - 0.008f)
        {
            TryGatePassDirection(
                shooter, gateA, gateB, origin, gateMid, gateDir, goalFlat, gateWidth, distGoal,
                gateMargin, coinBlockRadius, dc, goalFocus, shooterRearScore, gatePassPull, gateDir, demotePenalty,
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
                gateMargin, coinBlockRadius, dc, goalFocus, shooterRearScore, maxPull, dir, demotePenalty,
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
        float        demotePenalty,
        ref ShotPlan bestAdvance,
        ref float    bestAdvanceScore,
        ref bool     hasAdvance)
    {
        float travel = EffectiveTravelDistance(pull);
        float distGate = Vector3.Distance(origin, gateMid);
        float requiredTravel = ComputeGatePassTravelTarget(distGate, gateWidth);
        // Max güç dahil: güvenli travel ile yetişmiyorsa aday değil.
        if (travel * kReachTravelSafety < requiredTravel)
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
                         && ShotReachesGoal(origin, dir, travel, goalFlat, distGoal)
                         && IsClearShotLane(origin, goalFlat, shooter, Mathf.Max(coinBlockRadius, 0.10f), gateA, gateB);

        ShotKind kind = canFinish ? ShotKind.GoalFinish : ShotKind.Advance;
        float gateAlign = Vector3.Dot(dir, gateDirForScore);
        float score = ScoreAdvancePlan(
            origin, dir, goalFlat, gateWidth, advance, goalFocus, kind, shooterRearScore, 0f);
        score += kGatePassScoreBonus;
        score += Mathf.Max(0f, gateAlign) * 1.5f;
        score += shooterRearScore;
        score += (pull / dc.MaxPullDistance) * 2f;
        score -= demotePenalty;

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
            case SetupReason.EnablePass:
                score += 3.5f;
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
        if (gateA == null || gateB == null)
        {
            return false;
        }

        return WillPassGatePositions(
            origin,
            direction,
            travelDistance,
            Flat(gateA.transform.position),
            Flat(gateB.transform.position),
            gateMargin);
    }

    static bool WillPassGatePositions(
        Vector3 origin,
        Vector3 direction,
        float   travelDistance,
        Vector3 gateAPos,
        Vector3 gateBPos,
        float   gateMargin)
    {
        if (direction.sqrMagnitude < 0.0001f || travelDistance <= 0.01f)
        {
            return false;
        }

        Vector3 end = origin + direction.normalized * travelDistance;
        return PassBetweenValidator.DidPassBetween(
            origin,
            end,
            gateAPos,
            gateBPos,
            gateMargin);
    }

    static bool CanReachGateAtMaxPull(
        Vector3               origin,
        CoinIdentity          gateA,
        CoinIdentity          gateB,
        CoinDragController    dc,
        float                 gateMargin,
        OpponentBotDifficulty difficulty)
    {
        if (dc == null || gateA == null || gateB == null)
        {
            return false;
        }

        float maxPull = ClampStrengthPull(dc, difficulty, StrengthMaxPull(dc, difficulty));
        Vector3 gateAPos = Flat(gateA.transform.position);
        Vector3 gateBPos = Flat(gateB.transform.position);
        float travel = EffectiveTravelDistance(maxPull) * kReachTravelSafety;
        Vector3 gateMid = (gateAPos + gateBPos) * 0.5f;
        float gateWidth = Vector3.Distance(gateAPos, gateBPos);
        float required = ComputeGatePassTravelTarget(Vector3.Distance(origin, gateMid), gateWidth);
        if (travel < required)
        {
            return false;
        }

        Vector3 baseDir = SafeDir(gateMid - origin);
        float[] yaws = { 0f, -4f, 4f, -8f, 8f };
        for (int i = 0; i < yaws.Length; i++)
        {
            Vector3 dir = (Quaternion.Euler(0f, yaws[i], 0f) * baseDir).normalized;
            if (WillPassGatePositions(origin, dir, travel, gateAPos, gateBPos, gateMargin))
            {
                return true;
            }
        }

        return false;
    }

    static bool CanReachGateAtPull(
        Vector3 origin,
        Vector3 gateAPos,
        Vector3 gateBPos,
        float   pull,
        float   gateMargin)
    {
        float travel = EffectiveTravelDistance(pull) * kReachTravelSafety;
        Vector3 gateMid = (gateAPos + gateBPos) * 0.5f;
        float gateWidth = Vector3.Distance(gateAPos, gateBPos);
        float required = ComputeGatePassTravelTarget(Vector3.Distance(origin, gateMid), gateWidth);
        if (travel < required)
        {
            return false;
        }

        Vector3 baseDir = SafeDir(gateMid - origin);
        float[] yaws = { 0f, -4f, 4f, -8f, 8f };
        for (int i = 0; i < yaws.Length; i++)
        {
            Vector3 dir = (Quaternion.Euler(0f, yaws[i], 0f) * baseDir).normalized;
            if (WillPassGatePositions(origin, dir, travel, gateAPos, gateBPos, gateMargin))
            {
                return true;
            }
        }

        return false;
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
        float blockRadius,
        CoinIdentity ignoreA = null,
        CoinIdentity ignoreB = null)
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
            CoinIdentity other = all[i];
            if (other == null || other == shooter || other == ignoreA || other == ignoreB)
            {
                continue;
            }

            Vector3 toBlocker = Flat(other.transform.position) - origin;
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

    static ShotPlan FinalizePlanForDifficulty(ShotPlan plan, OpponentBotDifficulty difficulty, int shotNumber)
    {
        if (plan.Coin == null || plan.Coin.DragController == null)
        {
            return plan;
        }

        CoinDragController dc = plan.Coin.DragController;
        plan.PullDistance = ClampStrengthPull(dc, difficulty, plan.PullDistance);

        switch (plan.Kind)
        {
            case ShotKind.GoalFinish:
                plan.PullDistance = ClampStrengthPull(
                    dc,
                    difficulty,
                    plan.PullDistance * difficulty.GoalFinishPullScale);
                plan = ApplyNoise(plan, difficulty, difficulty.GoalFinishNoiseScale);
                break;

            case ShotKind.MandatoryGatePass:
                plan.PullDistance = ClampStrengthPull(
                    dc,
                    difficulty,
                    StrengthMaxPull(dc, difficulty) * difficulty.GatePassPullScale);
                plan = ApplyNoise(plan, difficulty, 0.35f);
                break;

            case ShotKind.Opening:
                plan = ApplyNoise(plan, difficulty, Mathf.Lerp(1.25f, 0.45f, difficulty.RuleCompliance));
                break;

            default:
                plan = ApplyNoise(plan, difficulty);
                break;
        }

        return plan;
    }

    static float StrengthMaxPull(CoinDragController dc, OpponentBotDifficulty difficulty)
    {
        return dc.MinPullDistance + (dc.MaxPullDistance - dc.MinPullDistance) * difficulty.MaxPullScale;
    }

    static float ClampStrengthPull(CoinDragController dc, OpponentBotDifficulty difficulty, float pull)
    {
        return Mathf.Clamp(pull, dc.MinPullDistance, StrengthMaxPull(dc, difficulty));
    }

    static ShotPlan ApplyNoise(ShotPlan plan, OpponentBotDifficulty difficulty, float noiseScale = 1f)
    {
        if (plan.Coin == null || plan.Coin.DragController == null)
        {
            return plan;
        }

        CoinDragController dc = plan.Coin.DragController;
        float complianceScale = Mathf.Lerp(1f, 0.15f, difficulty.RuleCompliance);
        float scaledNoise = Mathf.Max(0.01f, noiseScale);

        float yaw = Random.Range(-difficulty.AimNoiseDegrees, difficulty.AimNoiseDegrees)
                    * complianceScale
                    * scaledNoise;
        plan.Direction = (Quaternion.Euler(0f, yaw, 0f) * plan.Direction).normalized;

        float pullNoise = Random.Range(-difficulty.PullNoise, difficulty.PullNoise)
                          * complianceScale
                          * scaledNoise;
        plan.PullDistance = ClampStrengthPull(
            dc,
            difficulty,
            plan.PullDistance + pullNoise);

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
        GoalZone zone = GoalZone.FindPlayerGoalArea();
        return zone != null ? zone.transform.position : Vector3.zero;
    }
}
