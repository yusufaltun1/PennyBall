using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(OnboardingCoinDragController))]
public class OnboardingCoin : MonoBehaviour
{
    [SerializeField] int _coinIndex;
    [SerializeField] bool _isSelectable = true;

    OnboardingCoinDragController _dragController;

    public int CoinIndex => _coinIndex;
    public bool IsSelectable => _isSelectable;
    public OnboardingCoinDragController DragController => _dragController;

    void Awake()
    {
        _dragController = GetComponent<OnboardingCoinDragController>();
    }

    public void Configure(int coinIndex, bool selectable = true)
    {
        _coinIndex = coinIndex;
        _isSelectable = selectable;
    }

    public void SetSelectable(bool selectable)
    {
        _isSelectable = selectable;
    }
}
