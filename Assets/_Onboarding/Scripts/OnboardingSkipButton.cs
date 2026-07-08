using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Skip butonunun görsel durumunu düzeltir; tıklama OnboardingSceneBootstrap üzerinden bağlanır.
/// </summary>
[DisallowMultipleComponent]
public class OnboardingSkipButton : MonoBehaviour
{
    [SerializeField] Button _button;
    [SerializeField] Sprite _pressedSprite;

    void Awake()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        FixPressedSprite();
        transform.SetAsLastSibling();
    }

    void Start()
    {
        OnboardingSceneBootstrap.WireSkipButton();
    }

    public void SkipToExercise()
    {
        OnboardingSceneBootstrap.SkipToExercise();
    }

    void FixPressedSprite()
    {
        if (_button == null)
        {
            return;
        }

        Image image = _button.targetGraphic as Image;
        if (image == null || image.sprite == null)
        {
            return;
        }

        Sprite pressed = _pressedSprite != null ? _pressedSprite : image.sprite;
        SpriteState state = _button.spriteState;
        state.pressedSprite = pressed;
        state.highlightedSprite = pressed;
        state.selectedSprite = pressed;
        _button.spriteState = state;
        _button.transition = Selectable.Transition.SpriteSwap;
    }
}
