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

    public static bool TryChooseShot(
        TeamRoundState state,
        OpponentBotDifficulty difficulty,
        bool isResolvingMove,
        float gateMargin,
        out ShotPlan plan)
    {
        plan = default;

        var candidates = new List<ShotPlan>(12);
        CollectCandidateShots(state, difficulty, isResolvingMove, gateMargin, candidates);

        if (candidates.Count == 0)
        {
            return false;
        }

        candidates.Sort((a, b) => b.PullDistance.CompareTo(a.PullDistance));

        for (int i = 0; i < candidates.Count; i++)
        {
            ShotPlan candidate = candidates[i];
            if (candidate.RespectsRules && Random.value <= difficulty.RuleCompliance)
            {
                plan = ApplyNoise(candidate, difficulty);
                return true;
            }
        }

        plan = ApplyNoise(candidates[0], difficulty);
        return true;
    }

    static void CollectCandidateShots(
        TeamRoundState state,
        OpponentBotDifficulty difficulty,
        bool isResolvingMove,
        float gateMargin,
        List<ShotPlan> output)
    {
        Vector3 goalCenter = ResolvePlayerGoalCenter();
        if (goalCenter == Vector3.zero)
        {
            goalCenter = new Vector3(1.52f, 0.14f, 2.235f);
        }

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity coin = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, coin, isResolvingMove, coin.IsPassive))
            {
                continue;
            }

            bool isOpening = state.IsFirstMove;
            Vector3 origin = coin.transform.position;

            if (isOpening)
            {
                TryAddShot(state, coin, origin, goalCenter - origin, gateMargin, isOpening, true, output);
                continue;
            }

            if (!TeamRulesService.TryGetGateCoins(state, coin, out CoinIdentity gateA, out CoinIdentity gateB))
            {
                continue;
            }

            Vector3 gateCenter = (gateA.transform.position + gateB.transform.position) * 0.5f;
            Vector3 throughGate = (gateCenter - origin);
            throughGate.y = 0f;

            Vector3 toGoal = goalCenter - origin;
            toGoal.y = 0f;

            Vector3 blended = Vector3.Lerp(throughGate.normalized, toGoal.normalized, difficulty.GoalFocus);
            TryAddShot(state, coin, origin, blended, gateMargin, false, true, output);

            TryAddShot(state, coin, origin, throughGate, gateMargin, false, true, output);
            TryAddShot(state, coin, origin, toGoal, gateMargin, false, true, output);

            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, -0.2f));
            TryAddShot(state, coin, origin, randomDir, gateMargin, false, false, output);
        }
    }

    static void TryAddShot(
        TeamRoundState state,
        CoinIdentity coin,
        Vector3 origin,
        Vector3 direction,
        float gateMargin,
        bool isOpening,
        bool checkRules,
        List<ShotPlan> output)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        float pull = Mathf.Lerp(coin.DragController.MinPullDistance, coin.DragController.MaxPullDistance, Random.Range(0.45f, 1f));
        Vector3 end = origin + direction.normalized * (pull * 2.5f);
        var path = new List<Vector3> { origin, end };

        bool respectsRules = isOpening
            || !checkRules
            || TeamRulesService.ValidatePassBetween(state, coin, path, gateMargin);

        output.Add(new ShotPlan
        {
            Coin = coin,
            Direction = direction.normalized,
            PullDistance = pull,
            RespectsRules = respectsRules
        });
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
