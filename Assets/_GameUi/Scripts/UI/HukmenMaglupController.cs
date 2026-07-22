using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Hükmen mağlubiyet paneli — Continue ile Main Menu'ye döner.
/// </summary>
[DisallowMultipleComponent]
public class HukmenMaglupController : MonoBehaviour
{
    [SerializeField] Button _continueButton;

    bool _buttonsBound;

    void Awake()
    {
        ResolveReferences();
        BindButtons();
    }

    void OnDestroy()
    {
        UnbindButtons();
    }

    public void Show()
    {
        ResolveReferences();
        BindButtons();
        transform.SetAsLastSibling();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        MatchScoreboardPresenter.RefreshAll();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public static HukmenMaglupController FindPanel()
    {
        HukmenMaglupController existing =
            FindFirstObjectByType<HukmenMaglupController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, "HukmenMaglup");
            if (found == null)
            {
                continue;
            }

            existing = found.GetComponent<HukmenMaglupController>();
            if (existing == null)
            {
                // Scene'de eski ResultPanelController kalmış olabilir; çakışmayı engelle.
                ResultPanelController legacy = found.GetComponent<ResultPanelController>();
                if (legacy != null)
                {
                    legacy.enabled = false;
                }

                existing = found.gameObject.AddComponent<HukmenMaglupController>();
            }

            return existing;
        }

        return null;
    }

    void ResolveReferences()
    {
        if (_continueButton == null)
        {
            Transform found = FindDeepChild(transform, "Btn_Continue");
            if (found != null)
            {
                _continueButton = found.GetComponent<Button>();
            }
        }
    }

    void BindButtons()
    {
        if (_buttonsBound)
        {
            return;
        }

        if (_continueButton != null)
        {
            _continueButton.onClick.AddListener(OnContinueClicked);
            _buttonsBound = true;
        }
    }

    void UnbindButtons()
    {
        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
        }

        _buttonsBound = false;
    }

    void OnContinueClicked()
    {
        MainMenuClickSound.Play();
        Hide();
        SceneManager.LoadScene(GameSceneNames.MainMenu);
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
