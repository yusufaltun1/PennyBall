using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelStatusRowView : MonoBehaviour
{
    const float MinVisibleFillWidth = 27f;

    [SerializeField] Image rowBackground;
    [SerializeField] Image starImage;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] Image barBackground;
    [SerializeField] RectTransform barTrack;
    [SerializeField] RectTransform barFill;
    [SerializeField] Image barFillImage;
    [SerializeField] TextMeshProUGUI xpLabel;

    float _fullBarWidth;

    void Awake()
    {
        ResolveReferences();
    }

    public void ResolveReferences()
    {
        rowBackground ??= GetComponent<Image>();

        Transform star = transform.Find("Star");
        if (star != null)
        {
            starImage ??= star.GetComponent<Image>();
            levelLabel ??= star.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        Transform barBg = transform.Find("LevelBarBg");
        if (barBg != null)
        {
            barBackground ??= barBg.GetComponent<Image>();
            barTrack ??= barBg as RectTransform;

            Transform fill = barBg.Find("StatusBar");
            if (fill != null)
            {
                barFill ??= fill as RectTransform;
                barFillImage ??= fill.GetComponent<Image>();
            }
        }

        if (xpLabel == null)
        {
            Transform xp = transform.Find("XPNeeded");
            if (xp != null)
            {
                xpLabel = xp.GetComponent<TextMeshProUGUI>();
            }
        }

        CacheFullBarWidth();
    }

    void CacheFullBarWidth()
    {
        if (barTrack == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        _fullBarWidth = barTrack.rect.width;
        if (_fullBarWidth <= 0f)
        {
            _fullBarWidth = barTrack.sizeDelta.x;
        }
    }

    public void ApplyStyle(
        Sprite rowSprite,
        Sprite starSprite,
        Sprite barBackgroundSprite,
        Sprite barFillSprite,
        Color xpTextColor,
        FontStyles xpFontStyle)
    {
        if (rowBackground != null && rowSprite != null)
        {
            rowBackground.sprite = rowSprite;
        }

        if (starImage != null && starSprite != null)
        {
            starImage.sprite = starSprite;
        }

        if (barBackground != null && barBackgroundSprite != null)
        {
            barBackground.sprite = barBackgroundSprite;
        }

        if (barFillImage != null && barFillSprite != null)
        {
            barFillImage.sprite = barFillSprite;
            barFillImage.color = Color.white;
        }

        if (xpLabel != null)
        {
            xpLabel.color = xpTextColor;
            xpLabel.fontStyle = xpFontStyle;
        }
    }

    public void Bind(int level, int xpRequired, float progress, bool isMaxLevel, bool showMinFillAtZero = false)
    {
        ResolveReferences();

        if (levelLabel != null)
        {
            levelLabel.SetText(level.ToString());
        }

        if (xpLabel != null)
        {
            xpLabel.SetText(isMaxLevel ? "MAX" : xpRequired.ToString());
        }

        ApplyBarFill(isMaxLevel ? 1f : progress, showMinFillAtZero);
    }

    void ApplyBarFill(float progress, bool showMinFillAtZero)
    {
        if (barFill == null)
        {
            return;
        }

        CacheFullBarWidth();

        progress = Mathf.Clamp01(progress);
        if (progress <= 0f && !showMinFillAtZero)
        {
            if (barFillImage != null)
            {
                barFillImage.enabled = false;
            }

            barFill.sizeDelta = new Vector2(0f, barFill.sizeDelta.y);
            return;
        }

        if (barFillImage != null)
        {
            barFillImage.enabled = true;
        }

        float targetWidth = _fullBarWidth > 0f
            ? _fullBarWidth * progress
            : MinVisibleFillWidth;

        if (progress < 1f)
        {
            targetWidth = Mathf.Max(targetWidth, MinVisibleFillWidth);
        }

        barFill.sizeDelta = new Vector2(targetWidth, barFill.sizeDelta.y);
    }
}
