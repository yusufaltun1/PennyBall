using UnityEngine;

public class MainMenuMusicController : MonoBehaviour
{
    public static MainMenuMusicController Instance { get; private set; }

    [SerializeField] GameFeedbackAudioLibrary audioLibrary;
    [SerializeField] [Range(0f, 1f)] float volume = 0.3f;

    AudioSource _musicSource;

    void Awake()
    {
        Instance = this;

        _musicSource = GetComponent<AudioSource>();
        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
        }

        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _musicSource.loop = true;

        ResolveAudioLibrary();
    }

    void OnEnable()
    {
        GameFeedbackSettingsService.Changed += ApplyMusicSetting;
        GameFeedbackSettingsService.EnsureLoaded();
        ApplyMusicSetting();
    }

    void OnDisable()
    {
        GameFeedbackSettingsService.Changed -= ApplyMusicSetting;
    }

    void OnDestroy()
    {
        Stop();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    void ApplyMusicSetting()
    {
        if (GameFeedbackSettingsService.MusicEnabled)
        {
            Play();
        }
        else
        {
            Stop();
        }
    }

    public static void StopMusic()
    {
        Instance?.Stop();
    }

    public void Play()
    {
        ResolveAudioLibrary();
        if (audioLibrary == null || audioLibrary.mainTheme == null || _musicSource == null)
        {
            return;
        }

        if (_musicSource.isPlaying && _musicSource.clip == audioLibrary.mainTheme)
        {
            return;
        }

        _musicSource.Stop();
        _musicSource.clip = audioLibrary.mainTheme;
        _musicSource.volume = volume;
        _musicSource.Play();
    }

    public void Stop()
    {
        if (_musicSource != null)
        {
            _musicSource.Stop();
        }
    }

    void ResolveAudioLibrary()
    {
        if (audioLibrary != null)
        {
            return;
        }

        GameFeedbackAudioLibrary[] libraries = Resources.FindObjectsOfTypeAll<GameFeedbackAudioLibrary>();
        if (libraries.Length > 0)
        {
            audioLibrary = libraries[0];
        }
    }
}
