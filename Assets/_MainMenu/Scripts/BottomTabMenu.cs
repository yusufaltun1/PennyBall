using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class BottomTabMenu : MonoBehaviour
{
    [System.Serializable]
    public class TabItem
    {
        public Button button;
        public GameObject bgObject;
        public Image iconImage;
        public Sprite defaultSprite;
        public Sprite pressedSprite;
    }

    [Header("Panels Carousel")]
    [SerializeField] private RectTransform panelsContainer;
    [SerializeField] private float panelWidth = 1080f;
    [SerializeField] private int homeTabIndex = 1;
    [SerializeField] private float animationDuration = 0.45f;

    [Header("Tabs Configuration")]
    [SerializeField] private TabItem[] tabs;
    [SerializeField] private int initialActiveIndex = 1;

    private Coroutine slideCoroutine;
    private int activeIndex = -1;

    private void Start()
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            int index = i;
            if (tabs[i].button != null)
            {
                tabs[i].button.onClick.AddListener(() => SelectTab(index));
            }
        }

        if (panelsContainer != null)
        {
            panelsContainer.anchoredPosition = GetTargetPosition(homeTabIndex);
        }

        SelectTab(initialActiveIndex, immediate: true);
    }

    public void SelectTab(int index)
    {
        SelectTab(index, immediate: false);
    }

    private void SelectTab(int index, bool immediate)
    {
        if (tabs == null || tabs.Length == 0)
        {
            return;
        }

        index = Mathf.Clamp(index, 0, tabs.Length - 1);

        UpdateTabVisuals(index);

        if (panelsContainer == null || index == activeIndex)
        {
            return;
        }

        activeIndex = index;
        Vector2 targetPosition = GetTargetPosition(index);

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
        }

        if (immediate || animationDuration <= 0f)
        {
            panelsContainer.anchoredPosition = targetPosition;
            return;
        }

        slideCoroutine = StartCoroutine(SlideToPosition(targetPosition));
    }

    private void UpdateTabVisuals(int activeTabIndex)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            if (tab == null)
            {
                continue;
            }

            bool isActive = i == activeTabIndex;

            if (tab.bgObject != null)
            {
                tab.bgObject.SetActive(isActive);
            }

            if (tab.iconImage != null)
            {
                tab.iconImage.sprite = isActive ? tab.pressedSprite : tab.defaultSprite;
            }
        }
    }

    private Vector2 GetTargetPosition(int index)
    {
        float targetX = (homeTabIndex - index) * panelWidth;
        float currentY = panelsContainer != null ? panelsContainer.anchoredPosition.y : 0f;
        return new Vector2(targetX, currentY);
    }

    private IEnumerator SlideToPosition(Vector2 targetPosition)
    {
        Vector2 startPosition = panelsContainer.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / animationDuration);
            float easedT = EaseOutBack(t);
            panelsContainer.anchoredPosition = Vector2.LerpUnclamped(startPosition, targetPosition, easedT);
            yield return null;
        }

        panelsContainer.anchoredPosition = targetPosition;
        slideCoroutine = null;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
