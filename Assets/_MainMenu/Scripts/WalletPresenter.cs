using TMPro;
using UnityEngine;

public class WalletPresenter : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI coinsLabel;
    [SerializeField] private TextMeshProUGUI xpLabel;

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
        if (xpLabel != null)
            xpLabel.text = WalletService.TotalXp.ToString();
    }
}
