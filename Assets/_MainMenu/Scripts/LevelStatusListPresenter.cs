using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelStatusListPresenter : MonoBehaviour
{
    /// <summary>Aktif seviye + bundan sonraki gösterilecek seviye sayısı.</summary>
    const int ExtraLevelsAhead = 19;
    const int TargetRowCount = 1 + ExtraLevelsAhead;

    static readonly Color ActiveXpColor = Color.white;
    static readonly Color PassiveXpColor = new(0.5921569f, 0.5294118f, 0.74509805f, 1f);

    [SerializeField] Transform rowsRoot;
    [SerializeField] Sprite activeRowSprite;
    [SerializeField] Sprite passiveRowSprite;
    [SerializeField] Sprite activeStarSprite;
    [SerializeField] Sprite passiveStarSprite;
    [SerializeField] Sprite activeBarBackgroundSprite;
    [SerializeField] Sprite passiveBarBackgroundSprite;
    [SerializeField] Sprite activeBarFillSprite;
    [SerializeField] Sprite passiveBarFillSprite;

    readonly List<LevelStatusRowView> _rows = new();

    void Awake()
    {
        EnsureRows();
        CaptureStyleSpritesFromScene();
    }

    void OnEnable()
    {
        WalletService.Changed += Refresh;
        WalletService.LevelChanged += OnLevelChanged;
        Refresh();
    }

    void OnDisable()
    {
        WalletService.Changed -= Refresh;
        WalletService.LevelChanged -= OnLevelChanged;
    }

    void OnLevelChanged(int levelBefore, int levelAfter)
    {
        Refresh();
    }

    public void Refresh()
    {
        EnsureRows();

        int currentLevel = WalletService.Level;
        int totalXp = WalletService.TotalXp;

        for (int i = 0; i < _rows.Count; i++)
        {
            LevelStatusRowView row = _rows[i];
            int level = currentLevel + i;

            if (level > PlayerLevelProgression.MaxLevel)
            {
                row.gameObject.SetActive(false);
                continue;
            }

            row.gameObject.SetActive(true);
            bool isCurrentLevel = i == 0;
            bool isMaxLevel = level >= PlayerLevelProgression.MaxLevel;
            int xpRequired = PlayerLevelProgression.GetXpRequiredForNextLevel(level);
            float progress = isMaxLevel
                ? 1f
                : isCurrentLevel
                    ? WalletService.LevelProgress
                    : 0f;

            row.ApplyStyle(
                isCurrentLevel ? activeRowSprite : passiveRowSprite,
                isCurrentLevel ? activeStarSprite : passiveStarSprite,
                isCurrentLevel ? activeBarBackgroundSprite : passiveBarBackgroundSprite,
                isCurrentLevel ? activeBarFillSprite : passiveBarFillSprite,
                isCurrentLevel ? ActiveXpColor : PassiveXpColor,
                isCurrentLevel ? FontStyles.Bold : FontStyles.Normal);

            row.Bind(level, xpRequired, progress, isMaxLevel, showMinFillAtZero: isCurrentLevel);
        }
    }

    Transform ResolveRowsRoot()
    {
        Transform root = rowsRoot != null ? rowsRoot : transform;
        Transform content = root.Find("Content");
        return content != null ? content : root;
    }

    void EnsureRows()
    {
        if (_rows.Count >= TargetRowCount)
        {
            return;
        }

        Transform searchRoot = ResolveRowsRoot();
        CollectExistingRows(searchRoot);

        if (_rows.Count == 0)
        {
            return;
        }

        LevelStatusRowView template = _rows[_rows.Count > 1 ? 1 : 0];
        while (_rows.Count < TargetRowCount)
        {
            GameObject clone = Instantiate(template.gameObject, searchRoot);
            clone.name = $"LevelInfo2 ({_rows.Count})";
            clone.SetActive(true);

            LevelStatusRowView row = clone.GetComponent<LevelStatusRowView>();
            if (row == null)
            {
                row = clone.AddComponent<LevelStatusRowView>();
            }

            row.ResolveReferences();
            _rows.Add(row);
        }
    }

    void CollectExistingRows(Transform searchRoot)
    {
        if (_rows.Count > 0)
        {
            return;
        }

        for (int i = 0; i < searchRoot.childCount; i++)
        {
            Transform child = searchRoot.GetChild(i);
            if (!child.name.StartsWith("LevelInfo"))
            {
                continue;
            }

            LevelStatusRowView row = child.GetComponent<LevelStatusRowView>();
            if (row == null)
            {
                row = child.gameObject.AddComponent<LevelStatusRowView>();
            }

            row.ResolveReferences();
            _rows.Add(row);
        }
    }

    void CaptureStyleSpritesFromScene()
    {
        if (_rows.Count == 0)
        {
            return;
        }

        CaptureRowStyle(_rows[0], ref activeRowSprite, ref activeStarSprite, ref activeBarBackgroundSprite, ref activeBarFillSprite);

        if (_rows.Count > 1)
        {
            CaptureRowStyle(_rows[1], ref passiveRowSprite, ref passiveStarSprite, ref passiveBarBackgroundSprite, ref passiveBarFillSprite);
        }
        else
        {
            passiveRowSprite = activeRowSprite;
            passiveStarSprite = activeStarSprite;
            passiveBarBackgroundSprite = activeBarBackgroundSprite;
            passiveBarFillSprite = activeBarFillSprite;
        }
    }

    static void CaptureRowStyle(
        LevelStatusRowView row,
        ref Sprite rowSprite,
        ref Sprite starSprite,
        ref Sprite barBackgroundSprite,
        ref Sprite barFillSprite)
    {
        if (rowSprite != null)
        {
            return;
        }

        Image rowBackground = row.GetComponent<Image>();
        if (rowBackground != null)
        {
            rowSprite = rowBackground.sprite;
        }

        Transform star = row.transform.Find("Star");
        if (star != null)
        {
            Image starImage = star.GetComponent<Image>();
            if (starImage != null)
            {
                starSprite = starImage.sprite;
            }
        }

        Transform barBg = row.transform.Find("LevelBarBg");
        if (barBg != null)
        {
            Image barBackground = barBg.GetComponent<Image>();
            if (barBackground != null)
            {
                barBackgroundSprite = barBackground.sprite;
            }

            Transform fill = barBg.Find("StatusBar");
            if (fill != null)
            {
                Image fillImage = fill.GetComponent<Image>();
                if (fillImage != null)
                {
                    barFillSprite = fillImage.sprite;
                }
            }
        }
    }
}
