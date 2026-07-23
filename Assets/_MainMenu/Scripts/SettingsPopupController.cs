using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsPopupController : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button overlayCloseButton;
    [SerializeField] private RectTransform slidePanel;
    [SerializeField] private float animationDuration = 0.4f;
    [SerializeField] private float openBottomMargin;
    [SerializeField] private float hiddenPadding = 40f;

    private RectTransform rectTransform;
    private float closedY;
    private float openY;
    private Coroutine animationCoroutine;
    private bool isOpen;
    private bool _settingsBound;
    private bool _buttonsWired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        WirePopupsInLoadedScenes();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        WirePopupsInScene(scene);
    }

    static void WirePopupsInLoadedScenes()
    {
        SettingsPopupController[] controllers = Object.FindObjectsByType<SettingsPopupController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            controllers[i].EnsureInitialized();
        }
    }

    static void WirePopupsInScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        SettingsPopupController[] controllers = Object.FindObjectsByType<SettingsPopupController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i].gameObject.scene == scene)
            {
                controllers[i].EnsureInitialized();
            }
        }
    }

    private void Awake()
    {
        EnsureInitialized();

        if (!isOpen)
        {
            PrepareClosedState();
            gameObject.SetActive(false);
        }
    }

    void EnsureInitialized()
    {
        rectTransform ??= GetComponent<RectTransform>();
        ResolveSlidePanel();
        ResolveButtons();
        WireOpenCloseButtons();
        BindFeedbackSettings();
    }

    void ResolveButtons()
    {
        if (openButton == null)
        {
            openButton = FindButtonInScene(gameObject.scene, "Settings");
        }

        if (closeButton == null)
        {
            Transform closeTransform = transform.Find("Btn_Close");
            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }
        }

        if (overlayCloseButton == null)
        {
            Transform overlayTransform = transform.Find("Overlay");
            if (overlayTransform != null)
            {
                overlayCloseButton = overlayTransform.GetComponent<Button>();
                if (overlayCloseButton == null)
                {
                    Image overlayImage = overlayTransform.GetComponent<Image>();
                    if (overlayImage != null)
                    {
                        overlayCloseButton = overlayTransform.gameObject.AddComponent<Button>();
                        overlayCloseButton.transition = Selectable.Transition.None;
                        overlayCloseButton.targetGraphic = overlayImage;
                    }
                }
            }
        }
    }

    static Button FindButtonInScene(Scene scene, string objectName)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
            for (int j = 0; j < transforms.Length; j++)
            {
                if (transforms[j].name != objectName)
                {
                    continue;
                }

                Button button = transforms[j].GetComponent<Button>();
                if (button != null)
                {
                    return button;
                }
            }
        }

        return null;
    }

    void WireOpenCloseButtons()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
            closeButton.onClick.AddListener(Close);
        }

        if (overlayCloseButton != null)
        {
            overlayCloseButton.onClick.RemoveListener(Close);
            overlayCloseButton.onClick.AddListener(Close);
        }

        _buttonsWired = openButton != null;
    }

    void ResolveSlidePanel()
    {
        if (slidePanel != null)
        {
            return;
        }

        Transform panel = transform.Find("PanelRoot");
        if (panel != null)
        {
            slidePanel = panel as RectTransform;
        }
    }

    void ResetRootPosition()
    {
        rectTransform.anchoredPosition = Vector2.zero;
    }

    void BindFeedbackSettings()
    {
        if (_settingsBound)
        {
            RefreshAllToggles();
            return;
        }

        GameFeedbackSettingsService.EnsureLoaded();
        BindToggle("PanelRoot/Container/Wrapper/Control-Music/Button", SettingsToggleControl.SettingKind.Music);
        BindToggle("PanelRoot/Container/Wrapper/Control-SoundEffects/Button", SettingsToggleControl.SettingKind.SoundEffects);
        BindToggle("PanelRoot/Container/Wrapper/Control-Vibrations/Button", SettingsToggleControl.SettingKind.Vibration);
        BindToggle("PanelRoot/Container/Wrapper/Control-Notifications/Button", SettingsToggleControl.SettingKind.Notification);
        BindTermsLink();
        ConfigureVersionLabel();
        _settingsBound = true;
    }

    const string PrivacyPolicyUrl = "https://orviadigital.com.tr/privacy";

    void BindTermsLink()
    {
        Transform termsTransform = transform.Find("PanelRoot/Container/Wrapper/Link-Terms");
        if (termsTransform == null)
        {
            Debug.LogWarning("[Settings] Link-Terms bulunamadı.");
            return;
        }

        Button termsButton = termsTransform.GetComponent<Button>();
        if (termsButton == null)
        {
            Image image = termsTransform.GetComponent<Image>();
            termsButton = termsTransform.gameObject.AddComponent<Button>();
            termsButton.transition = Selectable.Transition.None;
            if (image != null)
            {
                termsButton.targetGraphic = image;
            }
        }

        termsButton.onClick.RemoveListener(OpenPrivacyPolicy);
        termsButton.onClick.AddListener(OpenPrivacyPolicy);
    }

    void OpenPrivacyPolicy()
    {
        MainMenuClickSound.Play();
        Application.OpenURL(PrivacyPolicyUrl);
    }

    void RefreshAllToggles()
    {
        SettingsToggleControl[] toggles = GetComponentsInChildren<SettingsToggleControl>(true);
        for (int i = 0; i < toggles.Length; i++)
        {
            toggles[i].RefreshFromService();
        }
    }

    void BindToggle(string path, SettingsToggleControl.SettingKind kind)
    {
        Transform toggleTransform = transform.Find(path);
        if (toggleTransform == null)
        {
            Debug.LogWarning($"[Settings] Toggle bulunamadı: {path}");
            return;
        }

        SettingsToggleControl[] existing = toggleTransform.GetComponents<SettingsToggleControl>();
        for (int i = 1; i < existing.Length; i++)
        {
            Destroy(existing[i]);
        }

        SettingsToggleControl toggle = existing.Length > 0
            ? existing[0]
            : toggleTransform.gameObject.AddComponent<SettingsToggleControl>();

        toggle.Initialize(kind);
    }

    void ConfigureVersionLabel()
    {
        Transform versionTransform = transform.Find("PanelRoot/Container/Wrapper/VersionText");
        if (versionTransform == null)
        {
            return;
        }

        Button[] buttons = versionTransform.GetComponents<Button>();
        for (int i = 0; i < buttons.Length; i++)
        {
            Destroy(buttons[i]);
        }

        TextMeshProUGUI versionText = versionTransform.GetComponent<TextMeshProUGUI>();
        if (versionText == null)
        {
            return;
        }

        versionText.text = $"Version {Application.version}";
        versionText.raycastTarget = false;
    }

    private void OnDestroy()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        if (overlayCloseButton != null)
        {
            overlayCloseButton.onClick.RemoveListener(Close);
        }
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        EnsureInitialized();
        if (!_buttonsWired)
        {
            Debug.LogWarning("[Settings] Settings butonu bağlanamadı.", this);
            return;
        }

        MainMenuClickSound.Play();
        isOpen = true;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        if (closeButton != null)
        {
            closeButton.transform.SetAsLastSibling();
        }
        EnsureInitialized();

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(OpenSequence());
    }

    IEnumerator OpenSequence()
    {
        ResetRootPosition();
        RefreshAllToggles();
        HidePanelOffScreen();

        yield return null;

        Canvas.ForceUpdateCanvases();
        PrepareClosedState();
        yield return AnimateTo(openY, deactivateOnComplete: false);
        animationCoroutine = null;
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        MainMenuClickSound.Play();
        isOpen = false;
        CachePositions();

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimateTo(closedY, deactivateOnComplete: true));
    }

    void PrepareClosedState()
    {
        CachePositions();
        SetClosedPosition();
    }

    void HidePanelOffScreen()
    {
        ResolveSlidePanel();
        RectTransform target = slidePanel != null ? slidePanel : rectTransform;
        Vector2 position = target.anchoredPosition;
        position.y = -10000f;
        target.anchoredPosition = position;
    }

    private void CachePositions()
    {
        ResolveSlidePanel();
        ResetRootPosition();

        if (slidePanel == null)
        {
            float halfHeight = GetCanvasHalfHeight();
            closedY = halfHeight;
            openY = -halfHeight;
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(slidePanel);

        float panelHeight = slidePanel.rect.height;
        closedY = -panelHeight - hiddenPadding;
        openY = openBottomMargin;
    }

    private float GetCanvasHalfHeight()
    {
        const float defaultHalfHeight = 1170f;
        Canvas canvas = GetComponentInParent<Canvas>();

        if (canvas == null)
        {
            return defaultHalfHeight;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null || canvasRect.rect.height <= 0f)
        {
            return defaultHalfHeight;
        }

        return canvasRect.rect.height * 0.5f;
    }

    private void SetClosedPosition()
    {
        RectTransform target = slidePanel != null ? slidePanel : rectTransform;
        Vector2 position = target.anchoredPosition;
        position.y = closedY;
        target.anchoredPosition = position;
    }

    private IEnumerator AnimateTo(float targetY, bool deactivateOnComplete)
    {
        RectTransform target = slidePanel != null ? slidePanel : rectTransform;
        Vector2 startPosition = target.anchoredPosition;
        Vector2 endPosition = new Vector2(startPosition.x, targetY);
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float easedT = SmoothStep(t);
            target.anchoredPosition = Vector2.Lerp(startPosition, endPosition, easedT);
            yield return null;
        }

        target.anchoredPosition = endPosition;

        if (deactivateOnComplete)
        {
            gameObject.SetActive(false);
        }

        animationCoroutine = null;
    }

    private static float SmoothStep(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
