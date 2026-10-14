using System.Collections;
using TMPro;
using UnityEngine;

public class MatchScoreboardPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _playerScoreText;
    [SerializeField] TextMeshProUGUI _opponentScoreText;

    bool _handleMatchEnd;

    void Awake()
    {
        // Sahnede birden fazla MatchScoreboardPresenter var: ana ScoreBar (skor + maç-bitişi
        // yönetimi) ve ResultPanel/HukmenMaglup/ExitMatch gibi panellerin içindeki "Score"
        // alt-objeleri (sadece skor gösterimi). Sadece gerçek ScoreBar örneği MatchTimerExpired'a
        // abone olmalı — aksi halde maç bitişi birden fazla kez işlenip (örn. ikinci işlemde
        // state tüketilmiş olduğu için yanlış "Draw" sonucu) çakışan panellere yol açıyordu.
        _handleMatchEnd = gameObject.name == "ScoreBar";
    }

    void Start()
    {
        StartCoroutine(BindWhenReady());
        ExitMatchPanelController.FindPanel();
    }

    void OnDestroy()
    {
        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.ScoresChanged -= RefreshDisplay;
            LeagueMatchController.Instance.MatchTimerExpired -= OnMatchTimerExpired;
        }
    }

    IEnumerator BindWhenReady()
    {
        while (LeagueMatchController.Instance == null)
            yield return null;

        LeagueMatchController.Instance.ScoresChanged -= RefreshDisplay;
        LeagueMatchController.Instance.ScoresChanged += RefreshDisplay;

        if (_handleMatchEnd)
        {
            LeagueMatchController.Instance.MatchTimerExpired -= OnMatchTimerExpired;
            LeagueMatchController.Instance.MatchTimerExpired += OnMatchTimerExpired;
        }

        RefreshDisplay();
    }

    public void RefreshDisplay()
    {
        GetScores(out int playerScore, out int opponentScore);

        if (_playerScoreText != null)
            _playerScoreText.SetText(playerScore.ToString());
        if (_opponentScoreText != null)
            _opponentScoreText.SetText(opponentScore.ToString());
    }

    void GetScores(out int playerScore, out int opponentScore)
    {
        if (LeagueMatchController.Instance != null && LeagueMatchController.Instance.IsMatchActive)
        {
            playerScore = LeagueMatchController.Instance.PlayerGoals;
            opponentScore = LeagueMatchController.Instance.OpponentGoals;
            return;
        }

        // Maç bitti (hükmen 0-3 dahil) — final skor
        playerScore = MatchSessionContext.PlayerGoalsAtEnd;
        opponentScore = MatchSessionContext.OpponentGoalsAtEnd;

        if (LeagueMatchController.Instance != null
            && playerScore == 0
            && opponentScore == 0
            && (LeagueMatchController.Instance.PlayerGoals != 0
                || LeagueMatchController.Instance.OpponentGoals != 0))
        {
            playerScore = LeagueMatchController.Instance.PlayerGoals;
            opponentScore = LeagueMatchController.Instance.OpponentGoals;
        }
    }

    public static void RefreshAll()
    {
        MatchScoreboardPresenter[] presenters =
            FindObjectsByType<MatchScoreboardPresenter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < presenters.Length; i++)
            presenters[i].RefreshDisplay();
    }

    void OnMatchTimerExpired()
    {
        CoinInputHandler[] inputs = FindObjectsByType<CoinInputHandler>(FindObjectsSortMode.None);
        for (int i = 0; i < inputs.Length; i++)
            inputs[i].enabled = false;

        OpponentBotController.Instance?.FreezeMatch();
        GameFeedback.EnsureInstance()?.PlayWhistle();

        MatchResultType result = MatchResultType.Draw;
        if (LeagueMatchController.Instance != null)
            result = LeagueMatchController.Instance.CompleteMatchFromTimer();

        RefreshAll();

        ResultPanelController resultPanel = FindNormalResultPanel();
        resultPanel?.ShowResult(result);
    }

    static ResultPanelController FindNormalResultPanel()
    {
        ResultPanelController[] panels =
            FindObjectsByType<ResultPanelController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].gameObject.name == "ResultPanel")
            {
                return panels[i];
            }
        }

        for (int i = 0; i < panels.Length; i++)
        {
            if (panels[i] != null && panels[i].gameObject.name != "HukmenMaglup")
            {
                return panels[i];
            }
        }

        return null;
    }
}
