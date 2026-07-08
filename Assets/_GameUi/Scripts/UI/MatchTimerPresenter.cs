using TMPro;
using UnityEngine;

public class MatchTimerPresenter : MonoBehaviour
{
    TextMeshProUGUI _label;

    void Awake()
    {
        _label = GetComponent<TextMeshProUGUI>();
        if (_label == null)
            _label = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Start()
    {
        Refresh();
    }

    void Update()
    {
        Refresh();
    }

    void Refresh()
    {
        if (_label == null || LeagueMatchController.Instance == null)
        {
            return;
        }

        int seconds = LeagueMatchController.Instance.IsMatchActive
            ? Mathf.CeilToInt(LeagueMatchController.Instance.MatchTimeRemaining)
            : Mathf.CeilToInt(GetIdleDisplaySeconds());

        _label.SetText(Mathf.Max(0, seconds).ToString());
    }

    static float GetIdleDisplaySeconds()
    {
        if (LeagueMatchController.Instance == null)
        {
            return 0f;
        }

        float remaining = LeagueMatchController.Instance.MatchTimeRemaining;
        if (remaining > 0f)
        {
            return remaining;
        }

        return ExerciseRuntime.IsActive
            ? LeagueConfig.ExerciseMatchDurationSeconds
            : LeagueConfig.MatchDurationSeconds;
    }
}
