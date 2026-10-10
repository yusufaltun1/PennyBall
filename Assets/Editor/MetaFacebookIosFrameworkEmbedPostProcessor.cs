#if UNITY_IOS
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// Facebook SDK iOS FBAEMKit launch crash düzeltmesi.
/// Asıl çözüm: EDM static linkage kapalı + dynamic use_frameworks! → CocoaPods frameworks'ü embed eder.
/// Manuel Embed & Sign EKLENMEMELİ; CocoaPods "[CP] Embed Pods Frameworks" ile çakışır
/// ("Multiple commands produce ... FBAEMKit.framework").
/// </summary>
[InitializeOnLoad]
public static class MetaFacebookIosFrameworkEmbedPostProcessor
{
    static readonly string[] FrameworkNames =
    {
        "FBAEMKit",
        "FBSDKCoreKit",
        "FBSDKCoreKit_Basics",
        "FBSDKGamingServicesKit",
        "FBSDKLoginKit",
        "FBSDKShareKit",
    };

    static MetaFacebookIosFrameworkEmbedPostProcessor()
    {
        TryDisableEdmStaticFrameworkLinking();
    }

    static void TryDisableEdmStaticFrameworkLinking()
    {
        try
        {
            Type resolver = Type.GetType("Google.IOSResolver, Google.IOSResolver");
            if (resolver == null)
            {
                return;
            }

            PropertyInfo prop = resolver.GetProperty(
                "PodfileStaticLinkFrameworks",
                BindingFlags.Public | BindingFlags.Static);
            if (prop == null || !prop.CanWrite)
            {
                return;
            }

            object current = prop.GetValue(null, null);
            if (current is bool enabled && enabled)
            {
                prop.SetValue(null, false, null);
                Debug.Log("[Meta] EDM iOS Resolver: Link frameworks statically kapatıldı (FBAEMKit).");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[Meta] EDM static link ayarı değiştirilemedi: {ex.Message}");
        }
    }

    /// <summary>
    /// EDM Podfile üretimi (40) ile pod install (50) arasında: static → dynamic.
    /// </summary>
    [PostProcessBuild(45)]
    public static void PatchPodfileForDynamicFrameworks(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
        if (!File.Exists(podfilePath))
        {
            return;
        }

        string content = File.ReadAllText(podfilePath);
        string original = content;

        content = content.Replace("use_frameworks! :linkage => :static", "use_frameworks!");
        content = content.Replace("use_frameworks! :linkage => static", "use_frameworks!");

        if (!content.Contains("DEBUG_INFORMATION_FORMAT'] = 'dwarf-with-dsym'"))
        {
            const string dsymLines =
                "    installer.pods_project.targets.each do |target|\n" +
                "      target.build_configurations.each do |config|\n" +
                "        config.build_settings['DEBUG_INFORMATION_FORMAT'] = 'dwarf-with-dsym'\n" +
                "        config.build_settings['ONLY_ACTIVE_ARCH'] = 'NO'\n" +
                "      end\n" +
                "    end\n";

            // EDM zaten post_install eklediyse içine enjekte et; yoksa yeni blok ekle.
            const string marker = "post_install do |installer|";
            int idx = content.IndexOf(marker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                int insertAt = idx + marker.Length;
                content = content.Insert(insertAt, "\n" + dsymLines);
            }
            else
            {
                content += "\n" + marker + "\n" + dsymLines + "end\n";
            }
        }

        if (content != original)
        {
            File.WriteAllText(podfilePath, content);
            Debug.Log("[Meta] Podfile: dynamic frameworks + dSYM ayarı güncellendi.");
        }
    }

    /// <summary>
    /// Önceki fix'in eklediği çift Embed Frameworks kayıtlarını temizler.
    /// Embed işini yalnızca CocoaPods yapmalı.
    /// </summary>
    [PostProcessBuild(1000)]
    public static void RemoveDuplicateManualFacebookEmbeds(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        if (!File.Exists(projPath))
        {
            return;
        }

        var project = new PBXProject();
        project.ReadFromFile(projPath);
        string mainTarget = project.GetUnityMainTargetGuid();

        project.SetBuildProperty(mainTarget, "ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES", "YES");
        project.AddBuildProperty(mainTarget, "LD_RUNPATH_SEARCH_PATHS", "$(inherited)");
        project.AddBuildProperty(mainTarget, "LD_RUNPATH_SEARCH_PATHS", "@executable_path/Frameworks");

        int removed = 0;
        foreach (string name in FrameworkNames)
        {
            string[] relativePaths =
            {
                $"Pods/{name}/XCFrameworks/{name}.xcframework",
                $"Pods/{name}/{name}.xcframework",
            };

            foreach (string relative in relativePaths)
            {
                string guid = project.FindFileGuidByProjectPath(relative);
                if (string.IsNullOrEmpty(guid))
                {
                    continue;
                }

                project.RemoveFile(guid);
                removed++;
            }
        }

        project.WriteToFile(projPath);
        if (removed > 0)
        {
            Debug.Log($"[Meta] Çift Facebook embed kaydı temizlendi: {removed} (CocoaPods embed kullanılacak).");
        }
    }
}
#endif
