using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class OnboardingEditorSetup
{
    const string OnboardingScenePath = "Assets/_Onboarding/Scenes/Onboarding.unity";

    [MenuItem("PennyBall/Reset ALL Progress (Recommended)")]
    public static void ResetAllProgress()
    {
        GameProgressReset.ResetAll(keepFeedbackSettings: false);

        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "[Progress] Play Mode açıkken bellek cache'i kalabilir. " +
                "Play'i durdurup tekrar başlat veya Settings > Version'a tıkla.");
            return;
        }

        Debug.Log("[Progress] Tüm kayıt silindi. Play'e bas → Splash → Onboarding açılmalı.");
    }

    [MenuItem("PennyBall/Onboarding/Reset Progress (Keep Settings)")]
    public static void ResetProgressKeepSettings()
    {
        GameProgressReset.ResetAll(keepFeedbackSettings: true);

        if (EditorApplication.isPlaying)
        {
            Debug.LogWarning(
                "[Progress] Play Mode açıkken bellek cache'i kalabilir. Play'i durdurup tekrar başlat.");
            return;
        }

        Debug.Log("[Progress] Oyun kaydı sıfırlandı (ses/müzik ayarları korundu).");
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
