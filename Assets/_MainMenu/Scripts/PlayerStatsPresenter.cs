using TMPro;
using UnityEngine;

public class PlayerStatsPresenter : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] TextMeshProUGUI _pointsText;
    [SerializeField] TextMeshProUGUI _matchesPlayedText;
    [SerializeField] TextMeshProUGUI _totalGoalsText;
    [SerializeField] TextMeshProUGUI _winsText;
    [SerializeField] TextMeshProUGUI _drawsText;
    [SerializeField] TextMeshProUGUI _lossesText;

    void OnEnable()
    {
        if (LeagueService.Instance != null)
            LeagueService.Instance.StandingsUpdated += Refresh;

        Refresh();
    }

    void OnDisable()
    {
        if (LeagueService.Instance != null)
            LeagueService.Instance.StandingsUpdated -= Refresh;
    }

    void Refresh()
    {
        LeagueStandingEntry player = FindPlayerEntry();

        int points  = player?.points ?? 0;
        int played  = player?.played ?? 0;
        int wins    = player?.wins   ?? 0;
        int draws   = player?.draws  ?? 0;
        int losses  = played - wins - draws;
        int goals   = LeagueService.Instance?.Save?.playerTotalGoals ?? 0;

        Set(_pointsText,       points.ToString());
        Set(_matchesPlayedText, played.ToString());
        Set(_totalGoalsText,   goals.ToString());
        Set(_winsText,         wins.ToString());
        Set(_drawsText,        draws.ToString());
        Set(_lossesText,       Mathf.Max(0, losses).ToString());
    }

    static LeagueStandingEntry FindPlayerEntry()
    {
        LeagueSaveData save = LeagueService.Instance?.Save;
        if (save?.standings == null) return null;

        for (int i = 0; i < save.standings.Length; i++)
            if (save.standings[i].isPlayer)
                return save.standings[i];

        return null;
    }

    static void Set(TextMeshProUGUI label, string value)
    {
        if (label != null) label.SetText(value);
    }
}
