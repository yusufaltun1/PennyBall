using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Metinleri Resources/Localization/{dil}.json dosyalarından okur.
/// Anahtar aktif dilde yoksa İngilizce'ye, orada da yoksa anahtarın kendisine düşer.
/// </summary>
public static class LocalizationService
{
    public const string DefaultLanguage = "en";

    const string LanguagePrefKey = "pennyball.language";
    const string ResourcesFolder = "Localization/";

    static readonly string[] SupportedLanguageCodes = { "tr", "en", "pt" };
    static readonly Dictionary<string, string> CurrentTable = new();
    static readonly Dictionary<string, string> FallbackTable = new();

    static bool _loaded;
    static string _currentLanguage = DefaultLanguage;

    public static event Action LanguageChanged;

    public static IReadOnlyList<string> SupportedLanguages => SupportedLanguageCodes;

    public static string CurrentLanguage
    {
        get
        {
            EnsureLoaded();
            return _currentLanguage;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        _loaded = false;
        _currentLanguage = DefaultLanguage;
        CurrentTable.Clear();
        FallbackTable.Clear();
        LanguageChanged = null;
    }

    public static string Get(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        EnsureLoaded();

        if (CurrentTable.TryGetValue(key, out string value) || FallbackTable.TryGetValue(key, out value))
        {
            return value;
        }

#if UNITY_EDITOR
        Debug.LogWarning($"[Localization] Eksik anahtar: '{key}' ({_currentLanguage})");
#endif
        return key;
    }

    public static string Get(string key, params object[] args)
    {
        string format = Get(key);
        if (args == null || args.Length == 0)
        {
            return format;
        }

        try
        {
            return string.Format(format, args);
        }
        catch (FormatException)
        {
            Debug.LogWarning($"[Localization] Format hatası: '{key}'");
            return format;
        }
    }

    public static bool IsSupported(string languageCode)
    {
        return Array.IndexOf(SupportedLanguageCodes, languageCode) >= 0;
    }

    /// <summary>Seçicide gösterilen kısa etiket (TR, EN, PT).</summary>
    public static string GetLanguageShortLabel(string languageCode)
    {
        return languageCode.ToUpperInvariant();
    }

    public static void SetLanguage(string languageCode)
    {
        if (!IsSupported(languageCode))
        {
            Debug.LogWarning($"[Localization] Desteklenmeyen dil: '{languageCode}'");
            return;
        }

        EnsureLoaded();
        if (_currentLanguage == languageCode)
        {
            return;
        }

        PlayerPrefs.SetString(LanguagePrefKey, languageCode);
        PlayerPrefs.Save();

        _currentLanguage = languageCode;
        LoadTable(_currentLanguage, CurrentTable);
        LanguageChanged?.Invoke();
    }

    static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _loaded = true;

        string saved = PlayerPrefs.GetString(LanguagePrefKey, string.Empty);
        _currentLanguage = IsSupported(saved) ? saved : DetectDeviceLanguage();

        LoadTable(DefaultLanguage, FallbackTable);
        LoadTable(_currentLanguage, CurrentTable);
    }

    static string DetectDeviceLanguage()
    {
        return Application.systemLanguage switch
        {
            SystemLanguage.Turkish => "tr",
            SystemLanguage.Portuguese => "pt",
            _ => DefaultLanguage,
        };
    }

    static void LoadTable(string languageCode, Dictionary<string, string> target)
    {
        target.Clear();

        TextAsset asset = Resources.Load<TextAsset>(ResourcesFolder + languageCode);
        if (asset == null)
        {
            Debug.LogWarning($"[Localization] Tablo bulunamadı: Resources/{ResourcesFolder}{languageCode}.json");
            return;
        }

        TableFile file = JsonUtility.FromJson<TableFile>(asset.text);
        if (file?.entries == null)
        {
            Debug.LogWarning($"[Localization] Tablo okunamadı: {languageCode}.json");
            return;
        }

        for (int i = 0; i < file.entries.Length; i++)
        {
            Entry entry = file.entries[i];
            if (!string.IsNullOrEmpty(entry.key))
            {
                target[entry.key] = entry.value ?? string.Empty;
            }
        }
    }

    [Serializable]
    class TableFile
    {
        public Entry[] entries;
    }

    [Serializable]
    class Entry
    {
        public string key;
        public string value;
    }
}
