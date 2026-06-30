using UnityEngine;

/// <summary>
/// Zorluk 1–10 profili. Sadece bot beyni kullanır.
/// </summary>
[System.Serializable]
public struct OpponentBotDifficulty
{
    [Range(1, 10)]
    public int Level;

    public float ThinkDelaySeconds => Mathf.Lerp(2.5f, 0.3f, Normalized);
    // Max 12° at level 1, 1° at level 10 — gate is ~20cm wide at ~50cm range, ±6° tolerance
    public float AimNoiseDegrees => Mathf.Lerp(12f, 1f, Normalized);
    public float PullNoise => Mathf.Lerp(0.08f, 0.01f, Normalized);
    public float RuleCompliance => Mathf.Lerp(0.4f, 0.99f, Normalized);
    public float GoalFocus => Mathf.Lerp(0.3f, 0.98f, Normalized);

    float Normalized => Mathf.Clamp01((Level - 1) / 9f);
}
