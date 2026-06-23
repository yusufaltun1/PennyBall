using UnityEditor;
using UnityEngine;

public static class ByteBrewSetup
{
    [MenuItem("PennyBall/Setup ByteBrew Analytics")]
    public static void OpenByteBrewSettings()
    {
        ByteBrewSettings settings = AssetDatabase.LoadAssetAtPath<ByteBrewSettings>(
            "Assets/ByteBrewSDK/Resources/ByteBrewSettings.asset");

        if (settings == null)
        {
            Debug.LogError("ByteBrewSettings bulunamadı. ByteBrew SDK import edilmiş mi kontrol et.");
            return;
        }

        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);

        Debug.Log(
            "ByteBrew ayarları açıldı.\n" +
            "1) iOS/Android platformlarını aktif et\n" +
            "2) ByteBrew dashboard'dan Game ID ve SDK Key gir\n" +
            "3) İlk sahneyi build settings'e ekle (ByteBrew ilk oturumu orada yakalar)\n" +
            "4) Cihazda test et (Editor'da event gönderilmez)");
    }
}
