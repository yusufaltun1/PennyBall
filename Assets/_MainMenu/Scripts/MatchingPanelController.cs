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
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject findingObject;
    [SerializeField] private TextMeshProUGUI findingText;
    [SerializeField] private GameObject sayacObject;
    [SerializeField] private TextMeshProUGUI sayacText;
    [SerializeField] private RectTransform vsObject;
    [SerializeField] private Image vsImage;

    [Header("Avatars")]
    [SerializeField] private AvatarSpriteLibrary avatarLibrary;

    [Header("Settings")]
    [SerializeField] private GameFeedbackAudioLibrary audioLibrary;
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private float slideDuration = 0.5f;
    [SerializeField] private float countdownStepDuration = 1f;
    [SerializeField] private int countdownStart = 3;
    [SerializeField] private float hornLeadTime = 2f;
    [SerializeField] private float vsRevealDuration = 0.22f;
    [SerializeField] private float vsStartScale = 20f;
    [SerializeField] private float vsEndScale = 1f;
    [SerializeField] private float vsEasePower = 3f;

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
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (vsObject == null)
        {
            Transform vsTransform = transform.Find("Players/VS");
            if (vsTransform != null)
            {
                vsObject = vsTransform as RectTransform;
            }
        }

        if (vsImage == null && vsObject != null)
        {
            vsImage = vsObject.GetComponent<Image>();
        }
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

    private void PlayThunderSound()
    {
        GameFeedbackSettingsService.EnsureLoaded();
        if (!GameFeedbackSettingsService.SoundEffectsEnabled)
        {
            return;
        }

        ResolveAudioLibrary();
        if (audioLibrary == null || audioLibrary.thunder == null || audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(audioLibrary.thunder, 1f);
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
        PrepareVsHiddenState();

        if (findingText != null)
        {
            findingText.fontSize = 64f;
            findingText.text = OnlineFeatureFlags.OnlineMatchmakingEnabled
                ? "Finding a match..."
                : "Finding opponent...";
        }
    }

    private IEnumerator DoMatchmakingSequence()
    {
        IReadOnlyList<BotPlayerEntry> shufflePool = GetShufflePool();

        yield return SlidePanelUp();

        isShuffling = true;

        // Online matchmaking (flag açıksa) — UI shuffle ile paralel
        MatchmakingResult matchResult = null;
        bool matchmakingDone = false;
        if (OnlineFeatureFlags.OnlineMatchmakingEnabled)
        {
            ResolveOnlineOpponentAsync(result =>
            {
                matchResult = result;
                matchmakingDone = true;
            });
        }
        else
        {
            BotPlayerEntry bot = LeagueService.Instance?.PickOpponentForNextMatch();
            if (bot != null)
            {
                MatchSessionContext.SetOpponent(bot);
                MatchSessionContext.SetOnlineMatch(false, null, null);
            }

            matchResult = MatchmakingResult.BotFallback(bot, "flag_disabled");
            matchmakingDone = true;
        }

        // Avatar shuffle + loop dönmesi: eşleşme bitene kadar sürer (min 3 sn).
        // Eşleşince isShuffling=false → dönme durur, final avatar kilitlenir.
        const float minShuffleSeconds = 3f;
        const float maxShuffleSeconds = 60f;
        float shuffleElapsed = 0f;
        while (true)
        {
            ShowRandomBotFromPool(shufflePool);
            yield return new WaitForSeconds(0.25f);
            shuffleElapsed += 0.25f;

            bool minTimeReached = shuffleElapsed >= minShuffleSeconds;
            if (minTimeReached && matchmakingDone)
            {
                break;
            }

            if (shuffleElapsed >= maxShuffleSeconds)
            {
                break;
            }
        }

        // Hâlâ bitmediyse bot fallback
        if (!matchmakingDone)
        {
            matchResult = MatchmakingResult.BotFallback(
                LeagueService.Instance?.PickOpponentForNextMatch(),
                "ui_timeout");
            if (matchResult.Source == MatchOpponentSource.Bot && MatchSessionContext.CurrentOpponent == null
                && LeagueService.Instance != null)
            {
                BotPlayerEntry bot = LeagueService.Instance.PickOpponentForNextMatch();
                MatchSessionContext.SetOpponent(bot);
                MatchSessionContext.SetOnlineMatch(false, null, null);
            }
        }

        // Eşleşti → dönme dursun, rakip avatarı kilitlensin.
        isShuffling = false;
        if (loopImage != null)
        {
            loopImage.rectTransform.localRotation = Quaternion.identity;
            loopImage.gameObject.SetActive(false);
        }

        findingObject?.SetActive(false);

        BotPlayerEntry opponent = MatchSessionContext.CurrentOpponent;
        ShowOpponent(opponent);

        bool isHuman = matchResult != null && matchResult.Source == MatchOpponentSource.Human;
        Debug.Log(
            $"[Matching] Locked opponent='{opponent?.displayName}' " +
            $"human={isHuman} room='{PendingPhotonSession.SessionName}'");

        GameAnalytics.Track("matchmaking_completed", new Dictionary<string, string>
        {
            { "opponent_type", matchResult != null && matchResult.Source == MatchOpponentSource.Human ? "human" : "bot" },
            { "timed_out", matchResult != null && matchResult.TimedOut ? "true" : "false" },
            { "reason", matchResult?.FailReason ?? "" }
        });

        if (OnlineFeatureFlags.OnlineOnlyMatches
            && (matchResult == null || !matchResult.Success || matchResult.Source != MatchOpponentSource.Human))
        {
            if (findingText != null)
            {
                findingText.text = "No opponent found";
                findingObject?.SetActive(true);
            }

            isRunning = false;
            matchmakingCoroutine = null;
            yield break;
        }

        yield return PlayVsRevealAnimation();

        sayacObject?.SetActive(true);

        bool hornPlayed = false;
        for (int count = countdownStart; count >= 0; count--)
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

    static async void ResolveOnlineOpponentAsync(System.Action<MatchmakingResult> onDone)
    {
        try
        {
            MatchmakingResult result = await OnlineMatchFlow.ResolveOpponentAsync();
            onDone?.Invoke(result);
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Matching] Online resolve failed: {ex.Message}");
            onDone?.Invoke(MatchmakingResult.Fail(ex.Message));
        }
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

        scoreText?.SetText(Random.Range(50, 600).ToString());
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

        scoreText?.SetText(GetOpponentStandingPoints(opponent).ToString());
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

    private int GetOpponentStandingPoints(BotPlayerEntry bot)
    {
        LeagueSaveData save = LeagueService.Instance?.Save;
        if (save?.standings == null)
        {
            return 0;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];
            if (!entry.isPlayer && entry.botId == bot.id)
            {
                return entry.points;
            }
        }

        return 0;
    }

    private void ShowPlaceholder()
    {
        opponentNameText?.SetText("---");
        scoreText?.SetText("0");
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

    private void PrepareVsHiddenState()
    {
        if (vsObject == null)
        {
            return;
        }

        vsObject.localScale = Vector3.one * vsStartScale;
        SetVsAlpha(0f);
        vsObject.gameObject.SetActive(false);
    }

    private IEnumerator PlayVsRevealAnimation()
    {
        if (vsObject == null)
        {
            yield break;
        }

        vsObject.gameObject.SetActive(true);
        vsObject.localScale = Vector3.one * vsStartScale;
        SetVsAlpha(0f);
        PlayThunderSound();

        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, vsRevealDuration);
        float easePower = Mathf.Max(1f, vsEasePower);

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInPower(Mathf.Clamp01(elapsed / safeDuration), easePower);
            float scale = Mathf.LerpUnclamped(vsStartScale, vsEndScale, t);
            vsObject.localScale = new Vector3(scale, scale, 1f);
            SetVsAlpha(t);
            yield return null;
        }

        vsObject.localScale = Vector3.one * vsEndScale;
        SetVsAlpha(1f);
    }

    private void SetVsAlpha(float alpha)
    {
        if (vsImage == null)
        {
            return;
        }

        Color color = vsImage.color;
        color.a = alpha;
        vsImage.color = color;
    }

    private static float EaseInPower(float t, float power)
    {
        return Mathf.Pow(t, power);
    }
}
