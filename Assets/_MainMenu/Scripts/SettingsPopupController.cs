using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPopupController : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private float animationDuration = 0.4f;

    private RectTransform rectTransform;
    private float closedY;
    private float openY;
    private Coroutine animationCoroutine;
    private bool isOpen;
    private bool _settingsBound;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        CachePositions();
        SetClosedPosition();
        BindFeedbackSettings();

        if (openButton != null)
        {
            openButton.onClick.AddListener(Open);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        if (!isOpen)
            gameObject.SetActive(false);
    }

    void BindFeedbackSettings()
    {
        if (_settingsBound)
        {
            RefreshAllToggles();
            return;
        }

        GameFeedbackSettingsService.EnsureLoaded();
        BindToggle("Container/Wrapper/Control-Music/Button", SettingsToggleControl.SettingKind.Music);
        BindToggle("Container/Wrapper/Control-SoundEffects/Button", SettingsToggleControl.SettingKind.SoundEffects);
        BindToggle("Container/Wrapper/Control-Vibrations/Button", SettingsToggleControl.SettingKind.Vibration);
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

        isOpen = true;
        gameObject.SetActive(true);
        RefreshAllToggles();

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimateTo(openY, deactivateOnComplete: false));
    }

    public void Close()
    {
        if (!isOpen)
        {
            return;
        }

        isOpen = false;

        if (animationCoroutine != null)
        {
            StopCoroutine(animationCoroutine);
        }

        animationCoroutine = StartCoroutine(AnimateTo(closedY, deactivateOnComplete: true));
    }

    private void CachePositions()
    {
        float halfHeight = GetCanvasHalfHeight();
        closedY = halfHeight;
        openY = -halfHeight;
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
        Vector2 position = rectTransform.anchoredPosition;
        position.y = closedY;
        rectTransform.anchoredPosition = position;
    }

    private IEnumerator AnimateTo(float targetY, bool deactivateOnComplete)
    {
        Vector2 startPosition = rectTransform.anchoredPosition;
        Vector2 endPosition = new Vector2(startPosition.x, targetY);
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float easedT = SmoothStep(t);
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, easedT);
            yield return null;
        }

        rectTransform.anchoredPosition = endPosition;

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
