using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] MatchingPanelController matchingPanel;
    [SerializeField] JoinRoomPanelController joinRoomPanel;
    [SerializeField] MatchPresetPanelController matchPresetPanel;
    [SerializeField] Button exerciseButton;

    readonly List<Button> _wiredExerciseButtons = new();

    void Awake()
    {
        if (matchingPanel == null)
        {
            Transform panelTransform = transform.parent != null
                ? transform.parent.Find("Matching_Panel")
                : null;
            if (panelTransform != null)
            {
                matchingPanel = panelTransform.GetComponent<MatchingPanelController>();
            }
        }

        if (matchingPanel != null)
        {
            matchingPanel.gameObject.SetActive(false);
        }

        Transform searchRoot = transform.parent != null ? transform.parent : transform;

        if (joinRoomPanel == null)
        {
            Transform panelTransform = FindDeepChild(searchRoot, "JoinRoomPanel");
            if (panelTransform != null)
            {
                joinRoomPanel = panelTransform.GetComponent<JoinRoomPanelController>();
            }
        }

        joinRoomPanel?.gameObject.SetActive(false);

        if (matchPresetPanel == null)
        {
            Transform panelTransform = FindDeepChild(searchRoot, "MatchPreset");
            if (panelTransform != null)
            {
                matchPresetPanel = panelTransform.GetComponent<MatchPresetPanelController>();
                if (matchPresetPanel == null)
                {
                    matchPresetPanel = panelTransform.gameObject.AddComponent<MatchPresetPanelController>();
                }
            }
        }

        if (matchPresetPanel != null)
        {
            matchPresetPanel.PlayOnlineRequested += StartOnlineMatch;
            matchPresetPanel.JoinCodeSubmitted += OnJoinCodeSubmitted;
            matchPresetPanel.RoomMatchFound += OnRoomMatchFound;
            matchPresetPanel.Close();
        }

        if (exerciseButton == null)
        {
            Transform exerciseTransform = FindDeepChild(searchRoot, "Btn_Exercise");
            if (exerciseTransform != null)
            {
                exerciseButton = exerciseTransform.GetComponent<Button>();
            }
        }

        WireExerciseButton(exerciseButton);

        Transform offlineCarouselItem = FindDeepChild(searchRoot, "Item1");
        if (offlineCarouselItem != null)
        {
            WireExerciseButton(offlineCarouselItem.GetComponentInChildren<Button>(true));
        }
    }

    void OnDestroy()
    {
        if (matchPresetPanel != null)
        {
            matchPresetPanel.PlayOnlineRequested -= StartOnlineMatch;
            matchPresetPanel.JoinCodeSubmitted -= OnJoinCodeSubmitted;
            matchPresetPanel.RoomMatchFound -= OnRoomMatchFound;
        }

        for (int i = 0; i < _wiredExerciseButtons.Count; i++)
        {
            Button button = _wiredExerciseButtons[i];
            if (button != null)
            {
                button.onClick.RemoveListener(OnPlayOfflineButtonPressed);
            }
        }

        _wiredExerciseButtons.Clear();
    }

    void WireExerciseButton(Button button)
    {
        if (button == null || _wiredExerciseButtons.Contains(button))
        {
            return;
        }

        button.onClick.AddListener(OnPlayOfflineButtonPressed);
        _wiredExerciseButtons.Add(button);
    }

    void Start()
    {
        if (!SceneManager.GetSceneByName("3d_Saha_Studio").isLoaded)
        {
            SceneManager.LoadScene("3d_Saha_Studio", LoadSceneMode.Additive);
        }

        if (PendingRematch.HasPending)
        {
            ResumeRematch();
        }
    }

    /// <summary>
    /// ResultPanel > RePlay'de "Play Again"e basılınca bırakılan PendingRematch'i tüketir —
    /// Matching_Panel'i (Create/Join ile aynı "bekleniyor" ekranı) hemen gösterir, ham MainMenu
    /// hiç görünmeden doğrudan matchmaking akışına geçilmiş olur.
    /// </summary>
    void ResumeRematch()
    {
        string roomName = PendingRematch.RoomName;
        string opponentUserId = PendingRematch.OpponentUserId;
        string opponentName = PendingRematch.OpponentDisplayName;
        int opponentAvatar = PendingRematch.OpponentAvatarIndex;
        PendingRematch.Clear();

        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginRematchFlow(roomName, opponentUserId, opponentName, opponentAvatar);
    }

    public void OnPlayButtonPressed()
    {
        MainMenuClickSound.Play();

        string pendingFirstRunScene = FirstRunFlow.GetPendingFirstRunScene();
        if (pendingFirstRunScene != null)
        {
            SceneManager.LoadScene(pendingFirstRunScene);
            return;
        }

        if (matchPresetPanel == null)
        {
            Debug.LogError("[UIManager] MatchPreset bulunamadı.");
            return;
        }

        matchPresetPanel.Open();
    }

    void StartOnlineMatch()
    {
        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginMatchFlow();
    }

    void OnJoinCodeSubmitted(string code)
    {
        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginJoinRoomFlow(code);
    }

    void OnRoomMatchFound(RoomCodeResult result)
    {
        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.ShowMatchFoundAndCountdown(result.Match, result.Code);
    }

    public void OnPlayOfflineButtonPressed()
    {
        MainMenuClickSound.Play();

        GameAnalytics.Track("play_offline", new Dictionary<string, string>
        {
            { "league", LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague.ToString() : "1" },
            { "player_level", WalletService.Level.ToString() },
            { "source", "main_menu" }
        });

        SceneManager.LoadScene(GameSceneNames.Exercise);
    }

    public void OnAntremanButtonPressed() => OnPlayOfflineButtonPressed();

    public void OnCreateRoomButtonPressed()
    {
        MainMenuClickSound.Play();

        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginCreateRoomFlow();
    }

    public void OnJoinRoomButtonPressed()
    {
        MainMenuClickSound.Play();

        if (joinRoomPanel == null)
        {
            Debug.LogError("[UIManager] JoinRoomPanel bulunamadı.");
            return;
        }

        joinRoomPanel.OpenJoinScreen();
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
