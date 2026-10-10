#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

/// <summary>
/// Meta App Events için Info.plist: FacebookAppID, URL scheme, ATT metni.
/// SKAdNetwork ID'leri FBSDK CocoaPods ile genelde eklenir; eksikse buraya eklenebilir.
/// </summary>
public static class MetaIosInfoPlistPostProcessor
{
    const string AppId = EnsureFacebookSettings.PennyBallAppId;
    const string AttUsage =
        "This identifier will be used to deliver personalized ads to you and to measure app performance.";

    [PostProcessBuild(50)]
    public static void OnPostProcessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS)
        {
            return;
        }

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        PlistElementDict root = plist.root;

        root.SetString("FacebookAppID", AppId);
        root.SetString("FacebookClientToken", EnsureFacebookSettings.PennyBallClientToken ?? string.Empty);
        root.SetString("FacebookDisplayName", "Penny Ball");
        root.SetBoolean("FacebookAutoLogAppEventsEnabled", true);
        root.SetBoolean("FacebookAdvertiserIDCollectionEnabled", true);
        // Her build'de güncelle — ATT diyaloğu bu metin olmadan çıkmaz.
        root.SetString("NSUserTrackingUsageDescription", AttUsage);

        // AppTrackingTransparency framework (ATT API)
        string pbxPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        var project = new PBXProject();
        project.ReadFromFile(pbxPath);
        string frameworkTarget = project.GetUnityFrameworkTargetGuid();
        project.AddFrameworkToProject(frameworkTarget, "AppTrackingTransparency.framework", false);
        project.WriteToFile(pbxPath);

        // URL scheme: fb{APP_ID}
        PlistElementArray urlTypes;
        if (root.values.ContainsKey("CFBundleURLTypes"))
        {
            urlTypes = root["CFBundleURLTypes"].AsArray();
        }
        else
        {
            urlTypes = root.CreateArray("CFBundleURLTypes");
        }

        string scheme = "fb" + AppId;
        bool hasScheme = false;
        for (int i = 0; i < urlTypes.values.Count; i++)
        {
            PlistElementDict dict = urlTypes.values[i].AsDict();
            if (dict == null || !dict.values.ContainsKey("CFBundleURLSchemes"))
            {
                continue;
            }

            PlistElementArray schemes = dict["CFBundleURLSchemes"].AsArray();
            for (int s = 0; s < schemes.values.Count; s++)
            {
                if (schemes.values[s].AsString() == scheme)
                {
                    hasScheme = true;
                    break;
                }
            }
        }

        if (!hasScheme)
        {
            PlistElementDict urlDict = urlTypes.AddDict();
            urlDict.SetString("CFBundleURLName", "facebook-unity-sdk");
            PlistElementArray schemes = urlDict.CreateArray("CFBundleURLSchemes");
            schemes.AddString(scheme);
        }

        File.WriteAllText(plistPath, plist.WriteToString());
        Debug.Log($"[Meta] Info.plist updated (FacebookAppID={AppId}, ATT, URL scheme).");
    }
}
#endif
