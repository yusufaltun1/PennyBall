using TMPro;
using UnityEngine;

public class WalletPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI coinsLabel;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] LevelXpBarFill levelXpBarFill;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        Refresh();
        WalletService.Changed += Refresh;
        WalletService.LevelChanged += OnLevelChanged;
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

    void ResolveReferences()
    {
        if (levelLabel == null)
        {
            Transform levelText = transform.Find("LevelBarContainer/LevelBar/LeveltText");
            if (levelText != null)
            {
                levelLabel = levelText.GetComponent<TextMeshProUGUI>();
            }
        }

        if (levelXpBarFill == null)
        {
            Transform levelStatus = transform.Find("LevelBarContainer/LevelBar/FillTrack/LevelStatus")
                ?? transform.Find("LevelBarContainer/LevelBar/LevelStatus");
            if (levelStatus != null)
            {
                levelXpBarFill = levelStatus.GetComponent<LevelXpBarFill>();
            }
        }
    }

    void Refresh()
    {
        if (coinsLabel != null)
        {
            coinsLabel.text = WalletService.TotalCoins.ToString();
        }

        if (levelLabel != null)
        {
            levelLabel.text = WalletService.Level.ToString();
        }

        levelXpBarFill?.Refresh();
    }
}
