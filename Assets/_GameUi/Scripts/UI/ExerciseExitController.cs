using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ExerciseExitController : MonoBehaviour
{
    [SerializeField] Button _button;
    [SerializeField] Sprite _pressedSprite;

    void Awake()
    {
        if (_button == null)
        {
            _button = GetComponent<Button>();
        }

        FixButtonSprites();

        if (_button != null)
        {
            _button.onClick.RemoveListener(ExitToMainMenu);
            _button.onClick.AddListener(ExitToMainMenu);
        }

        transform.SetAsLastSibling();
    }

    void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(ExitToMainMenu);
        }
    }

    void FixButtonSprites()
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
        state.highlightedSprite = pressed;
        state.pressedSprite = pressed;
        state.selectedSprite = pressed;
        _button.spriteState = state;
        _button.transition = Selectable.Transition.SpriteSwap;
    }

    public void ExitToMainMenu()
    {
        MainMenuClickSound.Play();
        SceneManager.LoadScene(GameSceneNames.MainMenu);
    }
}
