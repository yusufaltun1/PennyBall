using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lig durumunu UI'da göstermek için basit presenter.
/// LigTimer (TMP): "League ends in 2d 8h 32m" — sezon bitişine kalan süre.
/// </summary>
public class LeagueStatusPresenter : MonoBehaviour
{
    const string SeasonTimerObjectName = "LigTimer";
    const string SeasonEndedText = "League ended";

    [SerializeField] Text _leagueLabel;
    [SerializeField] Text _rankLabel;
    [SerializeField] Text _pointsLabel;
    [SerializeField] TextMeshProUGUI _seasonTimeLabel;
    [SerializeField] Button _continueButton;

    string _lastSeasonTimerText;

    void Awake()
    {
        if (_continueButton == null)
            _continueButton = GetComponentInChildren<Button>(true);

        if (_continueButton != null)
            _continueButton.onClick.AddListener(OnContinueClicked);

        ResolveSeasonTimeLabel();
    }

    void OnDestroy()
    {
        if (_continueButton != null)
            _continueButton.onClick.RemoveListener(OnContinueClicked);
    }

    void OnEnable()
    {
        ResolveSeasonTimeLabel();

        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.StandingsUpdated += Refresh;
            LeagueService.Instance.SeasonChanged += Refresh;
        }

        Refresh();
    }

    void OnDisable()
    {
        if (LeagueService.Instance != null)
        {
            LeagueService.Instance.StandingsUpdated -= Refresh;
            LeagueService.Instance.SeasonChanged -= Refresh;
        }
    }

    void OnContinueClicked()
    {
        gameObject.SetActive(false);
        AdsService.HidePostMatchOverlays();
        AdsService.GoToMainMenuMaybeWithInterstitial();
    }

    void Update()
    {
        RefreshSeasonTimer();
    }

    void Refresh()
    {
        if (LeagueService.Instance == null || LeagueService.Instance.Save == null)
        {
            return;
        }

        LeagueSaveData save = LeagueService.Instance.Save;
        LeagueStandingEntry player = FindPlayer(save);

        if (_leagueLabel != null)
        {
            _leagueLabel.text = LeagueConfig.GetLeagueName(save.playerLeague);
        }

        if (_rankLabel != null)
        {
            _rankLabel.text = $"#{LeagueService.Instance.GetPlayerRank()}";
        }

        if (_pointsLabel != null && player != null)
        {
            _pointsLabel.text = $"{player.points} pts";
        }

        _lastSeasonTimerText = null;
        RefreshSeasonTimer();
    }

    void RefreshSeasonTimer()
    {
        if (_seasonTimeLabel == null || LeagueService.Instance == null)
        {
            return;
        }

        TimeSpan remaining = LeagueService.Instance.SeasonRemaining;
        string text;
        if (remaining <= TimeSpan.Zero)
        {
            text = SeasonEndedText;
        }
        else
        {
            text =
                $"League ends in {remaining.Days}d {remaining.Hours}h {remaining.Minutes}m";
        }

        if (text == _lastSeasonTimerText)
        {
            return;
        }

        _lastSeasonTimerText = text;
        _seasonTimeLabel.text = text;
    }

    void ResolveSeasonTimeLabel()
    {
        if (_seasonTimeLabel != null)
        {
            return;
        }

        Transform timer = FindDeepChild(transform, SeasonTimerObjectName);
        if (timer != null)
        {
            _seasonTimeLabel = timer.GetComponent<TextMeshProUGUI>();
        }
    }

    static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == name)
            {
                return child;
            }

            Transform nested = FindDeepChild(child, name);
            if (nested != null)
            {
                return nested;
            }
        }

        return null;
    }

    static LeagueStandingEntry FindPlayer(LeagueSaveData save)
    {
        if (save.standings == null)
        {
            return null;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                return save.standings[i];
            }
        }

        return null;
    }
}
