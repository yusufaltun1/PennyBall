using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// LevelStatus paneli: Top_Br_Indicators içindeki LevelBar'a tıklanınca
/// Settings_Popup ile aynı kayma animasyonuyla açılır; Btn_Close veya Overlay
/// ile aynı animasyonla kapanır. Wrapper içeriği panel içinde dikey scroll edilir.
/// </summary>
public class LevelStatusPopupController : MonoBehaviour
{
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button overlayCloseButton;
    [SerializeField] private RectTransform slidePanel;
    [SerializeField] private float animationDuration = 0.4f;
    [SerializeField] private float openBottomMargin;
    [SerializeField] private float hiddenPadding = 40f;
    [SerializeField] private float scrollSensitivity = 30f;

    private RectTransform rectTransform;
    private ScrollRect scrollRect;
    private float closedY;
    private float openY;
    private Coroutine animationCoroutine;
    private bool isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RegisterSceneHook()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AttachToLevelStatusPanels();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        AttachToLevelStatusPanels();
    }

    static void AttachToLevelStatusPanels()
    {
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                Transform[] transforms = roots[i].GetComponentsInChildren<Transform>(true);
                for (int j = 0; j < transforms.Length; j++)
                {
                    if (transforms[j].name != "LevelStatus" || transforms[j].Find("PanelRoot") == null)
                    {
                        continue;
                    }

                    GameObject panelObject = transforms[j].gameObject;
                    LevelStatusPopupController controller = panelObject.GetComponent<LevelStatusPopupController>();
                    if (controller == null)
                    {
                        controller = panelObject.AddComponent<LevelStatusPopupController>();
                    }

                    // Panel sahnede kapalıyken Awake çalışmaz; LevelBar'ı buradan bağla.
                    controller.EnsureInitialized();
                }
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
        EnsureScrollSetup();
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

    void ResolveButtons()
    {
        if (openButton == null)
        {
            // Sadece üst bardaki LevelBar — sahnedeki diğer LevelBar kopyalarına bağlanma.
            openButton = FindOrCreateLevelBarButton(gameObject.scene);
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
                overlayCloseButton = GetOrCreateButton(overlayTransform.gameObject);
            }
        }
    }

    static Button FindOrCreateLevelBarButton(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return null;
        }

        GameObject[] roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            Transform topBar = FindDeepChild(roots[i].transform, "Top_Bar_Indicators");
            if (topBar == null)
            {
                continue;
            }

            Transform levelBar = FindDeepChild(topBar, "LevelBar");
            if (levelBar == null)
            {
                continue;
            }

            Button button = GetOrCreateButton(levelBar.gameObject);
            if (button != null)
            {
                // Çocuk görseller tıklamayı bölmesin; hit alanı LevelBar'ın kendi Image'ı olsun.
                DisableChildRaycasts(levelBar);
                return button;
            }
        }

        return null;
    }

    static void DisableChildRaycasts(Transform root)
    {
        Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i].transform == root)
            {
                graphics[i].raycastTarget = true;
                continue;
            }

            graphics[i].raycastTarget = false;
        }
    }

    static Button GetOrCreateButton(GameObject target)
    {
        Button button = target.GetComponent<Button>();
        if (button != null)
        {
            return button;
        }

        Image image = target.GetComponent<Image>();
        if (image == null)
        {
            return null;
        }

        button = target.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;
        return button;
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
    }

    /// <summary>
    /// Wrapper'ı viewport (RectMask2D'li sabit pencere) yapar; içindeki satırları
    /// runtime'da oluşturulan "Content" child'ına taşır ve onu ScrollRect ile
    /// dikey kaydırır. Wrapper alanı dışına taşan içerik maske ile gizlenir.
    /// </summary>
    void EnsureScrollSetup()
    {
        if (scrollRect != null)
        {
            return;
        }

        Transform wrapper = FindDeepChild(transform, "Wrapper");
        var viewport = wrapper as RectTransform;
        if (viewport == null)
        {
            Debug.LogWarning("[LevelStatus] Wrapper bulunamadı; scroll kurulamadı.", this);
            return;
        }

        ScrollRect existing = viewport.GetComponent<ScrollRect>();
        if (existing != null && existing.content != null)
        {
            scrollRect = existing;
            return;
        }

        if (viewport.GetComponent<RectMask2D>() == null)
        {
            viewport.gameObject.AddComponent<RectMask2D>();
        }

        // Satır aralarındaki boşluklarda da drag yakalansın diye görünmez bir grafik.
        if (viewport.GetComponent<Graphic>() == null)
        {
            Image dragCatcher = viewport.gameObject.AddComponent<Image>();
            dragCatcher.color = Color.clear;
            dragCatcher.raycastTarget = true;
        }

        RectTransform content = CreateScrollContent(viewport);

        scrollRect = existing != null ? existing : viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.content = content;
        scrollRect.viewport = viewport;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.elasticity = 0.1f;
        scrollRect.inertia = true;
        scrollRect.scrollSensitivity = scrollSensitivity;
    }

    static RectTransform CreateScrollContent(RectTransform viewport)
    {
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = Vector2.zero;
        content.anchoredPosition = Vector2.zero;

        // Satırları Content altına taşı (Content kendisi hariç).
        for (int i = viewport.childCount - 1; i >= 0; i--)
        {
            Transform child = viewport.GetChild(i);
            if (child != content)
            {
                child.SetParent(content, false);
                child.SetAsFirstSibling();
            }
        }

        MoveVerticalLayoutToContent(viewport, content);

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        return content;
    }

    static void MoveVerticalLayoutToContent(RectTransform viewport, RectTransform content)
    {
        VerticalLayoutGroup source = viewport.GetComponent<VerticalLayoutGroup>();
        VerticalLayoutGroup target = content.gameObject.AddComponent<VerticalLayoutGroup>();

        if (source != null)
        {
            target.padding = source.padding;
            target.spacing = source.spacing;
            target.childAlignment = source.childAlignment;
            target.reverseArrangement = source.reverseArrangement;
            target.childControlWidth = source.childControlWidth;
            target.childControlHeight = source.childControlHeight;
            target.childScaleWidth = source.childScaleWidth;
            target.childScaleHeight = source.childScaleHeight;
            target.childForceExpandWidth = source.childForceExpandWidth;
            target.childForceExpandHeight = source.childForceExpandHeight;

            // Viewport'ta layout kalırsa Content'in pozisyonunu ezer; kaldır.
            DestroyImmediate(source);
        }
        else
        {
            target.childControlWidth = true;
            target.childControlHeight = false;
            target.childForceExpandWidth = true;
            target.childForceExpandHeight = false;
        }
    }

    static Transform FindDeepChild(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i] != root && children[i].name == childName)
            {
                return children[i];
            }
        }

        return null;
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
        HidePanelOffScreen();

        yield return null;

        Canvas.ForceUpdateCanvases();
        ResetScrollToTop();
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

    void ResetScrollToTop()
    {
        if (scrollRect != null)
        {
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
    }

    void ResetRootPosition()
    {
        rectTransform.anchoredPosition = Vector2.zero;
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
        Canvas canvas = GetComponentInParent<Canvas>(true);

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
