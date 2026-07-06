using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MatchingPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image opponentAvatarImage;
    [SerializeField] private Image loopImage;
    [SerializeField] private TextMeshProUGUI opponentNameText;
    [SerializeField] private TextMeshProUGUI playerLevelText;
    [SerializeField] private TextMeshProUGUI opponentLevelText;
    [SerializeField] private GameObject findingObject;
    [SerializeField] private TextMeshProUGUI findingText;
    [SerializeField] private GameObject sayacObject;
    [SerializeField] private TextMeshProUGUI sayacText;

    [Header("Avatars")]
    [SerializeField] private AvatarSpriteLibrary avatarLibrary;

    [Header("Settings")]
    [SerializeField] private GameFeedbackAudioLibrary audioLibrary;
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private float hornLeadTime = 2f;

    private bool isShuffling;
    private bool isRunning;
    private Coroutine matchmakingCoroutine;
    private Coroutine findingFadeCoroutine;
    private RectTransform rectTransform;
    private AudioSource audioSource;
    private AudioSource findingSource;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        findingSource = gameObject.AddComponent<AudioSource>();
        findingSource.playOnAwake = false;
        findingSource.spatialBlend = 0f;
        findingSource.loop = true;

        ResolveAudioLibrary();
    }

    private void ResolveAudioLibrary()
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

    private void PlayFindingSound()
    {
        GameFeedbackSettingsService.EnsureLoaded();
        if (!GameFeedbackSettingsService.MusicEnabled)
        {
            StopFindingMusic();
            return;
        }

        ResolveAudioLibrary();
        if (audioLibrary == null || audioLibrary.finding == null || findingSource == null)
        {
            return;
        }

        if (findingFadeCoroutine != null)
        {
            StopCoroutine(findingFadeCoroutine);
            findingFadeCoroutine = null;
        }

        findingSource.Stop();
        findingSource.clip = audioLibrary.finding;
        findingSource.volume = 1f;
        findingSource.pitch = 1f;
        findingSource.loop = true;
        findingSource.Play();
    }

    private void StopFindingMusic()
    {
        if (findingFadeCoroutine != null)
        {
            StopCoroutine(findingFadeCoroutine);
            findingFadeCoroutine = null;
        }

        if (findingSource != null)
        {
            findingSource.Stop();
            findingSource.volume = 0f;
            findingSource.loop = true;
        }
    }

    private void ApplyAudioSettings()
    {
        if (!GameFeedbackSettingsService.MusicEnabled)
        {
            StopFindingMusic();
            return;
        }

        if (isRunning && findingObject != null && findingObject.activeSelf)
        {
            PlayFindingSound();
        }
    }

    private void PlayHornSound(float findingFadeDuration)
    {
        GameFeedbackSettingsService.EnsureLoaded();
        if (!GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        ResolveAudioLibrary();
        if (audioLibrary == null || audioLibrary.horn == null || audioSource == null)
        {
            return;
        }

        BeginFindingFadeOut(findingFadeDuration);
        audioSource.PlayOneShot(audioLibrary.horn, 1f);
    }

    private void BeginFindingFadeOut(float duration)
    {
        if (findingFadeCoroutine != null)
        {
            StopCoroutine(findingFadeCoroutine);
        }

        findingFadeCoroutine = StartCoroutine(FadeOutFindingSound(duration));
    }

    private IEnumerator FadeOutFindingSound(float duration)
    {
        if (findingSource == null || !findingSource.isPlaying)
        {
            findingFadeCoroutine = null;
            yield break;
        }

        float startVolume = findingSource.volume;
        float elapsed = 0f;
        duration = Mathf.Max(0.01f, duration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            findingSource.volume = Mathf.Lerp(startVolume, 0f, elapsed / duration);
            yield return null;
        }

        findingSource.volume = 0f;
        findingSource.Stop();
        findingFadeCoroutine = null;
    }

    private void StopFootballGameMusic()
    {
        if (GameFeedback.Instance != null)
        {
            GameFeedback.Instance.StopBackgroundMusic();
            return;
        }

        GameFeedback feedback = FindFirstObjectByType<GameFeedback>();
        feedback?.StopBackgroundMusic();
    }

    private void OnEnable()
    {
        GameFeedbackSettingsService.Changed += ApplyAudioSettings;
    }

    private void OnDisable()
    {
        GameFeedbackSettingsService.Changed -= ApplyAudioSettings;

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
            matchmakingCoroutine = null;
        }

        isShuffling = false;
        isRunning = false;

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        StopFindingMusic();
    }

    private void Update()
    {
        if (isShuffling && loopImage != null)
        {
            loopImage.rectTransform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    public void BeginMatchFlow()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        ResetUiState();

        GameAnalytics.Track("matchmaking_started", new Dictionary<string, string>
        {
            { "league", LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague.ToString() : "1" },
            { "player_level", WalletService.Level.ToString() }
        });

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        StopFootballGameMusic();
        MainMenuMusicController.StopMusic();
        PlayFindingSound();

        matchmakingCoroutine = StartCoroutine(DoMatchmakingSequence());
    }

    private void ResetUiState()
    {
        rectTransform ??= GetComponent<RectTransform>();
        if (rectTransform != null)
            rectTransform.anchoredPosition = new Vector2(0f, 0f);

        loopImage?.gameObject.SetActive(true);
        findingObject?.SetActive(true);
        sayacObject?.SetActive(false);

        if (findingText != null)
        {
            findingText.fontSize = 64f;
            findingText.text = "Finding a match...";
        }

        RefreshPlayerLevel();
        opponentLevelText?.SetText("?");
    }

    private void RefreshPlayerLevel()
    {
        playerLevelText?.SetText(WalletService.Level.ToString());
    }

    private IEnumerator DoMatchmakingSequence()
    {
        // Gerçek rakibi seç — MatchSessionContext'e de otomatik yazılır
        BotPlayerEntry opponent = LeagueService.Instance?.PickOpponentForNextMatch();

        IReadOnlyList<BotPlayerEntry> shufflePool = GetShufflePool();

        yield return SlidePanelUp();

        isShuffling = true;

        float shuffleElapsed = 0f;
        while (shuffleElapsed < 3.0f)
        {
            shuffleElapsed += 0.25f;
            ShowRandomBotFromPool(shufflePool);
            yield return new WaitForSeconds(0.25f);
        }

        isShuffling = false;

        loopImage?.gameObject.SetActive(false);
        findingObject?.SetActive(false);

        // Shuffle bitti — gerçek rakibi kilitle
        ShowOpponent(opponent);

        sayacObject?.SetActive(true);

        bool hornPlayed = false;
        for (int count = 5; count >= 0; count--)
        {
            if (sayacText != null)
            {
                sayacText.fontSize = 96f;
                sayacText.text = count.ToString();
            }

            float timeUntilCounterEnds = (count + 1) * countdownStepDuration;
            float delayBeforeHorn = timeUntilCounterEnds - hornLeadTime;

            if (!hornPlayed && delayBeforeHorn >= 0f && delayBeforeHorn < countdownStepDuration)
            {
                if (delayBeforeHorn > 0f)
                {
                    yield return new WaitForSeconds(delayBeforeHorn);
                }

                float remainingPanelTime = (countdownStepDuration - delayBeforeHorn) + count * countdownStepDuration;
                PlayHornSound(remainingPanelTime);
                hornPlayed = true;
                yield return new WaitForSeconds(countdownStepDuration - delayBeforeHorn);
            }
            else
            {
                yield return new WaitForSeconds(countdownStepDuration);
            }
        }

        isRunning = false;
        matchmakingCoroutine = null;

        SceneManager.LoadScene(GameSceneNames.Game);
    }

    private IReadOnlyList<BotPlayerEntry> GetShufflePool()
    {
        if (LeagueService.Instance == null)
        {
            return System.Array.Empty<BotPlayerEntry>();
        }

        return BotPlayerCatalog.GetBotsForLeague(LeagueService.Instance.PlayerLeague);
    }

    private void ShowRandomBotFromPool(IReadOnlyList<BotPlayerEntry> pool)
    {
        if (pool == null || pool.Count == 0)
        {
            ShowPlaceholder();
            return;
        }

        BotPlayerEntry bot = pool[Random.Range(0, pool.Count)];

        opponentNameText?.SetText(bot.displayName);

        SetAvatar(bot.avatarIndex);

        opponentLevelText?.SetText(GetOpponentDisplayLevel(bot).ToString());
    }

    private void ShowOpponent(BotPlayerEntry opponent)
    {
        if (opponent == null)
        {
            ShowPlaceholder();
            return;
        }

        opponentNameText?.SetText(opponent.displayName);

        SetAvatar(opponent.avatarIndex);

        opponentLevelText?.SetText(GetOpponentDisplayLevel(opponent).ToString());
    }

    private void SetAvatar(int avatarIndex)
    {
        if (opponentAvatarImage == null) return;

        // Inspector'da atanmamışsa Resources'dan yükle
        avatarLibrary ??= AvatarSpriteLibrary.Load();

        if (avatarLibrary == null) return;

        Sprite sprite = avatarLibrary.Get(avatarIndex);
        if (sprite != null)
            opponentAvatarImage.sprite = sprite;
    }

    /// <summary>
    /// Bot zorluğuna göre oyuncu seviyesine yakın bir görüntü seviyesi üretir.
    /// </summary>
    private static int GetOpponentDisplayLevel(BotPlayerEntry bot)
    {
        if (bot == null)
        {
            return 1;
        }

        int playerLevel = WalletService.Level;
        int difficulty = Mathf.Clamp(bot.difficultyLevel, 1, 10);
        int delta = (difficulty - 5) * 2 + (bot.id % 5) - 2;
        return Mathf.Clamp(playerLevel + delta, 1, PlayerLevelProgression.MaxLevel);
    }

    private void ShowPlaceholder()
    {
        opponentNameText?.SetText("---");
        opponentLevelText?.SetText("?");
    }

    private IEnumerator SlidePanelUp()
    {
        if (rectTransform == null)
        {
            yield break;
        }

        float canvasHeight = 2340f;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform canvasRt = canvas.GetComponent<RectTransform>();
            if (canvasRt != null && canvasRt.rect.height > 0f)
            {
                canvasHeight = canvasRt.rect.height;
            }
        }

        const float hiddenY = 0f;
        float targetY = canvasHeight;

        rectTransform.anchoredPosition = new Vector2(0f, hiddenY);

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float easedT = t * t * (3f - 2f * t);
            rectTransform.anchoredPosition = new Vector2(0f, Mathf.Lerp(hiddenY, targetY, easedT));
            yield return null;
        }

        rectTransform.anchoredPosition = new Vector2(0f, targetY);
    }
}
