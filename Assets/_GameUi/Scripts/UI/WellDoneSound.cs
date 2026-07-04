using UnityEngine;

public static class WellDoneSound
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
        AudioClip clip = _audioLibrary != null ? _audioLibrary.wellDone : null;
        if (clip == null && _audioLibrary != null)
        {
            clip = _audioLibrary.levelUp;
        }

        if (clip == null || _audioSource == null)
        {
            return;
        }

        _audioSource.PlayOneShot(clip);
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

        var audioObject = new GameObject("WellDoneSound");
        _audioSource = audioObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 0f;
    }
}
