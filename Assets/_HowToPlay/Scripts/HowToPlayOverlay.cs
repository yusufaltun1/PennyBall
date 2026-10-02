using UnityEngine;

/// <summary>
/// Kural anlatımını mevcut sahnenin üstünde overlay olarak açar (ana menü → Settings linki).
/// Prefab: Assets/_HowToPlay/Resources/HowToPlay/HowToPlayPanel.prefab
/// </summary>
public static class HowToPlayOverlay
{
    const string PrefabResourcePath = "HowToPlay/HowToPlayPanel";

    static HowToPlayController _openInstance;

    public static bool IsOpen => _openInstance != null;

    public static HowToPlayController Open(Transform parent)
    {
        if (_openInstance != null)
        {
            return _openInstance;
        }

        HowToPlayController prefab = Resources.Load<HowToPlayController>(PrefabResourcePath);
        if (prefab == null)
        {
            Debug.LogError($"[HowToPlay] Prefab bulunamadı: Resources/{PrefabResourcePath}");
            return null;
        }

        _openInstance = Object.Instantiate(prefab, parent, false);
        _openInstance.Configure(HowToPlayController.Mode.FromMenu);

        if (_openInstance.transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        _openInstance.transform.SetAsLastSibling();
        return _openInstance;
    }
}
