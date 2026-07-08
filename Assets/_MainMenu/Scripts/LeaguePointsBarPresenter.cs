using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class LeaguePointsBarPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _pointsLabel;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        Refresh();
        Subscribe();
        StartCoroutine(SubscribeWhenReady());
    }

    void Start()
    {
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    IEnumerator SubscribeWhenReady()
    {
        while (LeagueService.Instance == null)
        {
            yield return null;
        }

        Subscribe();
        Refresh();
    }

    void Subscribe()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.StandingsUpdated -= Refresh;
        LeagueService.Instance.StandingsUpdated += Refresh;
    }

    void Unsubscribe()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.StandingsUpdated -= Refresh;
    }

    void ResolveReferences()
    {
        if (_pointsLabel != null)
        {
            return;
        }

        Transform pointsText = transform.Find("Texts/PuanText");
        if (pointsText != null)
        {
            _pointsLabel = pointsText.GetComponent<TextMeshProUGUI>();
        }
    }

    public void Refresh()
    {
        ResolveReferences();

        if (_pointsLabel == null)
        {
            return;
        }

        _pointsLabel.SetText(LeaguePlayerStats.GetPlayerPoints().ToString());
    }
}
