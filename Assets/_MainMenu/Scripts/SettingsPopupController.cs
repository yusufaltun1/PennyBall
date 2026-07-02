using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPopupController : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
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
    private bool _initialized;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void WireInactivePopups()
    {
        SettingsPopupController[] controllers = Object.FindObjectsByType<SettingsPopupController>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            if (controllers[i].gameObject.scene.isLoaded)
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
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        rectTransform = GetComponent<RectTransform>();
        ResolveSlidePanel();
        BindFeedbackSettings();

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
        _settingsBound = true;
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
    }

    public void Open()
    {
        if (isOpen)
        {
            return;
        }

        MainMenuClickSound.Play();
        isOpen = true;
        gameObject.SetActive(true);
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
