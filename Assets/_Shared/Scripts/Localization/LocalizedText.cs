using TMPro;
using UnityEngine;

/// <summary>
/// TMP metnini lokalizasyon anahtarından doldurur; dil değişince kendini günceller.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TMP_Text))]
public class LocalizedText : MonoBehaviour
{
    [SerializeField] string _key;

    TMP_Text _text;

    public string Key
    {
        get => _key;
        set
        {
            _key = value;
            Refresh();
        }
    }

    void OnEnable()
    {
        LocalizationService.LanguageChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        LocalizationService.LanguageChanged -= Refresh;
    }

    public void Refresh()
    {
        if (string.IsNullOrEmpty(_key))
        {
            return;
        }

        _text ??= GetComponent<TMP_Text>();
        _text.text = LocalizationService.Get(_key);
    }
}
