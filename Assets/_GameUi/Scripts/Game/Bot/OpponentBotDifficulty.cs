using UnityEngine;

/// <summary>
/// Zorluk 1–10 profili. Sadece bot beyni kullanır.
/// </summary>
[System.Serializable]
public struct OpponentBotDifficulty
{
    [Range(1, 10)]
    public int Level;

    public float ThinkDelaySeconds => Mathf.Lerp(2.8f, 0.35f, Normalized);
    public float AimNoiseDegrees => Mathf.Lerp(24f, 2f, Normalized);
    public float PullNoise => Mathf.Lerp(0.14f, 0.02f, Normalized);
    public float RuleCompliance => Mathf.Lerp(0.3f, 0.98f, Normalized);
    public float GoalFocus => Mathf.Lerp(0.25f, 0.95f, Normalized);

    float Normalized => Mathf.Clamp01((Level - 1) / 9f);
}
