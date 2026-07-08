using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class AvatarSpriteLibraryEditor
{
    const string LibraryAssetPath = "Assets/Resources/AvatarSpriteLibrary.asset";

    [MenuItem("PennyBall/Avatars/Rebuild Avatar Sprite Library")]
    public static void RebuildFromMenu()
    {
        if (RebuildLibrary())
        {
            Debug.Log("[AvatarSpriteLibrary] 0: Avtr_0, 1-59: Avtrs_ (1..59) yüklendi.");
        }
    }

    public static bool RebuildLibrary()
    {
        var library = AssetDatabase.LoadAssetAtPath<AvatarSpriteLibrary>(LibraryAssetPath);
        if (library == null)
        {
            Debug.LogError($"[AvatarSpriteLibrary] Asset bulunamadı: {LibraryAssetPath}");
            return false;
        }

        var sprites = new List<Sprite>();

        Sprite defaultSprite = AssetDatabase.LoadAssetAtPath<Sprite>(AvatarSpriteLibrary.DefaultAvatarAssetPath);
        if (defaultSprite == null)
        {
            Debug.LogError($"[AvatarSpriteLibrary] Varsayılan avatar bulunamadı: {AvatarSpriteLibrary.DefaultAvatarAssetPath}");
            return false;
        }

        sprites.Add(defaultSprite);

        for (int i = AvatarSpriteLibrary.NumberedAvatarMin; i <= AvatarSpriteLibrary.NumberedAvatarMax; i++)
        {
            string path = AvatarSpriteLibrary.GetNumberedAvatarAssetPath(i);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
            {
                Debug.LogError($"[AvatarSpriteLibrary] Avatar bulunamadı: {path}");
                return false;
            }

            sprites.Add(sprite);
        }

        SerializedObject serializedLibrary = new SerializedObject(library);
        SerializedProperty spritesProperty = serializedLibrary.FindProperty("_sprites");
        spritesProperty.arraySize = sprites.Count;

        for (int i = 0; i < sprites.Count; i++)
        {
            spritesProperty.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
        }

        serializedLibrary.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(library);
        AssetDatabase.SaveAssets();
        return true;
    }
}

[CustomEditor(typeof(AvatarSpriteLibrary))]
public class AvatarSpriteLibraryInspector : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (GUILayout.Button("Rebuild From Avatars Folder"))
        {
            AvatarSpriteLibraryEditor.RebuildFromMenu();
        }
    }
}
