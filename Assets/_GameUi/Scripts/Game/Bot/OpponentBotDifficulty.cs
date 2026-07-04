using UnityEngine;

/// <summary>
/// Bot zorluk profili (1 = zayıf, 10 = güçlü).
/// </summary>
[System.Serializable]
public struct OpponentBotDifficulty
{
    [Range(1, 10)]
    public int Level;

    public float ThinkDelaySeconds => Mathf.Lerp(2.8f, 0.25f, Normalized);
    public float AimNoiseDegrees => Mathf.Lerp(18f, 0.35f, Normalized);
    public float PullNoise => Mathf.Lerp(0.22f, 0.012f, Normalized);
    public float RuleCompliance => Mathf.Lerp(0.35f, 0.99f, Normalized);
    public float GoalFocus => Mathf.Lerp(0.55f, 1f, Normalized);

    /// <summary>1'de ~%58 max pull, 10'da tam max pull.</summary>
    public float MaxPullScale => Mathf.Lerp(0.58f, 1f, Normalized);

    /// <summary>Gol denemelerinde ideal gücün ne kadarını kullanır.</summary>
    public float GoalFinishPullScale => Mathf.Lerp(0.68f, 1f, Normalized);

    /// <summary>Zorunlu kapı geçişinde max gücün ne kadarını kullanır.</summary>
    public float GatePassPullScale => Mathf.Lerp(0.70f, 1f, Normalized);

    /// <summary>Açılış hamlesinde strength max pull'un ne kadarı.</summary>
    public float OpeningPowerScale => Mathf.Lerp(0.52f, 0.88f, Normalized);

    /// <summary>Açılış nişan sapması (derece).</summary>
    public float OpeningAimSpreadDegrees => Mathf.Lerp(26f, 7f, Normalized);

    /// <summary>Gol fırsatında ek nişan/güç hatası çarpanı (zayıf bot daha hatalı).</summary>
    public float GoalFinishNoiseScale => Mathf.Lerp(1.85f, 0.12f, Normalized);

    float Normalized => Mathf.Clamp01((Level - 1) / 9f);
}
