using UnityEngine;

public static class MainMenuClickSound
{
    static GameFeedbackAudioLibrary _audioLibrary;
    static AudioSource _audioSource;

    public static void Play()
    {
        GameFeedbackSettingsService.EnsureLoaded();
        if (!GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        EnsureReady();
        if (_audioLibrary == null || _audioLibrary.buttonClick == null || _audioSource == null)
        {
            return;
        }

        _audioSource.PlayOneShot(_audioLibrary.buttonClick);
    }

    static void EnsureReady()
    {
        if (_audioLibrary == null)
        {
            GameFeedbackAudioLibrary[] libraries = Resources.FindObjectsOfTypeAll<GameFeedbackAudioLibrary>();
            if (libraries.Length > 0)
            {
                _audioLibrary = libraries[0];
            }
        }

        if (_audioSource != null)
        {
            return;
        }

        var audioObject = new GameObject("MainMenuClickSound");
        _audioSource = audioObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }
}
