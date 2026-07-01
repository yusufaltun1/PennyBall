using System;
using System.Collections.Generic;

/// <summary>
/// Oyun katmanından analytics'e giden ince facade. SDK implementasyonu ByteBrewGameAnalytics'te.
/// </summary>
public static class GameAnalytics
{
    public static event Action<string, Dictionary<string, string>> EventRequested;

    public static void Track(string eventName, Dictionary<string, string> parameters = null)
    {
        EventRequested?.Invoke(eventName, parameters);
    }

    public static void Track(string eventName, string key, string value)
    {
        Track(eventName, new Dictionary<string, string> { { key, value } });
    }
}
