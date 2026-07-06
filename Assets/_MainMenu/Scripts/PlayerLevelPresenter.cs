using TMPro;
using UnityEngine;

public class PlayerLevelPresenter : MonoBehaviour
{
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
        Refresh();
        WalletService.Changed += Refresh;
    }

    void OnDisable()
    {
        WalletService.Changed -= Refresh;
    }

    void ResolveReferences()
    {
        if (levelBarFill == null)
        {
            Transform levelStatus = transform.Find("LevelStatus");
            if (levelStatus != null)
            {
                levelBarFill = levelStatus.GetComponent<RectTransform>();
            }
        }

        if (levelLabel == null)
        {
            Transform levelText = transform.Find("LeveltText");
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
        if (levelLabel != null)
        {
            levelLabel.text = WalletService.Level.ToString();
        }

        ApplyLevelBarProgress(WalletService.LevelProgress);
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
