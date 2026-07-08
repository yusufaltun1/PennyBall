using System;
using UnityEngine;

public static class BoosterUnlockFlow
{
    public static bool TryShowPendingUnlock(Action onFinished)
    {
        if (!MatchSessionContext.TryConsumePendingBoosterUnlock(out BoosterType boosterType))
        {
            onFinished?.Invoke();
            return false;
        }

        UnlockedFeaturesController panel = FindPanel();
        if (panel == null)
        {
            Debug.LogWarning($"[BoosterUnlock] UnlockedFeatures panel bulunamadı. Tip={boosterType}");
            onFinished?.Invoke();
            return false;
        }

        panel.Configure(boosterType, onFinished);
        panel.gameObject.SetActive(true);
        return true;
    }

    static UnlockedFeaturesController FindPanel()
    {
        return UnityEngine.Object.FindAnyObjectByType<UnlockedFeaturesController>(FindObjectsInactive.Include);
    }
}
