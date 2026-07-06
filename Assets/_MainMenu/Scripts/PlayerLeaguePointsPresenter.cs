using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PlayerLeaguePointsPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _pointsText;

    bool _initialized;
    bool _subscribed;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WireAllPresenters()
    {
        RefreshAllPresenters();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshAllPresenters();
    }

    static void RefreshAllPresenters()
    {
        PlayerLeaguePointsPresenter[] presenters = Object.FindObjectsByType<PlayerLeaguePointsPresenter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < presenters.Length; i++)
        {
            if (presenters[i].gameObject.scene.isLoaded)
            {
                presenters[i].EnsureInitialized();
            }
        }
    }

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        EnsureInitialized();
        Refresh();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
        {
            EnsureInitialized();
            Refresh();
        }
    }

    public void EnsureInitialized()
    {
        if (_initialized)
        {
            Refresh();
            return;
        }

        _initialized = true;
        ResolveReferences();
        StartCoroutine(BindWhenReady());
    }

    IEnumerator BindWhenReady()
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
        if (_subscribed || LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.StandingsUpdated += Refresh;
        _subscribed = true;
    }

    void Unsubscribe()
    {
        if (!_subscribed || LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.StandingsUpdated -= Refresh;
        _subscribed = false;
    }

    void ResolveReferences()
    {
        if (_pointsText != null)
        {
            return;
        }

        Transform texts = transform.Find("Texts");
        if (texts != null)
        {
            Transform puanText = texts.Find("PuanText");
            if (puanText != null)
            {
                _pointsText = puanText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_pointsText == null)
        {
            TextMeshProUGUI[] labels = GetComponentsInChildren<TextMeshProUGUI>(true);
            for (int i = 0; i < labels.Length; i++)
            {
                if (labels[i].name == "PuanText")
                {
                    _pointsText = labels[i];
                    break;
                }
            }
        }
    }

    void Refresh()
    {
        ResolveReferences();

        if (_pointsText == null)
        {
            return;
        }

        _pointsText.SetText($"{LeagueService.Instance?.GetPlayerPoints() ?? 0}");
    }
}
