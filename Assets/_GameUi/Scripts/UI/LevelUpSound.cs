using UnityEngine;

public static class LevelUpSound
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
        if (_audioLibrary == null || _audioLibrary.levelUp == null || _audioSource == null)
        {
            return;
        }

        _audioSource.PlayOneShot(_audioLibrary.levelUp);
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

        var audioObject = new GameObject("LevelUpSound");
        _audioSource = audioObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }
}
