using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class OnboardingMainMenuPresenter : MonoBehaviour
{
    [SerializeField] Button _playButton;
    [SerializeField] CanvasGroup _menuCanvasGroup;
    [SerializeField] GameObject _playHighlight;
    [SerializeField] float _dimmedAlpha = 0.35f;

    void Start()
    {
        if (_playButton != null)
        {
            _playButton.onClick.AddListener(HandlePlayPressed);
        }

        if (!OnboardingProgress.IsPlayHighlightPending)
        {
            SetNormalMenu();
            return;
        }

        ApplyPlayHighlightState();
    }

    void ApplyPlayHighlightState()
    {
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.alpha = _dimmedAlpha;
            _menuCanvasGroup.interactable = false;
            _menuCanvasGroup.blocksRaycasts = true;
        }

        if (_playButton != null)
        {
            _playButton.interactable = true;
        }

        if (_playHighlight != null)
        {
            _playHighlight.SetActive(true);
        }
    }

    public void OnPlayButtonPressedAfterOnboarding()
    {
        HandlePlayPressed();
    }

    void HandlePlayPressed()
    {
        if (!OnboardingProgress.IsPlayHighlightPending)
        {
            return;
        }

        OnboardingProgress.MarkPlayHighlightShown();
        SetNormalMenu();
    }

    void SetNormalMenu()
    {
        if (_menuCanvasGroup != null)
        {
            _menuCanvasGroup.alpha = 1f;
            _menuCanvasGroup.interactable = true;
            _menuCanvasGroup.blocksRaycasts = true;
        }

        if (_playHighlight != null)
        {
            _playHighlight.SetActive(false);
        }
    }
}
