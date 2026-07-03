using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WalletPresenter : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI coinsLabel;
    [SerializeField] TextMeshProUGUI levelLabel;
    [SerializeField] Image progressFillImage;

    void OnEnable()
    {
        Refresh();
        WalletService.Changed += Refresh;
    }

    void OnDisable()
    {
        WalletService.Changed -= Refresh;
    }

    void Refresh()
    {
        if (coinsLabel != null)
            coinsLabel.text = WalletService.TotalCoins.ToString();

        if (levelLabel != null)
            levelLabel.text = "Level " + WalletService.Level.ToString();

        if (progressFillImage != null)
            progressFillImage.fillAmount = WalletService.LevelProgress;
    }
}
