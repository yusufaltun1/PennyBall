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
    /// <summary>Aynı coin ile 2 geçersiz kapı denemesinden sonra alternatif zorunlu.</summary>
    const int   kInvalidGateFailEscape = 2;
    /// <summary>Planlama mesafesini fizikten biraz daha kötümser tut (iyimser yetişme → sonsuz retry).</summary>
    const float kReachTravelSafety   = 0.88f;
    const float kGoalMouthPostInset  = 0.10f;
    const float kInGoalCoinRadius    = 0.075f;

    static readonly float[] kGoalBlends       = { 0f, 0.08f, 0.18f, 0.30f, 0.45f };
    static readonly float[] kPullRatios         = { 0.55f, 0.70f, 0.85f, 0.94f, 1.00f };
    static readonly float[] kGoalFinishBlends   = { 0f, 0.15f, 0.30f, 0.50f, 0.70f, 0.85f, 1.00f };
    static readonly float[] kGoalFinishPulls    = { 1.00f };
    static readonly float[] kGoalFinishGateTs   = { 0.15f, 0.30f, 0.45f, 0.50f, 0.55f, 0.70f, 0.85f };
    static readonly float[] kGoalFinishYaws     = { 0f, -2f, 2f, -4f, 4f, -7f, 7f, -11f, 11f, -16f, 16f };
    static readonly Vector3[] MouthAimScratch  = new Vector3[28];
    static readonly float[] kSetupPullRatios    = { 0.35f, 0.45f, 0.55f, 0.65f, 0.75f };
    static readonly float[] kEnablePassYaws     = { 0f, -6f, 6f, -12f, 12f, -20f, 20f, -30f, 30f };
    const float kGatePassScoreBonus      = 4f;
    const float kRearShooterBonus        = 2.5f;

    // Son geçerli atış — setup ping-pong (ileri-geri loop) kırıcı.
    static bool s_hasLastShotMemory;
    static int s_lastShotCoinId;
    static Vector3 s_lastShotFrom;
    static Vector3 s_lastShotTo;

    public static void RememberValidShot(CoinIdentity coin, Vector3 from, Vector3 to)
    {
        if (coin == null)
        {
            return;
        }

        s_hasLastShotMemory = true;
        s_lastShotCoinId = coin.GetInstanceID();
        s_lastShotFrom = Flat(from);
        s_lastShotTo = Flat(to);
    }

    public static void ClearShotMemory()
    {
        s_hasLastShotMemory = false;
        s_lastShotCoinId = 0;
    }

    static float OscillationPenalty(CoinIdentity coin, Vector3 origin, Vector3 land)
    {
        if (!s_hasLastShotMemory || coin == null || coin.GetInstanceID() != s_lastShotCoinId)
        {
            return 0f;
        }

        float penalty = 0f;
        Vector3 prevDir = SafeDir(s_lastShotTo - s_lastShotFrom);
        Vector3 nextDir = SafeDir(land - origin);
        if (Vector3.Dot(prevDir, nextDir) < -0.2f)
        {
            penalty += 60f;
        }

        // Önceki çıkış noktasına geri dönme.
        if (Vector3.Distance(land, s_lastShotFrom) < 0.22f)
        {
            penalty += 50f;
        }

        return penalty;
    }

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
                    demoteShooter,
                    consecutiveInvalidGateFails,
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

        // Fail sonrası aynı paraya yapışmayı kes (GoalFinish dahil — demote muafiyeti yok).
        if (ok
            && demoteShooter != null
            && consecutiveInvalidGateFails >= kInvalidGateFailEscape
            && plan.Coin == demoteShooter)
        {
            Debug.Log($"[Bot] FAIL-ESCAPE hard-block | {demoteShooter.name} tekrar seçildi ({plan.Kind}), alternatif aranıyor");
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
        CoinIdentity          demoteShooter,
        int                   consecutiveInvalidGateFails,
        out ShotPlan          plan)
    {
        plan = default;
        Vector3 goalFlat = Flat(goal);
        float goalFocus = difficulty.GoalFocus;
        float laneRadius = Mathf.Max(coinBlockRadius, 0.10f);
        bool excludeDemoted = demoteShooter != null
                              && consecutiveInvalidGateFails >= kInvalidGateFailEscape;

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

            // Kapı fail sonrası aynı parayla "gol fırsatı" diye tekrar denemeyi kes.
            if (excludeDemoted && shooter == demoteShooter)
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

            // Zaten kale alanında overlap olan para gol sayılmaz; fırsat gibi seçme.
            GoalZone playerGoal = GoalZone.FindPlayerGoalArea();
            if (playerGoal != null && playerGoal.OverlapsCoin(shooter))
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
            float goalPull = Mathf.Clamp(PullForDistance(dc, distGoal + 0.35f), dc.MinPullDistance, maxPull);
            // Kaleye yakın bariz fırsatlarda skor bonus — başka coine kaçmayı kes.
            float proximityBonus = Mathf.Clamp01(1.2f - distGoal) * 40f;

            Vector3 gateAPos = Flat(gateA.transform.position);
            Vector3 gateBPos = Flat(gateB.transform.position);

            // 0) Kale ağzı nişan noktaları: direk–coin aralığı dahil (insan gibi boşluktan sok).
            int mouthCount = CollectGoalMouthAimPoints(
                playerGoal, origin, gateA, gateB, MouthAimScratch);
            for (int mi = 0; mi < mouthCount; mi++)
            {
                Vector3 mouthTarget = MouthAimScratch[mi];
                // Ağza giden koridorda takım arkadaşını ENGEL say (içinden geçme).
                if (!IsClearShotLane(origin, mouthTarget, shooter, laneRadius * 0.85f))
                {
                    continue;
                }

                Vector3 mouthAim = SafeDir(mouthTarget - origin);
                TryGoalFinishAimVariants(
                    shooter, gateA, gateB, origin, goalFlat, gateWidth, distGoal,
                    gateMargin, dc, goalFocus, shooterRear, proximityBonus + 25f,
                    mouthAim, minGatePull, goalPull, maxPull,
                    1f, mouthTarget, preferLowYaw: true,
                    ref best, ref bestScore, ref found);
            }

            // 1) Kapı segmenti üzerinden kale yönüne örnekle.
            for (int ti = 0; ti < kGoalFinishGateTs.Length; ti++)
            {
                Vector3 gatePoint = Vector3.Lerp(gateAPos, gateBPos, kGoalFinishGateTs[ti]);
                Vector3 throughGate = SafeDir(gatePoint - origin);

                for (int bi = 0; bi < 4; bi++)
                {
                    float goalBias = bi * 0.28f;
                    Vector3 baseAim = goalBias < 0.01f
                        ? throughGate
                        : Vector3.Lerp(throughGate, goalDir, goalBias).normalized;

                    TryGoalFinishAimVariants(
                        shooter, gateA, gateB, origin, goalFlat, gateWidth, distGoal,
                        gateMargin, dc, goalFocus, shooterRear, proximityBonus,
                        baseAim, minGatePull, goalPull, maxPull,
                        goalBias, default, preferLowYaw: false,
                        ref best, ref bestScore, ref found);
                }
            }

            // 2) Klasik gate→goal blend + geniş yaw (yedek).
            for (int b = kGoalFinishBlends.Length - 1; b >= 0; b--)
            {
                float blend = kGoalFinishBlends[b];
                Vector3 dir = blend < 0.01f
                    ? gateDir
                    : Vector3.Lerp(gateDir, goalDir, blend).normalized;

                TryGoalFinishAimVariants(
                    shooter, gateA, gateB, origin, goalFlat, gateWidth, distGoal,
                    gateMargin, dc, goalFocus, shooterRear, proximityBonus,
                    dir, minGatePull, goalPull, maxPull,
                    blend, default, preferLowYaw: false,
                    ref best, ref bestScore, ref found);
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
    /// Kale ağzı boyunca nişan noktaları üretir.
    /// Kale içinde takım arkadaşı varsa direk ile o coin arasındaki boşlukları da ekler.
    /// </summary>
    static int CollectGoalMouthAimPoints(
        GoalZone zone,
        Vector3 origin,
        CoinIdentity gateA,
        CoinIdentity gateB,
        Vector3[] buffer)
    {
        if (zone == null || buffer == null || buffer.Length == 0)
        {
            return 0;
        }

        BoxCollider box = zone.GetComponent<BoxCollider>();
        if (box == null)
        {
            return 0;
        }

        Bounds b = box.bounds;
        bool approachFromPosZ = origin.z >= b.center.z;
        float mouthZ = approachFromPosZ ? b.max.z : b.min.z;
        // Genişlik ekseni: X daha genişse X, değilse dünya X varsay.
        float minX = b.min.x + b.size.x * kGoalMouthPostInset;
        float maxX = b.max.x - b.size.x * kGoalMouthPostInset;
        if (maxX <= minX)
        {
            minX = b.min.x;
            maxX = b.max.x;
        }

        int count = 0;
        void AddPoint(float x)
        {
            if (count >= buffer.Length)
            {
                return;
            }

            buffer[count++] = new Vector3(x, 0f, mouthZ);
        }

        // Ağzı eşit aralıkla tara (direklerden içeride).
        for (int i = 0; i <= 8; i++)
        {
            float t = i / 8f;
            AddPoint(Mathf.Lerp(minX, maxX, t));
        }

        CoinIdentity inGoal = null;
        if (zone.OverlapsCoin(gateA))
        {
            inGoal = gateA;
        }
        else if (zone.OverlapsCoin(gateB))
        {
            inGoal = gateB;
        }

        if (inGoal == null)
        {
            return count;
        }

        // Direk ↔ kale içi coin boşluklarının ortası — insan nişanı.
        Vector3 coinPos = Flat(inGoal.transform.position);
        float leftEdge = coinPos.x - kInGoalCoinRadius;
        float rightEdge = coinPos.x + kInGoalCoinRadius;

        if (leftEdge > minX + 0.02f)
        {
            AddPoint((minX + leftEdge) * 0.5f);
            AddPoint(Mathf.Lerp(minX, leftEdge, 0.35f));
            AddPoint(Mathf.Lerp(minX, leftEdge, 0.65f));
        }

        if (rightEdge < maxX - 0.02f)
        {
            AddPoint((maxX + rightEdge) * 0.5f);
            AddPoint(Mathf.Lerp(rightEdge, maxX, 0.35f));
            AddPoint(Mathf.Lerp(rightEdge, maxX, 0.65f));
        }

        return count;
    }

    static void TryGoalFinishAimVariants(
        CoinIdentity shooter,
        CoinIdentity gateA,
        CoinIdentity gateB,
        Vector3 origin,
        Vector3 goalFlat,
        float gateWidth,
        float distGoal,
        float gateMargin,
        CoinDragController dc,
        float goalFocus,
        float shooterRear,
        float proximityBonus,
        Vector3 baseAim,
        float minGatePull,
        float goalPull,
        float maxPull,
        float blendForScore,
        Vector3 mouthTarget,
        bool preferLowYaw,
        ref ShotPlan best,
        ref float bestScore,
        ref bool found)
    {
        Vector3 goalDir = SafeDir(goalFlat - origin);
        bool hasMouthTarget = mouthTarget.sqrMagnitude > 0.0001f;
        int yawCount = preferLowYaw ? 5 : kGoalFinishYaws.Length; // 0,±2,±4

        for (int yi = 0; yi < yawCount; yi++)
        {
            Vector3 aim = (Quaternion.Euler(0f, kGoalFinishYaws[yi], 0f) * baseAim).normalized;

            for (int p = 0; p < kGoalFinishPulls.Length; p++)
            {
                // Bariz gol: her zaman strength max güç — çizgide bırakma.
                float pull = maxPull;
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

                // Ağza nişanda: varış ağza yakın olsun, direğe sürtmesin.
                Vector3 land = origin + aim * travel;

                // Takım arkadaşı / rakip coinin içinden geçme — aradan (boşluktan) nişan.
                // Yarıçap düşük tutulur: kapı ortasından geçişe izin, coinin üstünden geçişi kes.
                if (IsPathBlocked(origin, land, shooter, 0.055f))
                {
                    continue;
                }

                if (hasMouthTarget)
                {
                    float mouthMiss = Vector3.Distance(
                        new Vector3(land.x, 0f, land.z),
                        new Vector3(mouthTarget.x, 0f, mouthTarget.z));
                    if (mouthMiss > 0.55f && Vector3.Distance(land, goalFlat) > 0.45f)
                    {
                        continue;
                    }
                }

                float advance = EstimateGoalAdvance(origin, aim, travel, goalFlat);
                float score = ScoreAdvancePlan(
                    origin, aim, goalFlat, gateWidth, advance, goalFocus,
                    ShotKind.GoalFinish, shooterRear, blendForScore);
                score += kGoalFinishPriority;
                score += proximityBonus;
                score += Mathf.Max(0f, Vector3.Dot(aim, goalDir)) * 6f;
                score += blendForScore * 3f;
                score += (advance - distGoal) * 8f;
                score += (1f - Mathf.Clamp01(Vector3.Distance(land, goalFlat) / 0.40f)) * 8f;
                // Kaleyi geçip içeri gömülmeyi ödüllendir (çizgide kalmasın).
                Vector3 goalAxis = SafeDir(goalFlat - origin);
                float pastGoal = Vector3.Dot(land - goalFlat, goalAxis);
                score += Mathf.Clamp(pastGoal, 0f, 0.6f) * 14f;

                if (hasMouthTarget)
                {
                    float alignMouth = Vector3.Dot(aim, SafeDir(mouthTarget - origin));
                    score += Mathf.Max(0f, alignMouth) * 12f;
                    // Küçük yaw = daha temiz ağzı nişanı.
                    score += (5 - Mathf.Min(yi, 4)) * 1.5f;
                }

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
        // Kaleye yakın bitişlerde daha toleranslı.
        float nearGoalSlack = distGoal < 0.85f ? 0.38f : 0.28f;
        if (landToGoal <= nearGoalSlack)
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
        float lateralSlack = distGoal < 0.85f ? 0.42f : 0.30f;
        return landProj >= goalDist - 0.15f && lateral <= lateralSlack;
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

            // Aynı coin 2 invalid sonrası zorunlu kapıdan tamamen çıkar.
            if (demoteShooter != null
                && shooter == demoteShooter
                && consecutiveInvalidGateFails >= kInvalidGateFailEscape)
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
            // İlk invalid sonrası aynı coini zorunlu kapıda cezalandır (2. invalid'de hard exclude).
            if (demoteShooter != null
                && shooter == demoteShooter
                && consecutiveInvalidGateFails >= 1)
            {
                rear -= 35f;
            }
            // Zorunlu kapıda marjinal menzil adaylarını ele: planlama iyimser,
            // fizik/noise sonrası invalid-loop üretmesin.
            float maxTravel = EffectiveTravelDistance(StrengthMaxPull(dc, difficulty));
            float requiredTravel = ComputeGatePassTravelTarget(
                Vector3.Distance(origin, gateMid), gateWidth);
            if (maxTravel <= 0.001f || requiredTravel > maxTravel * 0.85f)
            {
                continue;
            }

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

        if (plan.Kind == ShotKind.MandatoryGatePass)
        {
            plan.Direction = gateDir;
            plan.PullDistance = ClampStrengthPull(
                dc,
                difficulty,
                StrengthMaxPull(dc, difficulty) * difficulty.GatePassPullScale);
        }
        else if (plan.Kind == ShotKind.Advance && gateDot > 0.80f)
        {
            // Yönü kapıya kilitle ama gücü şişirme (setup loop / aşırı geri kaçış önlemi).
            plan.Direction = gateDir;
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
            float pull = ClampStrengthPull(dc, difficulty, StrengthMaxPull(dc, difficulty) * 0.65f);
            float travel = EffectiveTravelDistance(pull);
            if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
            {
                continue;
            }

            float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
            Vector3 land = origin + dir * travel;
            float score = 40f + advance * 3f + ScoreRearShooter(origin, gateMid, goalFlat) * 0.25f;
            score -= OscillationPenalty(shooter, origin, land);
            score -= Mathf.Max(0f, Vector3.Distance(land, goalFlat) - Vector3.Distance(origin, goalFlat)) * 12f;
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
                if (kind == ShotKind.GoalFinish)
                {
                    pull = StrengthMaxPull(dc, difficulty);
                    travel = EffectiveTravelDistance(pull);
                    if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin)
                        || !ShotReachesGoal(origin, dir, travel, goalFlat, distGoal))
                    {
                        continue;
                    }

                    advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
                }

                float score = ScoreAdvancePlan(
                    origin, dir, goalFlat, gateWidth, advance, goalFocus, kind, shooterRearScore, blend);
                score -= demotePenalty;
                score -= OscillationPenalty(shooter, origin, origin + dir * travel);

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
                float pull = StrengthMaxPull(dc, difficulty);
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

        // ── Setup: dar kapı — paraları ayır (ılımlı güç) ──
        if (gateWidth < kNarrowGateWidth)
        {
            TryAddModerateSetupShots(
                shooter, gateA, gateB, origin, gateDir, gateMid, goalFlat, gateWidth, gateMargin, dc,
                ShotKind.SetupSeparate, SetupReason.NarrowGate, 0f,
                ref bestSetup, ref bestSetupScore, ref hasSetup);
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
            TryAddModerateSetupShots(
                shooter, gateA, gateB, origin, dir, gateMid, goalFlat, gateWidth, gateMargin, dc,
                ShotKind.SetupClearBlocker, SetupReason.Blocker, 0.6f * goalFocus,
                ref bestSetup, ref bestSetupScore, ref hasSetup);
        }

        // ── Setup: kötü gate açısı ──
        if (IsPoorGateAngle(origin, gateA, gateB, gateDir))
        {
            TryAddModerateSetupShots(
                shooter, gateA, gateB, origin, gateDir, gateMid, goalFlat, gateWidth, gateMargin, dc,
                ShotKind.SetupReposition, SetupReason.PoorAngle, 0f,
                ref bestSetup, ref bestSetupScore, ref hasSetup);
        }

        // ── Setup fallback: saf gate ortası (ılımlı güç) ──
        TryAddModerateSetupShots(
            shooter, gateA, gateB, origin, gateDir, gateMid, goalFlat, gateWidth, gateMargin, dc,
            ShotKind.Fallback, SetupReason.Fallback, 0f,
            ref bestSetup, ref bestSetupScore, ref hasSetup);
    }

    static void TryAddModerateSetupShots(
        CoinIdentity shooter,
        CoinIdentity gateA,
        CoinIdentity gateB,
        Vector3 origin,
        Vector3 dir,
        Vector3 gateMid,
        Vector3 goalFlat,
        float gateWidth,
        float gateMargin,
        CoinDragController dc,
        ShotKind kind,
        SetupReason reason,
        float extraScore,
        ref ShotPlan bestSetup,
        ref float bestSetupScore,
        ref bool hasSetup)
    {
        float minGatePull = PullForGatePass(dc, Vector3.Distance(origin, gateMid), gateWidth);
        for (int i = 0; i < kSetupPullRatios.Length; i++)
        {
            float pull = Mathf.Max(minGatePull * 0.85f, dc.MaxPullDistance * kSetupPullRatios[i]);
            pull = Mathf.Clamp(pull, dc.MinPullDistance, dc.MaxPullDistance * 0.78f);
            float travel = EffectiveTravelDistance(pull);
            if (!WillPassGate(origin, dir, travel, gateA, gateB, gateMargin))
            {
                continue;
            }

            Vector3 land = origin + dir.normalized * travel;
            float advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
            float setup = ScoreSetupPlan(origin, dir, travel, gateMid, goalFlat, gateWidth, advance, reason);
            setup += extraScore;
            setup -= OscillationPenalty(shooter, origin, land);
            // Gereksiz geriye kaçışı cezalandır.
            float retreat = Vector3.Distance(land, goalFlat) - Vector3.Distance(origin, goalFlat);
            setup -= Mathf.Max(0f, retreat) * 18f;
            // Daha kısa setup'ı tercih et (Frame 3 gibi).
            setup -= kSetupPullRatios[i] * 4f;

            TryAdoptSetup(shooter, dir, pull, kind, setup, advance, ref bestSetup, ref bestSetupScore, ref hasSetup);
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
            float maxPull = moverDc.MaxPullDistance * 0.78f;

            for (int yi = 0; yi < kEnablePassYaws.Length; yi++)
            {
                Vector3 dir = (Quaternion.Euler(0f, kEnablePassYaws[yi], 0f) * moverGateDir).normalized;
                for (int ri = 0; ri < kSetupPullRatios.Length; ri++)
                {
                    float pull = Mathf.Clamp(
                        Mathf.Max(gatePassPull * 0.75f, maxPull * kSetupPullRatios[ri] / 0.78f),
                        moverDc.MinPullDistance,
                        maxPull);
                    float travel = EffectiveTravelDistance(pull);
                    float required = ComputeGatePassTravelTarget(distGate, moverGateWidth);
                    if (travel < required * 0.90f)
                    {
                        continue;
                    }

                    if (!WillPassGate(moverOrigin, dir, travel, moverGateA, moverGateB, gateMargin))
                    {
                        continue;
                    }

                    Vector3 land = moverOrigin + dir * travel;
                    float advance = EstimateGoalAdvance(moverOrigin, dir, travel, goalFlat);
                    float retreat = Vector3.Distance(land, goalFlat) - Vector3.Distance(moverOrigin, goalFlat);

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

                        // "Az geri" tercih: fazla geriye / stuck coinin yanına yapışmayı cezalandır.
                        score -= Mathf.Max(0f, retreat) * 22f;
                        score -= kSetupPullRatios[ri] * 6f;
                        float landToStuck = Vector3.Distance(land, futureOrigin);
                        if (landToStuck < 0.28f)
                        {
                            score -= 40f;
                        }
                        else if (landToStuck < 0.40f)
                        {
                            score -= 15f;
                        }

                        // Kaleye daha yakın landing bonus (Frame 3 > Frame 2).
                        score -= Vector3.Distance(land, goalFlat) * 3.5f;
                        score -= OscillationPenalty(mover, moverOrigin, land);

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
        if (kind == ShotKind.GoalFinish)
        {
            pull = dc.MaxPullDistance; // TryGatePassDirection doesn't have difficulty; use dc max then Finalize clamps
            // Strength clamp happens in Finalize — burada da tam güç.
            pull = Mathf.Max(pull, dc.MaxPullDistance);
            travel = EffectiveTravelDistance(pull);
            if (travel * kReachTravelSafety < requiredTravel
                || !WillPassGate(origin, dir, travel, gateA, gateB, gateMargin)
                || !ShotReachesGoal(origin, dir, travel, goalFlat, distGoal))
            {
                return;
            }

            advance = EstimateGoalAdvance(origin, dir, travel, goalFlat);
            kind = ShotKind.GoalFinish;
        }

        float gateAlign = Vector3.Dot(dir, gateDirForScore);
        float score = ScoreAdvancePlan(
            origin, dir, goalFlat, gateWidth, advance, goalFocus, kind, shooterRearScore, 0f);
        score += kGatePassScoreBonus;
        score += Mathf.Max(0f, gateAlign) * 1.5f;
        score += shooterRearScore;
        score += (pull / dc.MaxPullDistance) * 2f;
        score -= demotePenalty;
        score -= OscillationPenalty(shooter, origin, origin + dir.normalized * travel);

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
                // Gol fırsatında tam güç — GoalFinishPullScale ile kısma.
                plan.PullDistance = StrengthMaxPull(dc, difficulty);
                // Kale nişanında noise direğe/coine sürtmesin — çok düşük tut.
                plan = ApplyNoise(
                    plan,
                    difficulty,
                    Mathf.Min(difficulty.GoalFinishNoiseScale, 0.04f));
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
