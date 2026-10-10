using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class PaytoneOneFontSetup
{
    const string SourceFontPath = "Assets/_MainMenu/Fonts/PaytoneOne-Regular.ttf";
    const string FontAssetPath = "Assets/_MainMenu/Fonts/PaytoneOne-Regular SDF.asset";
    const string TabLabelMaterialPath = "Assets/_MainMenu/Fonts/PaytoneOne-Regular SDF - Tab Label.mat";
    const string BottomTabMenuPrefabPath = "Assets/_MainMenu/Prefab/Bottom_Tab_Menu.prefab";
    const string LeagueNamePath = "Buttons/Btn_League/Btn_Tab_League/Name";

    static readonly Color OutlineColor = new Color32(22, 98, 196, 255);
    static readonly Color UnderlayColor = new Color32(14, 34, 120, 255);
    static readonly Color FaceTopColor = new Color32(255, 255, 255, 255);
    static readonly Color FaceBottomColor = new Color32(188, 198, 222, 255);

    [MenuItem("PennyBall/Fonts/Setup Paytone One")]
    static void Setup()
    {
        TMP_FontAsset fontAsset = GetOrCreateFontAsset();
        if (fontAsset == null)
        {
            return;
        }

        Material tabLabelMaterial = CreateOrUpdateTabLabelMaterial(fontAsset);
        AssignToLeagueName(fontAsset, tabLabelMaterial);
    }

    static TMP_FontAsset GetOrCreateFontAsset()
    {
        TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        if (existing != null)
        {
            return existing;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (sourceFont == null)
        {
            Debug.LogError($"[Fonts] Kaynak font bulunamadı: {SourceFontPath}");
            return null;
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic);
        if (fontAsset == null)
        {
            Debug.LogError("[Fonts] Paytone One font asset oluşturulamadı.");
            return null;
        }

        fontAsset.name = "PaytoneOne-Regular SDF";
        AssetDatabase.CreateAsset(fontAsset, FontAssetPath);

        fontAsset.atlasTexture.name = "PaytoneOne-Regular SDF Atlas";
        AssetDatabase.AddObjectToAsset(fontAsset.atlasTexture, fontAsset);
        fontAsset.material.name = "PaytoneOne-Regular SDF Material";
        AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(FontAssetPath);

        Debug.Log($"[Fonts] Oluşturuldu: {FontAssetPath}");
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
    }

    static Material CreateOrUpdateTabLabelMaterial(TMP_FontAsset fontAsset)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(TabLabelMaterialPath);
        bool isNew = material == null;
        if (isNew)
        {
            material = new Material(fontAsset.material);
        }

        material.name = "PaytoneOne-Regular SDF - Tab Label";
        material.SetTexture(ShaderUtilities.ID_MainTex, fontAsset.atlasTexture);

        material.EnableKeyword(ShaderUtilities.Keyword_Outline);
        material.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
        material.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.22f);
        material.SetFloat(ShaderUtilities.ID_FaceDilate, 0.15f);

        // Koyu alt kabartma: sert kenarlı, aşağı kaydırılmış underlay.
        material.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        material.SetColor(ShaderUtilities.ID_UnderlayColor, UnderlayColor);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
        material.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.75f);
        material.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.6f);
        material.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);

        if (isNew)
        {
            AssetDatabase.CreateAsset(material, TabLabelMaterialPath);
        }
        else
        {
            EditorUtility.SetDirty(material);
        }

        AssetDatabase.SaveAssets();
        return material;
    }

    static void AssignToLeagueName(TMP_FontAsset fontAsset, Material material)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BottomTabMenuPrefabPath);
        try
        {
            Transform nameTransform = root.transform.Find(LeagueNamePath);
            TMP_Text text = nameTransform != null ? nameTransform.GetComponent<TMP_Text>() : null;
            if (text == null)
            {
                Debug.LogError($"[Fonts] {BottomTabMenuPrefabPath} içinde '{LeagueNamePath}' TMP metni bulunamadı.");
                return;
            }

            text.font = fontAsset;
            text.fontSharedMaterial = material;
            text.color = Color.white;
            text.enableVertexGradient = true;
            text.colorGradient = new VertexGradient(FaceTopColor, FaceTopColor, FaceBottomColor, FaceBottomColor);
            PrefabUtility.SaveAsPrefabAsset(root, BottomTabMenuPrefabPath);
            Debug.Log($"[Fonts] {LeagueNamePath} artık Paytone One kullanıyor.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
