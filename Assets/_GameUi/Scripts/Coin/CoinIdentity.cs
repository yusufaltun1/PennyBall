using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CoinDragController))]
public class CoinIdentity : MonoBehaviour
{
    [SerializeField] CoinTeam _team;
    [SerializeField] bool _autoDetectTeamFromName = true;

    CoinDragController _dragController;
    bool _isPassive;

    public CoinTeam Team => _team;
    public bool IsPassive => _isPassive;
    public CoinDragController DragController => _dragController;

    void Awake()
    {
        _dragController = GetComponent<CoinDragController>();

        if (_autoDetectTeamFromName)
        {
            DetectTeamFromName();
        }
    }

    void DetectTeamFromName()
    {
        string objectName = gameObject.name;
        if (objectName.Contains("_P"))
        {
            _team = CoinTeam.Player;
            return;
        }

        if (objectName.Contains("_E"))
        {
            _team = CoinTeam.Opponent;
        }
    }

    public void SetPassive(bool passive)
    {
        _isPassive = passive;

        CoinVisualState visualState = GetComponent<CoinVisualState>();
        if (visualState != null)
        {
            visualState.SetPassiveVisual(passive);
        }
    }

    public bool CanBeSelectedByPlayer()
    {
        return _team == CoinTeam.Player && !_isPassive && !IsFrozenByBooster();
    }

    /// <summary>
    /// Donma booster'ı şimdiye kadar sadece CoinTeam.Opponent paralarını hedefliyordu — ki
    /// aktive eden oyuncu zaten o paraları sürükleyemez, bu yüzden seçim mantığında hiç
    /// kontrol edilmemişti. Artık karşı tarafa ağ üzerinden de uygulandığı için (kendi
    /// CoinTeam.Player paraları donuyor), bu kontrol olmadan donmuş bir para yine de
    /// sürüklenip atılabilirdi.
    /// </summary>
    public bool IsFrozenByBooster()
    {
        IceFrozenCoinState frozen = GetComponent<IceFrozenCoinState>();
        return frozen != null && frozen.IsFrozen;
    }
}
