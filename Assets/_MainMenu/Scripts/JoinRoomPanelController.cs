using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// "Play with Friends" > Join: sadece 6 haneli kod girişi. Submit'te
/// MatchingPanelController.BeginJoinRoomFlow() çağrılır — bekleme/geri sayım
/// mevcut Matching_Panel'de (quick-match ile aynı akış) gerçekleşir.
/// </summary>
public class JoinRoomPanelController : MonoBehaviour
{
    [SerializeField] MatchingPanelController matchingPanel;
    [SerializeField] TMP_InputField codeInputField;
    [SerializeField] TextMeshProUGUI statusText;
    [SerializeField] Button submitButton;
    [SerializeField] Button cancelButton;

    void Awake()
    {
        if (codeInputField != null)
        {
            codeInputField.characterLimit = 6;
            codeInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        }

        if (submitButton != null)
        {
            submitButton.onClick.AddListener(SubmitCode);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(Close);
        }
    }

    public void OpenJoinScreen()
    {
        gameObject.SetActive(true);
        if (codeInputField != null)
        {
            codeInputField.text = string.Empty;
            codeInputField.interactable = true;
        }

        statusText?.SetText(LocalizationService.Get("party.enter_code"));
    }

    public void Close()
    {
        MainMenuClickSound.Play();
        gameObject.SetActive(false);
    }

    public void SubmitCode()
    {
        if (codeInputField == null || matchingPanel == null)
        {
            return;
        }

        string code = codeInputField.text?.Trim();
        if (string.IsNullOrEmpty(code) || code.Length != 6)
        {
            statusText?.SetText(LocalizationService.Get("party.enter_code"));
            return;
        }

        MainMenuClickSound.Play();
        gameObject.SetActive(false);
        matchingPanel.BeginJoinRoomFlow(code);
    }
}
