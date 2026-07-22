using System;
using System.IO;
using UnityEngine;

public static class DeviceIdStore
{
    const string PrefKeyBase = "pb.device_id";

    /// <summary>
    /// Editor clone (ParrelSync) ile ana proje aynı PlayerPrefs'i paylaşır.
    /// Bu yüzden instance suffix ile ayrı key / ayrı id üretiriz.
    /// </summary>
    public static string GetOrCreate()
    {
        string key = PrefKeyBase + GetInstanceSuffix();
        string id = PlayerPrefs.GetString(key, string.Empty);
        if (!string.IsNullOrEmpty(id))
        {
            return id;
        }

        id = Guid.NewGuid().ToString("N");
        PlayerPrefs.SetString(key, id);
        PlayerPrefs.Save();
        return id;
    }

    /// <summary>
    /// Photon Auth UserId — her Editor/build instance için benzersiz olmalı.
    /// </summary>
    public static string GetPhotonUserId()
    {
        return GetOrCreate();
    }

    static string GetInstanceSuffix()
    {
#if UNITY_EDITOR
        // ParrelSync clone path: .../PennyBall_clone_0/Assets
        string dataPath = Application.dataPath.Replace('\\', '/');
        string projectFolder = Directory.GetParent(dataPath)?.Name ?? string.Empty;
        int cloneIdx = projectFolder.IndexOf("_clone", StringComparison.OrdinalIgnoreCase);
        if (cloneIdx >= 0)
        {
            return "_" + projectFolder.Substring(cloneIdx + 1);
        }

        foreach (string arg in Environment.GetCommandLineArgs())
        {
            if (arg.IndexOf("clone", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "_arg_" + Mathf.Abs(arg.GetHashCode()).ToString("x");
            }
        }
#endif
        return string.Empty;
    }
}
