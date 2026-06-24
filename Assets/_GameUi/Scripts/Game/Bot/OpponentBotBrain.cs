using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rakip bot hamle seçimi — zorluk ve takım kuralları burada uygulanır.
/// </summary>
public static class OpponentBotBrain
{
    public struct ShotPlan
    {
        public CoinIdentity Coin;
        public Vector3 Direction;
        public float PullDistance;
        public bool RespectsRules;
    }

    // Fiziğe dayalı sabit: pull * 18 (forceMultiplier) * 0.4 (damping) ≈ 7.2
    // Yani 1 birim pull → ~7.2 birim durma mesafesi
    const float kDistancePerPull = 7.2f;

    public static bool TryChooseShot(
        TeamRoundState state,
        OpponentBotDifficulty difficulty,
        bool isResolvingMove,
        float gateMargin,
        out ShotPlan plan)
    {
        plan = default;

        Vector3 goalCenter = ResolvePlayerGoalCenter();
        if (goalCenter == Vector3.zero)
        {
            goalCenter = new Vector3(1.52f, 0.14f, 2.235f);
        }

        var candidates = new List<ShotPlan>(12);
        CollectCandidateShots(state, difficulty, isResolvingMove, gateMargin, goalCenter, candidates);

        if (candidates.Count == 0)
        {
            return false;
        }

        // Rule-respecting adaylar arasından kaleye en iyi hizalanmış olanı seç.
        ShotPlan bestPlan = default;
        float bestScore = float.MinValue;
        bool hasRuleRespecting = false;

        for (int i = 0; i < candidates.Count; i++)
        {
            ShotPlan candidate = candidates[i];
            float goalAlignment = ComputeGoalAlignment(candidate, goalCenter);

            if (candidate.RespectsRules)
            {
                if (!hasRuleRespecting || goalAlignment > bestScore)
                {
                    bestScore = goalAlignment;
                    bestPlan = candidate;
                    hasRuleRespecting = true;
                }
            }
            else if (!hasRuleRespecting && goalAlignment > bestScore)
            {
                bestScore = goalAlignment;
                bestPlan = candidate;
            }
        }

        plan = ApplyNoise(bestPlan, difficulty);
        return true;
    }

    static float ComputeGoalAlignment(in ShotPlan candidate, Vector3 goalCenter)
    {
        Vector3 toGoal = goalCenter - candidate.Coin.transform.position;
        toGoal.y = 0f;
        if (toGoal.sqrMagnitude < 0.0001f)
        {
            return 0f;
        }

        return Vector3.Dot(candidate.Direction, toGoal.normalized);
    }

    static void CollectCandidateShots(
        TeamRoundState state,
        OpponentBotDifficulty difficulty,
        bool isResolvingMove,
        float gateMargin,
        Vector3 goalCenter,
        List<ShotPlan> output)
    {
        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity coin = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, coin, isResolvingMove, coin.IsPassive))
            {
                continue;
            }

            Vector3 origin = coin.transform.position;
            bool isOpening = state.IsFirstMove;

            if (isOpening)
            {
                TryAddShot(state, coin, origin, goalCenter - origin, goalCenter, gateMargin, true, output);
                continue;
            }

            if (!TeamRulesService.TryGetGateCoins(state, coin, out CoinIdentity gateA, out CoinIdentity gateB))
            {
                continue;
            }

            Vector3 gateCenter = (gateA.transform.position + gateB.transform.position) * 0.5f;
            Vector3 throughGate = gateCenter - origin;
            throughGate.y = 0f;

            Vector3 toGoal = goalCenter - origin;
            toGoal.y = 0f;

            // Kapı merkezi + kale yönü karışımı
            Vector3 blended = Vector3.Lerp(throughGate.normalized, toGoal.normalized, difficulty.GoalFocus);
            TryAddShot(state, coin, origin, blended, goalCenter, gateMargin, false, output);

            // Sadece kapı yönü
            TryAddShot(state, coin, origin, throughGate, goalCenter, gateMargin, false, output);

            // Sadece kale yönü (kural ihlal edebilir ama alternatif)
            TryAddShot(state, coin, origin, toGoal, goalCenter, gateMargin, false, output);
        }
    }

    static void TryAddShot(
        TeamRoundState state,
        CoinIdentity coin,
        Vector3 origin,
        Vector3 direction,
        Vector3 goalCenter,
        float gateMargin,
        bool isOpening,
        List<ShotPlan> output)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        direction = direction.normalized;

        // Kural doğrulaması için uzun sabit yol — yön kontrolü, fiziksel erişim değil
        var validationPath = new List<Vector3> { origin, origin + direction * 5f };
        bool respectsRules = isOpening
            || TeamRulesService.ValidatePassBetween(state, coin, validationPath, gateMargin);

        float pull = ComputeIdealPull(coin, origin, goalCenter, isOpening);

        output.Add(new ShotPlan
        {
            Coin = coin,
            Direction = direction,
            PullDistance = pull,
            RespectsRules = respectsRules
        });
    }

    /// <summary>
    /// Hedefe ulaşmak için gereken pull değerini fizik tabanlı hesaplar.
    /// Durma mesafesi ≈ pull × kDistancePerPull, bu yüzden pull = dist / kDistancePerPull.
    /// </summary>
    static float ComputeIdealPull(CoinIdentity coin, Vector3 origin, Vector3 goalCenter, bool isOpening)
    {
        float minPull = coin.DragController.MinPullDistance;
        float maxPull = coin.DragController.MaxPullDistance;

        if (isOpening)
        {
            // Açılış: hafif-orta güç (ilk hamle, gol şartı yok)
            return Mathf.Lerp(minPull, maxPull, Random.Range(0.35f, 0.55f));
        }

        float distToGoal = Mathf.Max((goalCenter - origin).magnitude, 0.1f);

        // Coin'in kaleye rahatça ulaşması için %20 fazla güç
        const float reachFactor = 1.2f;
        float idealPull = distToGoal * reachFactor / kDistancePerPull;

        return Mathf.Clamp(idealPull, minPull, maxPull);
    }

    static ShotPlan ApplyNoise(ShotPlan plan, OpponentBotDifficulty difficulty)
    {
        CoinDragController dragController = plan.Coin.DragController;
        float yaw = Random.Range(-difficulty.AimNoiseDegrees, difficulty.AimNoiseDegrees);
        Vector3 direction = Quaternion.Euler(0f, yaw, 0f) * plan.Direction;
        float pull = Mathf.Clamp(
            plan.PullDistance + Random.Range(-difficulty.PullNoise, difficulty.PullNoise),
            dragController.MinPullDistance,
            dragController.MaxPullDistance);

        plan.Direction = direction.normalized;
        plan.PullDistance = pull;
        return plan;
    }

    static Vector3 ResolvePlayerGoalCenter()
    {
        GoalZone[] zones = Object.FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            GoalZone zone = zones[i];
            if (zone.transform.parent != null && zone.transform.parent.name.Contains("_P"))
            {
                return zone.transform.position;
            }
        }

        return Vector3.zero;
    }
}
