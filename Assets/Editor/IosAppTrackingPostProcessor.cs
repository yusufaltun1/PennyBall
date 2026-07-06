#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// App Store ATT: Info.plist + native plugin linked for DllImport("__Internal").
/// </summary>
public static class IosAppTrackingPostProcessor
{
    public const string TrackingUsageDescription =
        "PennyBall uses your device identifier to deliver personalized ads and measure ad performance. This helps us keep the game free.";

    const string NativeSourceRelativePath = "Libraries/Plugins/iOS/AppTrackingTransparency.m";

    [PostProcessBuild(50)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(buildPath, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        plist.root.SetString("NSUserTrackingUsageDescription", TrackingUsageDescription);
        plist.WriteToFile(plistPath);

        string projectPath = PBXProject.GetPBXProjectPath(buildPath);
        var project = new PBXProject();
        project.ReadFromString(File.ReadAllText(projectPath));

        string mainTarget = project.GetUnityMainTargetGuid();
        string frameworkTarget = project.GetUnityFrameworkTargetGuid();

        project.AddFrameworkToProject(mainTarget, "AppTrackingTransparency.framework", true);
        project.AddFrameworkToProject(frameworkTarget, "AppTrackingTransparency.framework", true);

        string nativeSourcePath = Path.Combine(buildPath, NativeSourceRelativePath);
        if (!File.Exists(nativeSourcePath))
        {
            UnityEngine.Debug.LogWarning(
                $"[ATT] Native source not found at {nativeSourcePath}. Re-export iOS build from Unity.");
            File.WriteAllText(projectPath, project.WriteToString());
            return;
        }

        string fileGuid = project.FindFileGuidByProjectPath(NativeSourceRelativePath);
        if (string.IsNullOrEmpty(fileGuid))
        {
            fileGuid = project.AddFile(NativeSourceRelativePath, NativeSourceRelativePath, PBXSourceTree.Source);
        }

        // Unity auto-adds plugin to UnityFramework; ensure compile + export for __Internal.
        project.AddFileToBuild(frameworkTarget, fileGuid);

        const string exportSymbols =
            "-Wl,-exported_symbol,_PB_RequestAppTrackingAuthorization " +
            "-Wl,-exported_symbol,_PB_GetAppTrackingAuthorizationStatus";
        project.AddBuildProperty(frameworkTarget, "OTHER_LDFLAGS", exportSymbols);

        File.WriteAllText(projectPath, project.WriteToString());
    }
}
#endif
