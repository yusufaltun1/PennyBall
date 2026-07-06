using UnityEngine;

[CreateAssetMenu(fileName = "HandCursorConfig", menuName = "PennyBall/Hand Cursor Config")]
public class HandCursorConfig : ScriptableObject
{
    [SerializeField] Sprite _defaultHand;
    [SerializeField] Sprite _pressedHand;
    [SerializeField] float _screenSize = 220f;

    public Sprite DefaultHand => _defaultHand;
    public Sprite PressedHand => _pressedHand;
    public float ScreenSize => _screenSize;
}
