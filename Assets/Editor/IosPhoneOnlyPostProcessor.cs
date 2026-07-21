#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

/// <summary>
/// iPhone-only build'lerde Xcode projesinden iPad launch screen referanslarını temizler.
/// </summary>
public static class IosPhoneOnlyPostProcessor
{
    const string IpadLaunchStoryboard = "LaunchScreen-iPad.storyboard";

    [PostProcessBuild(999)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        if (PlayerSettings.iOS.targetDevice != iOSTargetDevice.iPhoneOnly)
        {
            return;
        }

        string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(pbxPath);

        string fileGuid = project.FindFileGuidByProjectPath(IpadLaunchStoryboard);
        if (!string.IsNullOrEmpty(fileGuid))
        {
            project.RemoveFile(fileGuid);
            project.WriteToFile(pbxPath);
        }

        string storyboardPath = Path.Combine(pathToBuiltProject, IpadLaunchStoryboard);
        if (File.Exists(storyboardPath))
        {
            File.Delete(storyboardPath);
        }
    }
}
#endif
