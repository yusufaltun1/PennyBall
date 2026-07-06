#if UNITY_IOS
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

/// <summary>
/// LevelPlay/IronSource kaldırıldıktan sonra eski pod tanımları Podfile'a düşmeye devam ederse
/// pod install öncesinde temizler.
/// </summary>
public static class IosPodfileSanitizer
{
    static readonly string[] BlockedPodPrefixes =
    {
        "IronSourceSDK",
        "IronSourceUnityAdsAdapter",
    };

    [PostProcessBuild(49)]
    public static void SanitizePodfile(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string podfilePath = Path.Combine(buildPath, "Podfile");
        if (!File.Exists(podfilePath))
        {
            return;
        }

        string[] lines = File.ReadAllLines(podfilePath);
        List<string> filtered = new(lines.Length);
        bool removedAny = false;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (ShouldRemoveLine(line))
            {
                removedAny = true;
                continue;
            }

            filtered.Add(line);
        }

        if (!removedAny)
        {
            return;
        }

        File.WriteAllLines(podfilePath, filtered);
        Debug.Log("[iOS] Podfile'dan kullanılmayan IronSource pod satırları kaldırıldı.");
    }

    static bool ShouldRemoveLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        string trimmed = line.Trim();
        if (!trimmed.StartsWith("pod "))
        {
            return false;
        }

        for (int i = 0; i < BlockedPodPrefixes.Length; i++)
        {
            if (trimmed.Contains($"'{BlockedPodPrefixes[i]}'")
                || trimmed.Contains($"\"{BlockedPodPrefixes[i]}\"")
                || trimmed.Contains($"{BlockedPodPrefixes[i]},")
                || trimmed.Contains($"{BlockedPodPrefixes[i]}'"))
            {
                return true;
            }
        }

        return false;
    }
}
#endif
