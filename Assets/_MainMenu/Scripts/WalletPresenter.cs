using TMPro;
using UnityEngine;

public class WalletPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI coinsLabel;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] RectTransform levelBarFill;

    float _fullAnchorMaxX;
    bool _barSizeCaptured;

    void Awake()
    {
        ResolveReferences();
        CaptureBarFullSize();
    }

    void OnEnable()
    {
        WalletService.Changed += Refresh;
        Refresh();
    }

    void Start()
    {
        Refresh();
    }

    void OnDisable()
    {
        WalletService.Changed -= Refresh;
    }

    void ResolveReferences()
    {
        if (levelBarFill == null)
        {
            Transform levelStatus = transform.Find("LevelBarContainer/LevelBar/LevelStatus");
            if (levelStatus != null)
            {
                levelBarFill = levelStatus.GetComponent<RectTransform>();
            }
        }

        if (levelLabel == null)
        {
            Transform levelText = transform.Find("LevelBarContainer/LevelBar/LeveltText");
            if (levelText != null)
            {
                levelLabel = levelText.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    void CaptureBarFullSize()
    {
        if (levelBarFill == null || _barSizeCaptured)
        {
            return;
        }

        _fullAnchorMaxX = levelBarFill.anchorMax.x;
        _barSizeCaptured = true;
    }

    void Refresh()
    {
        if (coinsLabel != null)
        {
            coinsLabel.text = WalletService.TotalCoins.ToString();
        }

        UpdateLevelLabel();
        ApplyLevelBarProgress(WalletService.LevelProgress);
    }

    void UpdateLevelLabel()
    {
        if (levelLabel == null)
        {
            return;
        }

        levelLabel.SetText(WalletService.Level.ToString());
    }

    void ApplyLevelBarProgress(float progress)
    {
        if (levelBarFill == null)
        {
            return;
        }

        CaptureBarFullSize();

        progress = Mathf.Clamp01(progress);
        Vector2 anchorMax = levelBarFill.anchorMax;
        anchorMax.x = Mathf.Lerp(levelBarFill.anchorMin.x, _fullAnchorMaxX, progress);
        levelBarFill.anchorMax = anchorMax;
    }
}
