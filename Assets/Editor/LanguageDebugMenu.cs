using UnityEditor;
using UnityEngine;

public static class LanguageDebugMenu
{
    const string MenuRoot = "PennyBall/Set Active Language/";
    const string TurkishItem = MenuRoot + "TR";
    const string EnglishItem = MenuRoot + "EN";
    const string PortugueseItem = MenuRoot + "PT";

    [MenuItem(TurkishItem)]
    static void SetTurkish() => SetLanguage("tr");

    [MenuItem(EnglishItem)]
    static void SetEnglish() => SetLanguage("en");

    [MenuItem(PortugueseItem)]
    static void SetPortuguese() => SetLanguage("pt");

    [MenuItem(TurkishItem, true)]
    static bool ValidateTurkish() => RefreshChecks();

    [MenuItem(EnglishItem, true)]
    static bool ValidateEnglish() => RefreshChecks();

    [MenuItem(PortugueseItem, true)]
    static bool ValidatePortuguese() => RefreshChecks();

    static void SetLanguage(string languageCode)
    {
        LocalizationService.SetLanguage(languageCode);
        Debug.Log($"[Localization] Aktif dil: {LocalizationService.CurrentLanguage}");
    }

    static bool RefreshChecks()
    {
        string current = LocalizationService.CurrentLanguage;
        Menu.SetChecked(TurkishItem, current == "tr");
        Menu.SetChecked(EnglishItem, current == "en");
        Menu.SetChecked(PortugueseItem, current == "pt");
        return true;
    }
}
