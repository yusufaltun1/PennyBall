using System;
using UnityEngine;

public static class GameFeedbackSettingsService
{
    static GameFeedbackSettingsData _data;
    static bool _loaded;

    public static event Action Changed;

    public static bool SoundEffectsEnabled
    {
        get
        {
            EnsureLoaded();
            return _data.soundEffectsEnabled;
        }
        set => SetField(ref _data.soundEffectsEnabled, value);
    }

    public static bool VibrationEnabled
    {
        get
        {
            EnsureLoaded();
            return _data.vibrationEnabled;
        }
        set => SetField(ref _data.vibrationEnabled, value);
    }

    public static bool MusicEnabled
    {
        get
        {
            EnsureLoaded();
            return _data.musicEnabled;
        }
        set => SetField(ref _data.musicEnabled, value);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        EnsureLoaded();
    }

    public static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        _data = GameFeedbackSettingsRepository.Load();
        _loaded = true;
    }

    public static void Reload()
    {
        _data = GameFeedbackSettingsRepository.Load();
        _loaded = true;
        Changed?.Invoke();
    }

    static void SetField(ref bool field, bool value)
    {
        EnsureLoaded();
        if (field == value)
        {
            return;
        }

        field = value;
        GameFeedbackSettingsRepository.Save(_data);
        Changed?.Invoke();
    }
}
