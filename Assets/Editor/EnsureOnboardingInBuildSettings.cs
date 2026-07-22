#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Onboarding sahnesinin Build Settings / Build Profile global scene listesinde
/// olduğundan emin olur. Unity 6 bazen diskteki EditorBuildSettings değişikliğini
/// Play Mode'a geç yansıtır.
/// </summary>
[InitializeOnLoad]
public static class EnsureOnboardingInBuildSettings
{
    const string OnboardingPath = "Assets/_Onboarding/Scenes/Onboarding.unity";
    const string OnboardingOldPath = "Assets/_Onboarding/Scenes/Onboarding_Old.unity";

    static EnsureOnboardingInBuildSettings()
    {
        EditorApplication.delayCall += Ensure;
    }

    [MenuItem("PennyBall/Build/Ensure Onboarding In Build Settings")]
    public static void Ensure()
    {
        if (!System.IO.File.Exists(OnboardingPath))
        {
            Debug.LogWarning($"[Build] Onboarding scene bulunamadı: {OnboardingPath}");
            return;
        }

        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        bool changed = false;

        int removed = scenes.RemoveAll(s => s.path == OnboardingOldPath);
        if (removed > 0)
        {
            changed = true;
        }

        int index = scenes.FindIndex(s => s.path == OnboardingPath);
        if (index < 0)
        {
            int insertAt = scenes.FindIndex(s =>
                s.path != null && s.path.Contains("MainMenu_Scene"));
            var entry = new EditorBuildSettingsScene(OnboardingPath, true);
            if (insertAt >= 0)
            {
                scenes.Insert(insertAt + 1, entry);
            }
            else
            {
                scenes.Add(entry);
            }

            changed = true;
        }
        else if (!scenes[index].enabled)
        {
            EditorBuildSettingsScene scene = scenes[index];
            scene.enabled = true;
            scenes[index] = scene;
            changed = true;
        }

        // Duplicate path temizliği
        var seen = new HashSet<string>();
        var cleaned = new List<EditorBuildSettingsScene>(scenes.Count);
        foreach (EditorBuildSettingsScene scene in scenes)
        {
            if (string.IsNullOrEmpty(scene.path) || !seen.Add(scene.path))
            {
                changed = true;
                continue;
            }

            cleaned.Add(scene);
        }

        if (!changed)
        {
            // Unity Play Mode bazen disk değişikliğini almaz; yeniden assign ederek cache'i tazele.
            EditorBuildSettings.scenes = cleaned.ToArray();
            return;
        }

        EditorBuildSettings.scenes = cleaned.ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("[Build] Onboarding Build Settings listesine eklendi/güncellendi.");
    }
}
#endif
