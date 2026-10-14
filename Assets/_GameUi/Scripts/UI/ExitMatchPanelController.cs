using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ScoreBar'daki ExitButton ile açılan "maçtan çık" onay paneli.
/// Btn_yes: online ise rakibe forfeit bildirilir, maç hükmen kaybedilir (ResultPanel yenilgiyle açılır).
/// Btn_cancel: paneli kapatır, maç devam eder.
/// </summary>
[DisallowMultipleComponent]
public class ExitMatchPanelController : MonoBehaviour
{
    const string ForfeitReason = "exit_button";

    [SerializeField] Button _exitButton;
    [SerializeField] Button _cancelButton;
    [SerializeField] Button _yesButton;
    [SerializeField] TextMeshProUGUI _titleText;
    [SerializeField] TextMeshProUGUI _subtitleText;

    void Awake()
    {
        ResolveReferences();
        BindButtons();
        ApplyLocalization();
    }

    void OnEnable()
    {
        LocalizationService.LanguageChanged += ApplyLocalization;
        ApplyLocalization();
    }

    void OnDisable()
    {
        LocalizationService.LanguageChanged -= ApplyLocalization;
    }

    void OnDestroy()
    {
        UnbindButtons();
    }

    public static ExitMatchPanelController FindPanel()
    {
        ExitMatchPanelController existing =
            FindFirstObjectByType<ExitMatchPanelController>(FindObjectsInactive.Include);
        if (existing != null)
        {
            return existing;
        }

        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform found = FindDeepChild(roots[i].transform, "ExitMatch");
            if (found == null)
            {
                continue;
            }

            var controller = found.gameObject.AddComponent<ExitMatchPanelController>();

            // ExitMatch pasif (inactive) başladığı için AddComponent'in Awake()'i hemen
            // tetiklemesi garanti değil — ExitButton gibi dış (hep aktif) objelere bağlanmak
            // Awake'e bağlı kalamaz, burada açıkça çağırıyoruz.
            controller.ResolveReferences();
            controller.BindButtons();
            controller.ApplyLocalization();
            return controller;
        }

        return null;
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
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    void ResolveReferences()
    {
        if (_cancelButton == null)
        {
            Transform found = FindDeepChild(transform, "Btn_cancel");
            if (found != null)
            {
                _cancelButton = found.GetComponent<Button>();
            }
        }

        if (_yesButton == null)
        {
            Transform found = FindDeepChild(transform, "Btn_yes");
            if (found != null)
            {
                _yesButton = found.GetComponent<Button>();
            }
        }

        if (_exitButton == null)
        {
            Transform scoreBar = FindDeepChild(transform.root, "ScoreBar");
            if (scoreBar != null)
            {
                Transform found = FindDeepChild(scoreBar, "ExitButton");
                if (found != null)
                {
                    _exitButton = found.GetComponent<Button>();
                }
            }
        }

        if (_titleText == null || _subtitleText == null)
        {
            Transform image = transform.Find("Image");
            if (image != null)
            {
                if (_titleText == null)
                {
                    Transform found = image.Find("Text (TMP)");
                    if (found != null)
                    {
                        _titleText = found.GetComponent<TextMeshProUGUI>();
                    }
                }

                if (_subtitleText == null)
                {
                    Transform found = image.Find("Text (TMP) (1)");
                    if (found != null)
                    {
                        _subtitleText = found.GetComponent<TextMeshProUGUI>();
                    }
                }
            }
        }
    }

    void ApplyLocalization()
    {
        if (_titleText != null)
        {
            _titleText.text = LocalizationService.Get("exit_match.title");
        }

        if (_subtitleText != null)
        {
            _subtitleText.text = LocalizationService.Get("exit_match.subtitle");
        }

        SetButtonLabel(_cancelButton, "exit_match.cancel_button");
        SetButtonLabel(_yesButton, "exit_match.yes_button");
    }

    static void SetButtonLabel(Button button, string key)
    {
        if (button == null)
        {
            return;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = LocalizationService.Get(key);
        }
    }

    void BindButtons()
    {
        if (_exitButton != null)
        {
            _exitButton.onClick.RemoveListener(Show);
            _exitButton.onClick.AddListener(Show);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(OnCancelClicked);
            _cancelButton.onClick.AddListener(OnCancelClicked);
        }

        if (_yesButton != null)
        {
            _yesButton.onClick.RemoveListener(OnYesClicked);
            _yesButton.onClick.AddListener(OnYesClicked);
        }
    }

    void UnbindButtons()
    {
        if (_exitButton != null)
        {
            _exitButton.onClick.RemoveListener(Show);
        }

        if (_cancelButton != null)
        {
            _cancelButton.onClick.RemoveListener(OnCancelClicked);
        }

        if (_yesButton != null)
        {
            _yesButton.onClick.RemoveListener(OnYesClicked);
        }
    }

    void OnCancelClicked()
    {
        MainMenuClickSound.Play();
        Hide();
    }

    void OnYesClicked()
    {
        MainMenuClickSound.Play();
        Hide();

        if (OnlineMatchSession.IsOnlineMatch)
        {
            OnlineMatchSession.Channel?.SendForfeit(ForfeitReason);
        }

        LeagueMatchController.Instance?.ForfeitActiveMatch(ForfeitReason);
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
