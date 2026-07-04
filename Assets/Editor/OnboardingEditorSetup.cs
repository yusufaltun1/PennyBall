using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OnboardingEditorSetup
{
    const string OnboardingScenePath = "Assets/_Onboarding/Scenes/Onboarding.unity";

    [MenuItem("PennyBall/Onboarding/Reset Progress")]
    public static void ResetProgress()
    {
        OnboardingProgress.ResetAll();
        Debug.Log("[Onboarding] Progress sıfırlandı.");
    }

    [MenuItem("PennyBall/Onboarding/Open Onboarding Scene")]
    public static void OpenOnboardingScene()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            return;
        }

        EditorSceneManager.OpenScene(OnboardingScenePath);
    }

    [MenuItem("PennyBall/Onboarding/Wire Onboarding Complete Panel")]
    public static void WireOnboardingCompletePanel()
    {
        GameObject panel = GameObject.Find("OnboardingComplete");
        if (panel == null)
        {
            Debug.LogError("[Onboarding] OnboardingComplete bulunamadı.");
            return;
        }

        OnboardingCompleteController controller = panel.GetComponent<OnboardingCompleteController>();
        if (controller == null)
        {
            controller = panel.AddComponent<OnboardingCompleteController>();
        }

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.scene);
        Debug.Log("[Onboarding] OnboardingCompleteController bağlandı.");
    }
}
