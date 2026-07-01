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
    public float AimNoiseDegrees => Mathf.Lerp(14f, 0.5f, Normalized);
    public float PullNoise => Mathf.Lerp(0.10f, 0.005f, Normalized);
    public float RuleCompliance => Mathf.Lerp(0.35f, 0.99f, Normalized);
    public float GoalFocus => Mathf.Lerp(0.55f, 1f, Normalized);

    float Normalized => Mathf.Clamp01((Level - 1) / 9f);
}
