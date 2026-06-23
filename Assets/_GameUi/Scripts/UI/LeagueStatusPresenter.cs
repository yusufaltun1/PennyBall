using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lig durumunu UI'da göstermek için basit presenter.
/// </summary>
public class LeagueStatusPresenter : MonoBehaviour
{
    [SerializeField] Text _leagueLabel;
    [SerializeField] Text _rankLabel;
    [SerializeField] Text _pointsLabel;
    [SerializeField] Text _seasonTimeLabel;

    void OnEnable()
    {
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
            _leagueLabel.text = $"League {save.playerLeague}";
        }

        if (_rankLabel != null)
        {
            _rankLabel.text = $"#{LeagueService.Instance.GetPlayerRank()}";
        }

        if (_pointsLabel != null && player != null)
        {
            _pointsLabel.text = $"{player.points} pts";
        }

        RefreshSeasonTimer();
    }

    void RefreshSeasonTimer()
    {
        if (_seasonTimeLabel == null || LeagueService.Instance == null)
        {
            return;
        }

        System.TimeSpan remaining = LeagueService.Instance.SeasonRemaining;
        if (remaining < System.TimeSpan.Zero)
        {
            remaining = System.TimeSpan.Zero;
        }

        _seasonTimeLabel.text = $"{remaining.Days}d {remaining.Hours}h {remaining.Minutes}m";
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
