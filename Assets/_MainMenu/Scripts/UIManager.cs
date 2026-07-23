using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] MatchingPanelController matchingPanel;
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
    }

    public void OnPlayButtonPressed()
    {
        MainMenuClickSound.Play();

        if (!OnboardingProgress.IsCompleted)
        {
            SceneManager.LoadScene(OnboardingSceneNames.Onboarding);
            return;
        }

        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginMatchFlow();
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
