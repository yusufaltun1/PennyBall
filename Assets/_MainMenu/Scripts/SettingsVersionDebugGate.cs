using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SettingsVersionDebugGate : MonoBehaviour
{
    void Awake()
    {
        TextMeshProUGUI versionText = GetComponent<TextMeshProUGUI>();
        if (versionText != null)
        {
            versionText.text = $"Version {Application.version}";
            versionText.raycastTarget = true;
        }

        Button button = GetComponent<Button>();
        if (button == null)
        {
            button = gameObject.AddComponent<Button>();
        }

        button.transition = Selectable.Transition.None;
        button.onClick.RemoveListener(OpenOnboardingForTesting);
        button.onClick.AddListener(OpenOnboardingForTesting);
    }

    void OnDestroy()
    {
        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(OpenOnboardingForTesting);
        }
    }

    void OpenOnboardingForTesting()
    {
        GameProgressReset.ResetAll();
        SceneManager.LoadScene(GameSceneNames.Splash);
    }
}
