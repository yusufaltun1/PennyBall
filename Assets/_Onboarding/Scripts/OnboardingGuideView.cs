using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class OnboardingGuideView : MonoBehaviour
{
    [SerializeField] Text _instructionText;

    void Awake()
    {
        if (_instructionText == null)
        {
            _instructionText = GetComponentInChildren<Text>(true);
        }
    }

    public void BindInstructionText(Text instructionText)
    {
        _instructionText = instructionText;
    }

    public void Show(string message)
    {
        if (_instructionText == null)
        {
            return;
        }

        _instructionText.text = message;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (_instructionText != null)
        {
            _instructionText.text = string.Empty;
        }

        gameObject.SetActive(false);
    }
}
