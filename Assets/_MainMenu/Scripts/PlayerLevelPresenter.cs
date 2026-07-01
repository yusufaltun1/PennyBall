using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerLevelPresenter : MonoBehaviour
{
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
        if (levelLabel != null)
            levelLabel.text = WalletService.Level.ToString();

        if (progressFillImage != null)
            progressFillImage.fillAmount = WalletService.LevelProgress;
    }
}
