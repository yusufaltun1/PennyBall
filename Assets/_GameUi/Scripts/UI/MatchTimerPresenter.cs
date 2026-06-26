using System;
using System.Collections;
using TMPro;
using UnityEngine;

public class MatchTimerPresenter : MonoBehaviour
{
    [SerializeField] float _totalSeconds = LeagueConfig.MatchDurationSeconds;

    public event Action TimerExpired;

    TextMeshProUGUI _label;
    float _remaining;

    void Awake()
    {
        _label = GetComponent<TextMeshProUGUI>();
        if (_label == null)
            _label = GetComponentInChildren<TextMeshProUGUI>();
    }

    void Start()
    {
        _remaining = _totalSeconds;
        UpdateDisplay(Mathf.CeilToInt(_remaining));
        StartCoroutine(CountdownRoutine());
    }

    IEnumerator CountdownRoutine()
    {
        while (_remaining > 0f)
        {
            yield return null;
            _remaining -= Time.deltaTime;
            UpdateDisplay(Mathf.Max(0, Mathf.CeilToInt(_remaining)));
        }

        UpdateDisplay(0);
        TimerExpired?.Invoke();
    }

    void UpdateDisplay(int seconds)
    {
        if (_label != null)
            _label.SetText(seconds.ToString());
    }
}
