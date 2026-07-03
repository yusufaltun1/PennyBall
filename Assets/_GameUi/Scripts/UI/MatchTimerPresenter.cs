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

    void Update()
    {
        if (_label == null || LeagueMatchController.Instance == null)
        {
            return;
        }

        int seconds = Mathf.CeilToInt(LeagueMatchController.Instance.MatchTimeRemaining);
        _label.SetText(Mathf.Max(0, seconds).ToString());
    }
}
