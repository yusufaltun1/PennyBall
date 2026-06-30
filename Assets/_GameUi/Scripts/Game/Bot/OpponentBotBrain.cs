using UnityEngine;

/// <summary>
/// Bot beyni — 4 aşamalı strateji:
///   1. Atış : sol coin → sol kenara %60 güç
///   2. Atış : sağ coin → sağ kenara %60 güç
///   3. Atış : orta coin → ilk iki coinin ortasından %100 güç
///   4+ Atış: kaleyi hedefle; yol doluysa 5 sn bekle,
///            yetişemiyorsa iki coin arasını hedefle (oto güç)
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
    }

    // durma = pull × launchForce(18) / drag(2.5) = pull × 7.2
    const float kStopDistPerPull = 7.2f;
    const float kRefDist         = 2.88f;   // maxPull(0.40) × 7.2

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
        if (goal == Vector3.zero) goal = new Vector3(1.52f, 0.14f, 2.235f);

        bool ok;
        switch (shotNumber)
        {
            case 1:
                ok = BuildSideShot(state, isResolvingMove, goRight: false, goal, out plan);
                break;
            case 2:
                ok = BuildSideShot(state, isResolvingMove, goRight: true,  goal, out plan);
                break;
            case 3:
                ok = BuildThroughMiddleShot(state, isResolvingMove, goal, out plan);
                break;
            default:
                ok = BuildGoalShot(state, isResolvingMove, goal, coinBlockRadius,
                                   out plan, out pathBlocked);
                break;
        }

        if (ok && shotNumber >= 3)
            plan = ApplyNoise(plan, difficulty);   // 1-2. atışa noise ekleme

        return ok;
    }

    // ── 1. ve 2. atış: kenarlara %60 güç ────────────────────────────────────

    static bool BuildSideShot(
        TeamRoundState state, bool isResolvingMove,
        bool goRight, Vector3 goal, out ShotPlan plan)
    {
        plan = default;

        // Uygun coinler arasından yöne göre en iyi konumdaki coini seç:
        // sağa gidecekse en sağdaki (X max), sola gidecekse en soldaki (X min).
        // İlk atışta first-move kısıtlaması zaten opening coini (ortadaki) zorlar.
        CoinIdentity coin = null;
        float bestX = goRight ? float.MinValue : float.MaxValue;
        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity c = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive)) continue;
            float x = c.transform.position.x;
            if (goRight ? x > bestX : x < bestX) { bestX = x; coin = c; }
        }
        if (coin == null || coin.DragController == null) return false;

        CoinDragController dc      = coin.DragController;
        Vector3            origin  = Flat(coin.transform.position);
        Vector3            goalDir = (Flat(goal) - origin).normalized;

        // Karşı kaleye doğru hafif açı: +15° sağa, -15° sola
        float   angle     = goRight ? 15f : -15f;
        Vector3 direction = (Quaternion.Euler(0f, angle, 0f) * goalDir).normalized;

        float pull = dc.MaxPullDistance * 0.70f;

        plan = new ShotPlan
        {
            Coin = coin, Direction = direction, PullDistance = pull,
            RespectsRules = true, Score = 1f
        };
        Debug.Log($"[Bot] Atış {(goRight ? 2 : 1)} | {coin.name} → {(goRight ? "sağ" : "sol")} kenar " +
                  $"| MaxPull={dc.MaxPullDistance:F3} pull={pull:F3} beklenenMesafe={pull * kStopDistPerPull:F2}m");
        return true;
    }

    // ── 3. atış: ilk iki coin arasından %100 güç ─────────────────────────────

    static bool BuildThroughMiddleShot(
        TeamRoundState state, bool isResolvingMove,
        Vector3 goal, out ShotPlan plan)
    {
        plan = default;

        // Eligible coinler arasından diğer ikisinin tam ortasına en iyi açıyla atabilecek coini seç
        CoinIdentity coin = null;
        {
            CoinIdentity bestC = null; float bestScore = float.MinValue;
            for (int i = 0; i < state.Coins.Count; i++)
            {
                CoinIdentity c = state.Coins[i];
                if (!TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive)) continue;
                // Diğer iki coin'in midpoint'ini bul
                Vector3 pA = Vector3.zero, pB = Vector3.zero; int found = 0;
                for (int j = 0; j < state.Coins.Count; j++)
                {
                    if (state.Coins[j] == c) continue;
                    if (found == 0) { pA = Flat(state.Coins[j].transform.position); found++; }
                    else            { pB = Flat(state.Coins[j].transform.position); found++; break; }
                }
                if (found < 2) continue;
                Vector3 mid        = (pA + pB) * 0.5f;
                Vector3 toMidDir   = (mid - Flat(c.transform.position)).normalized;
                Vector3 goalDir    = (Flat(goal) - Flat(c.transform.position)).normalized;
                float   score      = Vector3.Dot(toMidDir, goalDir);
                if (score > bestScore) { bestScore = score; bestC = c; }
            }
            coin = bestC;
        }
        if (coin == null || coin.DragController == null) return false;

        CoinDragController dc     = coin.DragController;
        Vector3            origin = Flat(coin.transform.position);

        // İlk iki coin (Coins[0] ve Coins[son]) konumlarını al
        CoinIdentity coinA = state.Coins[0];
        CoinIdentity coinB = state.Coins[state.Coins.Count - 1];

        // Eğer coin kendisi kenardaysa bitişiğini kullan
        if (coinA == coin && state.Coins.Count > 1) coinA = state.Coins[1];
        if (coinB == coin && state.Coins.Count > 1) coinB = state.Coins[state.Coins.Count - 2];

        Vector3 posA    = Flat(coinA.transform.position);
        Vector3 posB    = Flat(coinB.transform.position);
        Vector3 midPoint = (posA + posB) * 0.5f;

        Vector3 toMid = midPoint - origin;
        if (toMid.sqrMagnitude < 0.001f) return false;

        // %100 güç, iki coinin tam ortasına doğru
        plan = new ShotPlan
        {
            Coin = coin, Direction = toMid.normalized, PullDistance = dc.MaxPullDistance,
            RespectsRules = true, Score = 1f
        };
        Debug.Log($"[Bot] Atış 3 | {coin.name} → [{coinA.name}@{posA:F1}, {coinB.name}@{posB:F1}] " +
                  $"orta={midPoint:F1} | 100% pull={dc.MaxPullDistance:F3}");
        return true;
    }

    // ── 4+. atış: kaleyi hedefle, yol kontrolü ────────────────────────────────

    static bool BuildGoalShot(
        TeamRoundState state, bool isResolvingMove,
        Vector3 goal, float coinBlockRadius,
        out ShotPlan plan, out bool pathBlocked)
    {
        plan        = default;
        pathBlocked = false;

        // Kaleye en yakın uygun coini bul
        CoinIdentity best     = null;
        float        bestDist = float.MaxValue;
        Vector3      goalFlat = Flat(goal);

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity c = state.Coins[i];
            if (!TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive)) continue;
            float d = Vector3.Distance(Flat(c.transform.position), goalFlat);
            if (d < bestDist) { bestDist = d; best = c; }
        }

        if (best == null)
        {
            Debug.LogWarning("[Bot] Atış 4+: Uygun coin yok");
            return false;
        }

        // Yolu açık olan coini bul — önce en yakın, sonra diğerleri
        CoinIdentity chosen  = null;
        float        chosenDist = 0f;
        {
            // En yakın coinden başla, yolu doluysa diğerlerini dene
            CoinIdentity[] candidates = new CoinIdentity[state.Coins.Count];
            float[]        dists      = new float[state.Coins.Count];
            int            count      = 0;
            for (int i = 0; i < state.Coins.Count; i++)
            {
                CoinIdentity c = state.Coins[i];
                if (!TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive)) continue;
                candidates[count] = c;
                dists[count]      = Vector3.Distance(Flat(c.transform.position), goalFlat);
                count++;
            }
            // Mesafeye göre sırala (basit bubble sort — coin sayısı az)
            for (int i = 0; i < count - 1; i++)
                for (int j = i + 1; j < count; j++)
                    if (dists[j] < dists[i])
                    {
                        (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
                        (dists[i],      dists[j])      = (dists[j],      dists[i]);
                    }

            for (int i = 0; i < count; i++)
            {
                Vector3 org = Flat(candidates[i].transform.position);
                if (!IsPathBlocked(org, goalFlat, candidates[i], coinBlockRadius))
                {
                    chosen     = candidates[i];
                    chosenDist = dists[i];
                    break;
                }
                Debug.Log($"[Bot] {candidates[i].name} yolu dolu, sıradaki deneniyor");
            }
        }

        // Tüm yollar dolu → pozisyon atışına geç (5 sn bekleme yok)
        if (chosen == null)
        {
            Debug.Log("[Bot] Atış 4+: Tüm yollar dolu, pozisyon atışı yapılıyor");
            Vector3 anyDir = (goalFlat - Flat(best.transform.position)).normalized;
            if (BuildPositioningShot(state, best, anyDir, best.DragController, out plan))
                return true;
            // Son çare: en yakın coinle direkt at
            chosen     = best;
            chosenDist = bestDist;
        }

        CoinDragController dc     = chosen.DragController;
        Vector3            origin  = Flat(chosen.transform.position);
        float              dist    = chosenDist;
        Vector3            goalDir = (goalFlat - origin).normalized;

        float maxReach = dc.MaxPullDistance * kStopDistPerPull;   // 2.88 m

        // ── Kaleye yetişebilir → direkt at ──
        if (dist <= maxReach - 0.2f)
        {
            // Gate'den de geçmesi gerekiyor; gate yönüyle %40 blend ekle
            if (TeamRulesService.TryGetGateCoins(state, chosen, out CoinIdentity gA, out CoinIdentity gB))
            {
                Vector3 gateCenter = (Flat(gA.transform.position) + Flat(gB.transform.position)) * 0.5f;
                Vector3 gateDir    = (gateCenter - origin).normalized;
                goalDir = Vector3.Lerp(gateDir, goalDir, 0.60f).normalized;  // %60 kale, %40 kapı
            }

            float pull = PullForDistance(dc, dist + 0.5f);
            plan = new ShotPlan
            {
                Coin = chosen, Direction = goalDir, PullDistance = pull,
                RespectsRules = true, Score = 1f
            };
            Debug.Log($"[Bot] Atış 4+ KALE | {chosen.name} dist={dist:F2}m pull={pull:F3}");
            return true;
        }

        // ── Kaleye yetişemiyor → iki coin arasından ilerle ──
        if (BuildPositioningShot(state, chosen, goalDir, dc, out plan))
        {
            Debug.Log($"[Bot] Atış 4+ POZİSYON | {chosen.name} dist={dist:F2}m > maxReach={maxReach:F2}m");
            return true;
        }

        // Fallback: max güçle kaleye doğru at
        plan = new ShotPlan
        {
            Coin = chosen, Direction = goalDir, PullDistance = dc.MaxPullDistance,
            RespectsRules = true, Score = 1f
        };
        Debug.Log($"[Bot] Atış 4+ MAX-FALLBACK | {chosen.name}");
        return true;
    }

    // ── Pozisyon vuruşu: iki coin arasından geç ──────────────────────────────

    static bool BuildPositioningShot(
        TeamRoundState state, CoinIdentity shooter,
        Vector3 goalDir, CoinDragController dc, out ShotPlan plan)
    {
        plan = default;

        CoinIdentity bestA = null, bestB = null;
        float        bestScore = float.MinValue;
        Vector3      origin = Flat(shooter.transform.position);

        CoinIdentity[] all = Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == shooter) continue;
            for (int j = i + 1; j < all.Length; j++)
            {
                if (all[j] == shooter) continue;

                Vector3 pA   = Flat(all[i].transform.position);
                Vector3 pB   = Flat(all[j].transform.position);
                Vector3 mid  = (pA + pB) * 0.5f;
                Vector3 toMid = mid - origin;

                // Öne bak (kale yönünde)
                if (Vector3.Dot(toMid.normalized, goalDir) < 0.25f) continue;

                float gateW = (pA - pB).magnitude;
                if (gateW < 0.12f) continue;  // çok dar kapı

                float score = Vector3.Dot(toMid.normalized, goalDir) * 0.7f
                            + Mathf.Clamp01(gateW / 0.5f) * 0.3f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestA = all[i];
                    bestB = all[j];
                }
            }
        }

        if (bestA == null) return false;

        Vector3 gateCenter = (Flat(bestA.transform.position) + Flat(bestB.transform.position)) * 0.5f;
        Vector3 direction  = (gateCenter - origin).normalized;
        float   distToGate = Vector3.Distance(origin, gateCenter);

        // Formül: coin kapıyı geçip ötesinde dursun (+1.0 m tampon)
        float pull = PullForDistance(dc, distToGate + 1.0f);

        plan = new ShotPlan
        {
            Coin = shooter, Direction = direction, PullDistance = pull,
            RespectsRules = true, Score = bestScore
        };
        Debug.Log($"[Bot] Pozisyon | → [{bestA.name},{bestB.name}] orta={gateCenter:F1} " +
                  $"dist={distToGate:F2}m pull={pull:F3}");
        return true;
    }

    // ── Yol engeli kontrolü ───────────────────────────────────────────────────

    static bool IsPathBlocked(
        Vector3 origin, Vector3 goalPos,
        CoinIdentity shooter, float blockRadius)
    {
        CoinIdentity[] all    = Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        Vector3        toGoal = goalPos - origin;
        float          totalD = toGoal.magnitude;
        Vector3        dir    = toGoal.normalized;

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == shooter) continue;

            Vector3 toBlocker = Flat(all[i].transform.position) - origin;
            float   fwd       = Vector3.Dot(toBlocker, dir);
            if (fwd < 0.05f || fwd > totalD) continue;

            float lateral = (toBlocker - dir * fwd).magnitude;
            if (lateral < blockRadius)
            {
                Debug.Log($"[Bot] Engel: {all[i].name} @ ilerle={fwd:F2}m lateral={lateral:F2}m");
                return true;
            }
        }

        return false;
    }

    // ── Gürültü (3+. atışlar için) ────────────────────────────────────────────

    static ShotPlan ApplyNoise(ShotPlan plan, OpponentBotDifficulty difficulty)
    {
        if (plan.Coin == null || plan.Coin.DragController == null) return plan;

        CoinDragController dc = plan.Coin.DragController;

        float yaw = Random.Range(-difficulty.AimNoiseDegrees, difficulty.AimNoiseDegrees);
        plan.Direction = (Quaternion.Euler(0f, yaw, 0f) * plan.Direction).normalized;

        float pullNoise = Random.Range(-difficulty.PullNoise, difficulty.PullNoise);
        plan.PullDistance = Mathf.Clamp(
            plan.PullDistance + pullNoise,
            dc.MinPullDistance, dc.MaxPullDistance);

        return plan;
    }

    // ── Yardımcılar ──────────────────────────────────────────────────────────

    /// <summary>X metreye ulaşmak için gereken pull. Tampon ekleyerek çağır.</summary>
    static float PullForDistance(CoinDragController dc, float meters)
        => Mathf.Clamp(meters / kStopDistPerPull, dc.MinPullDistance, dc.MaxPullDistance);

    /// <summary>Tercih edilen indeksteki uygun coini döner; yoksa herhangi birini.</summary>
    static CoinIdentity GetEligibleAt(TeamRoundState state, bool isResolvingMove, int preferredIdx)
    {
        if (preferredIdx >= 0 && preferredIdx < state.Coins.Count)
        {
            CoinIdentity c = state.Coins[preferredIdx];
            if (TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive)) return c;
        }

        for (int i = 0; i < state.Coins.Count; i++)
        {
            CoinIdentity c = state.Coins[i];
            if (TeamRulesService.CanSelectCoin(state, c, isResolvingMove, c.IsPassive)) return c;
        }

        return null;
    }

    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

    static Vector3 ResolvePlayerGoalCenter()
    {
        GoalZone[] zones = Object.FindObjectsByType<GoalZone>(FindObjectsSortMode.None);
        for (int i = 0; i < zones.Length; i++)
        {
            GoalZone z = zones[i];
            if (z.transform.parent != null && z.transform.parent.name.Contains("_P"))
                return z.transform.position;
        }
        return Vector3.zero;
    }
}
