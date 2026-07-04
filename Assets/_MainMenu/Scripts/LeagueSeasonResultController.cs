using UnityEngine;

/// <summary>
/// Main menü açılışında sezon sonucunu kontrol eder ve LeagueChange panelini gösterir.
/// </summary>
[DisallowMultipleComponent]
public class LeagueSeasonResultController : MonoBehaviour
{
    [SerializeField] LeagueChangePresenter _leagueChangePanel;

    void Start()
    {
        ResolvePanel();
        TryShowPendingResult();
    }

    void ResolvePanel()
    {
        if (_leagueChangePanel != null)
        {
            return;
        }

        _leagueChangePanel = FindFirstObjectByType<LeagueChangePresenter>(FindObjectsInactive.Include);
        if (_leagueChangePanel != null)
        {
            return;
        }

        Transform canvas = transform.root;
        Transform found = FindDeepChild(canvas, "LeagueChange");
        if (found != null)
        {
            _leagueChangePanel = found.GetComponent<LeagueChangePresenter>();
            if (_leagueChangePanel == null)
            {
                _leagueChangePanel = found.gameObject.AddComponent<LeagueChangePresenter>();
            }
        }
    }

    public void TryShowPendingResult()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.ResolveSeasonIfNeeded();

        if (_leagueChangePanel == null)
        {
            if (LeagueService.Instance.HasPendingSeasonResult)
            {
                Debug.LogWarning(
                    "[League] Sezon sonucu bekliyor ama LeagueChange paneli sahnede yok. " +
                    "MainMenu Canvas altına LeagueChange panelini ekleyin.");
            }

            return;
        }

        _leagueChangePanel.ShowIfPending();
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent == null)
        {
            return null;
        }

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
