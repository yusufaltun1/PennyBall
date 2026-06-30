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
        return _team == CoinTeam.Player && !_isPassive;
    }
}
