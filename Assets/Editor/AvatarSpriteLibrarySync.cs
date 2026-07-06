using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class AvatarSpriteLibrarySync
{
    public const string LibraryAssetPath = "Assets/Resources/AvatarSpriteLibrary.asset";
    public const string AvatarsFolderPath = "Assets/_MainMenu/Textures/Avatars";

    [MenuItem("PennyBall/Avatars/Sync Avatar Sprite Library")]
    public static void SyncFromMenu()
    {
        if (Sync())
        {
            Debug.Log("[Avatar] AvatarSpriteLibrary güncellendi.");
        }
    }

    public static bool Sync()
    {
        var library = AssetDatabase.LoadAssetAtPath<AvatarSpriteLibrary>(LibraryAssetPath);
        if (library == null)
        {
            Debug.LogError($"[Avatar] Asset bulunamadı: {LibraryAssetPath}");
            return false;
        }

        Sprite firstSprite = GetFirstSprite(library);
        List<Sprite> avatarSprites = LoadAvtrsSprites();

        var serializedObject = new SerializedObject(library);
        SerializedProperty spritesProperty = serializedObject.FindProperty("_sprites");
        if (spritesProperty == null || !spritesProperty.isArray)
        {
            Debug.LogError("[Avatar] AvatarSpriteLibrary._sprites alanı bulunamadı.");
            return false;
        }

        spritesProperty.arraySize = 1 + avatarSprites.Count;
        spritesProperty.GetArrayElementAtIndex(0).objectReferenceValue = firstSprite;

        for (int i = 0; i < avatarSprites.Count; i++)
        {
            spritesProperty.GetArrayElementAtIndex(i + 1).objectReferenceValue = avatarSprites[i];
        }

        serializedObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return true;
    }

    static Sprite GetFirstSprite(AvatarSpriteLibrary library)
    {
        var serializedObject = new SerializedObject(library);
        SerializedProperty spritesProperty = serializedObject.FindProperty("_sprites");
        if (spritesProperty == null || spritesProperty.arraySize == 0)
        {
            return null;
        }

        return spritesProperty.GetArrayElementAtIndex(0).objectReferenceValue as Sprite;
    }

    static List<Sprite> LoadAvtrsSprites()
    {
        if (!AssetDatabase.IsValidFolder(AvatarsFolderPath))
        {
            Debug.LogError($"[Avatar] Klasör bulunamadı: {AvatarsFolderPath}");
            return new List<Sprite>();
        }

        return AssetDatabase
            .FindAssets(string.Empty, new[] { AvatarsFolderPath })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .Where(path => Path.GetFileName(path).StartsWith("Avtrs_", StringComparison.Ordinal))
            .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
            .Select(AssetDatabase.LoadAssetAtPath<Sprite>)
            .Where(sprite => sprite != null)
            .ToList();
    }
}

[CustomEditor(typeof(AvatarSpriteLibrary))]
public class AvatarSpriteLibraryEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Avtrs_ klasöründen senkronize et"))
        {
            AvatarSpriteLibrarySync.Sync();
        }
    }
}

public class AvatarSpriteLibraryPostprocessor : AssetPostprocessor
{
    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ShouldSync(importedAssets)
            && !ShouldSync(deletedAssets)
            && !ShouldSync(movedAssets)
            && !ShouldSync(movedFromAssetPaths))
        {
            return;
        }

        AvatarSpriteLibrarySync.Sync();
    }

    static bool ShouldSync(IEnumerable<string> assetPaths)
    {
        return assetPaths.Any(IsAvtrsAvatarAsset);
    }

    static bool IsAvtrsAvatarAsset(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath)
            || !assetPath.StartsWith(AvatarSpriteLibrarySync.AvatarsFolderPath, StringComparison.Ordinal))
        {
            return false;
        }

        string fileName = Path.GetFileName(assetPath);
        return fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
               && fileName.StartsWith("Avtrs_", StringComparison.Ordinal);
    }
}
