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
        public GameObject panelObject;
    }

    [Header("Tabs Configuration")]
    [SerializeField] private TabItem[] tabs;
    [SerializeField] private int initialActiveIndex = 1; // Default to Home (Index 1)

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

        // Initialize state
        SelectTab(initialActiveIndex);
    }

    public void SelectTab(int activeIndex)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            var tab = tabs[i];
            if (tab == null) continue;

            bool isActive = (i == activeIndex);

            // Toggle background object
            if (tab.bgObject != null)
            {
                tab.bgObject.SetActive(isActive);
            }

            // Toggle panel object
            if (tab.panelObject != null)
            {
                tab.panelObject.SetActive(isActive);
            }

            // Swap icon sprite
            if (tab.iconImage != null)
            {
                tab.iconImage.sprite = isActive ? tab.pressedSprite : tab.defaultSprite;
            }
        }
    }
}
