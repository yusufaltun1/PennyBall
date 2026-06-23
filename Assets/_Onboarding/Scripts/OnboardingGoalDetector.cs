using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public class OnboardingGoalDetector : MonoBehaviour
{
    public event Action<OnboardingCoin> GoalScored;

    readonly Collider[] _overlapResults = new Collider[8];

    void OnTriggerEnter(Collider other)
    {
        TryReportGoal(other);
    }

    void OnTriggerStay(Collider other)
    {
        TryReportGoal(other);
    }

    void TryReportGoal(Collider other)
    {
        OnboardingCoin coin = other.GetComponentInParent<OnboardingCoin>();
        if (coin == null)
        {
            return;
        }

        GoalScored?.Invoke(coin);
    }

    public bool ContainsCoin(OnboardingCoin coin, float margin = 0.55f)
    {
        if (coin == null)
        {
            return false;
        }

        Collider trigger = GetComponent<Collider>();
        if (trigger == null)
        {
            return false;
        }

        Bounds bounds = trigger.bounds;
        bounds.Expand(margin);
        if (bounds.Contains(coin.transform.position))
        {
            return true;
        }

        int hitCount = Physics.OverlapBoxNonAlloc(
            bounds.center,
            bounds.extents,
            _overlapResults,
            Quaternion.identity);
        for (int i = 0; i < hitCount; i++)
        {
            OnboardingCoin hitCoin = _overlapResults[i].GetComponentInParent<OnboardingCoin>();
            if (hitCoin == coin)
            {
                return true;
            }
        }

        return false;
    }
}
