using ByteBrewSDK;
using UnityEngine;

/// <summary>
/// ByteBrew SDK nesnesini oluşturur ve ilk sahnede initialize eder.
/// Game ID / SDK Key: Window → ByteBrew → Select ByteBrew settings
/// </summary>
public static class ByteBrewAnalyticsBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void EnsureByteBrewObject()
    {
        if (ByteBrew.Instance != null)
        {
            return;
        }

        var byteBrewObject = new GameObject("ByteBrew");
        byteBrewObject.AddComponent<ByteBrew>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void InitializeByteBrew()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // ATT sonrası AppTrackingTransparencyService başlatır.
        return;
#endif
        ByteBrew.InitializeByteBrew();
    }

    public static void InitializeAfterTrackingPrompt()
    {
        if (ByteBrew.IsByteBrewInitialized())
        {
            return;
        }

        ByteBrew.InitializeByteBrew();
    }
}
