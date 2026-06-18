using UnityEditor;
using UnityEngine;
using System;
using System.Reflection;

public static class GameViewSizesMenu
{
    [MenuItem("Tools/Mobil Cihazlari Ekle")]
    public static void AddSizes()
    {
        var sizesType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizes");
        var singleType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var instance = singleType.GetProperty("instance").GetValue(null, null);
        var currentGroup = sizesType.GetProperty("currentGroup").GetValue(instance, null);
        var addCustomSize = currentGroup.GetType().GetMethod("AddCustomSize", BindingFlags.Public | BindingFlags.Instance);
        
        var gameViewSizeType = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSize");
        var gameViewSizeTypeEnum = typeof(Editor).Assembly.GetType("UnityEditor.GameViewSizeType");

        // Yeni sürümlerde enum parametresi (0: Aspect Ratio, 1: Fixed Resolution) doğrudan bu tipe bağlıdır
        object fixedResolutionEnum = Enum.Parse(gameViewSizeTypeEnum, "FixedResolution");

        // Eklemek istediğin cihazlar
        string[] names = { "iPhone 12 mini", "iPhone 11", "iPhone 12 Pro", "iPad Pro 12.9" };
        int[] widths = { 1080, 828, 1170, 2048 };
        int[] heights = { 2340, 1792, 2532, 2732 };

        for (int i = 0; i < names.Length; i++)
        {
            // Yeni Unity sürümlerindeki constructor parametre sırası ve tipleri:
            // (GameViewSizeType type, int width, int height, string baseText)
            var newSize = Activator.CreateInstance(gameViewSizeType, new object[] { fixedResolutionEnum, widths[i], heights[i], names[i] });
            addCustomSize.Invoke(currentGroup, new object[] { newSize });
        }
        
        Debug.Log("Cihazlar başarıyla listenize eklendi!");
    }
}