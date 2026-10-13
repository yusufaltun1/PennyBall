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
        HideOtherResultPanels();
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

    /// <summary>
    /// Önceki bir maç döngüsünden kalmış olabilecek diğer sonuç panellerini (kapatılmadan
    /// bırakılmış ResultPanel/LeagueStatus/LevelUp/UnlockedFeatures) kapatır — aksi halde
    /// HukmenMaglup ile aynı anda üst üste görünebiliyorlardı.
    /// </summary>
    static void HideOtherResultPanels()
    {
        HideIfActive(FindFirstObjectByType<ResultPanelController>(FindObjectsInactive.Include)?.gameObject);
        HideIfActive(FindFirstObjectByType<LeagueStatusPresenter>(FindObjectsInactive.Include)?.gameObject);
        HideIfActive(FindFirstObjectByType<LevelUpController>(FindObjectsInactive.Include)?.gameObject);
        HideIfActive(FindFirstObjectByType<UnlockedFeaturesController>(FindObjectsInactive.Include)?.gameObject);
    }

    static void HideIfActive(GameObject go)
    {
        if (go != null && go.activeSelf)
        {
            go.SetActive(false);
        }
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
        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
            _continueButton.onClick.AddListener(OnContinueClicked);
        }
    }

    void UnbindButtons()
    {
        if (_continueButton != null)
        {
            _continueButton.onClick.RemoveListener(OnContinueClicked);
        }
    }

    void OnContinueClicked()
    {
        MainMenuClickSound.Play();
        Hide();

        // HukmenMaglup sadece "pes ettin" bildirimi — asıl yenilgi akışı (skor, ödül,
        // seviye/lig zinciri) ResultPanel'in kendi Continue akışına devrediliyor.
        ResultPanelController resultPanel =
            FindFirstObjectByType<ResultPanelController>(FindObjectsInactive.Include);
        if (resultPanel != null)
        {
            resultPanel.ShowResult(MatchResultType.Loss);
            return;
        }

        Debug.LogWarning("[HukmenMaglup] ResultPanel bulunamadı — doğrudan lig/ana menüye düşülüyor.");
        BoosterUnlockFlow.TryShowPendingUnlock(ShowLeagueStatusOrMainMenu);
    }

    static void ShowLeagueStatusOrMainMenu()
    {
        LeagueStatusPresenter presenter =
            FindFirstObjectByType<LeagueStatusPresenter>(FindObjectsInactive.Include);
        if (presenter != null)
        {
            presenter.gameObject.SetActive(true);
        }
        else
        {
            AdsService.GoToMainMenuMaybeWithInterstitial();
        }
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
