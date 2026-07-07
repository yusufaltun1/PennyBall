using System;
using UnityEngine;

[Serializable]
public class IceBoosterTimingSettings
{
    [Tooltip("Rakip coinlerin donuk kalacağı süre (saniye).")]
    [Min(0.1f)]
    public float FreezeDurationSeconds = 3f;

    [Tooltip("ICE butonunun tekrar kullanılamayacağı geri sayım (saniye).")]
    [Min(1)]
    public int CooldownSeconds = 5;
}

[Serializable]
public class IceUsedFeedbackTimingSettings
{
    [Header("Süreler")]
    [Tooltip("IceUsed görselinin 0.1 → 0.75 scale bounce süresi (saniye).")]
    [Min(0.01f)]
    public float BounceInDuration = 0.25f;

    [Tooltip("IceUsed görselinin büyüyüp solma süresi (saniye).")]
    [Min(0.01f)]
    public float ExpandFadeDuration = 0.25f;

    [Header("Scale")]
    [Tooltip("Bounce başlangıç scale değeri.")]
    [Min(0.01f)]
    public float BounceStartScale = 0.1f;

    [Tooltip("Bounce bitiş scale değeri.")]
    [Min(0.01f)]
    public float BounceEndScale = 0.75f;

    [Tooltip("Solmadan önceki son scale değeri.")]
    [Min(0.01f)]
    public float ExitScale = 100f;
}
