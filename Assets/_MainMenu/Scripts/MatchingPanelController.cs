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
    [SerializeField] private Button cancelButton;

    [Header("Play with Friends (Create/Join)")]
    [SerializeField] private GameObject codeContainer;
    [SerializeField] private TextMeshProUGUI codeText;

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

    private enum PartyFlowMode { QuickMatch, CreateRoom, JoinRoom, Rematch }

    private bool isShuffling;
    private bool isRunning;
    private bool cancelButtonBound;
    private PartyFlowMode partyMode = PartyFlowMode.QuickMatch;
    private string joinCode;
    private string activeRoomCode;
    private string rematchRoomName;
    private string rematchOpponentUserId;
    private string rematchOpponentDisplayName;
    private int rematchOpponentAvatarIndex;
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
        BindCancelButton();
    }

    private void OnDestroy()
    {
        UnbindCancelButton();
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

        ResolveCancelButton();
    }

    private void ResolveCancelButton()
    {
        if (cancelButton != null)
        {
            return;
        }

        Transform found = transform.Find("Btn_Cancel");
        if (found == null)
        {
            found = transform.Find("Cancel");
        }

        if (found == null)
        {
            found = transform.Find("Button");
        }

        if (found != null)
        {
            cancelButton = found.GetComponent<Button>();
        }
    }

    private void BindCancelButton()
    {
        ResolveCancelButton();
        if (cancelButtonBound || cancelButton == null)
        {
            return;
        }

        cancelButton.onClick.AddListener(OnCancelButtonPressed);
        cancelButtonBound = true;
    }

    private void UnbindCancelButton()
    {
        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(OnCancelButtonPressed);
        }

        cancelButtonBound = false;
    }

    private void OnCancelButtonPressed()
    {
        CancelMatchFlow();
    }

    /// <summary>Eşleşmeyi iptal eder, Matching panel'i kapatır, Main Menu'ye döner.</summary>
    public void CancelMatchFlow()
    {
        if (!isRunning && !gameObject.activeSelf)
        {
            return;
        }

        MainMenuClickSound.Play();

        GameAnalytics.Track("matchmaking_cancelled", new Dictionary<string, string>
        {
            { "league", LeagueService.Instance != null ? LeagueService.Instance.PlayerLeague.ToString() : "1" },
            { "player_level", WalletService.Level.ToString() }
        });

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
        MatchSessionContext.Clear();

        if (partyMode == PartyFlowMode.CreateRoom && !string.IsNullOrEmpty(activeRoomCode))
        {
            _ = PartyRoomService.CancelRoomAsync(activeRoomCode);
        }

        partyMode = PartyFlowMode.QuickMatch;
        joinCode = null;
        activeRoomCode = null;

        ResetUiState();
        gameObject.SetActive(false);

        GameFeedbackSettingsService.EnsureLoaded();
        if (GameFeedbackSettingsService.MusicEnabled)
        {
            MainMenuMusicController.Instance?.Play();
        }

        Debug.Log("[Matching] Cancel — eşleşme iptal, Main Menu.");
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
        partyMode = PartyFlowMode.QuickMatch;
        joinCode = null;
        BeginFlowInternal();
    }

    /// <summary>Play with Friends > Create: 6 haneli kod üretir, bu panelde gösterir.</summary>
    public void BeginCreateRoomFlow()
    {
        partyMode = PartyFlowMode.CreateRoom;
        joinCode = null;
        BeginFlowInternal();
    }

    /// <summary>Play with Friends > Join: verilen kodla aynı odaya katılır.</summary>
    public void BeginJoinRoomFlow(string code)
    {
        partyMode = PartyFlowMode.JoinRoom;
        joinCode = code;
        BeginFlowInternal();
    }

    /// <summary>
    /// ResultPanel > RePlay > "Play Again": host/guest ayrımı yok, yeni kod üretmeye/yazmaya
    /// gerek yok — önceki maçın aynı Photon odasına tekrar katılıp rakibi bekler. Panel MainMenu
    /// yüklenir yüklenmez hemen gösterilir (Create/Join ile aynı "bekleniyor" ekranı), böylece
    /// ham MainMenu görünmeden doğrudan matchmaking ekranına geçilmiş olur.
    /// </summary>
    public void BeginRematchFlow(
        string roomName, string opponentUserId, string opponentDisplayName, int opponentAvatarIndex)
    {
        partyMode = PartyFlowMode.Rematch;
        joinCode = null;
        rematchRoomName = roomName;
        rematchOpponentUserId = opponentUserId;
        rematchOpponentDisplayName = opponentDisplayName;
        rematchOpponentAvatarIndex = opponentAvatarIndex;
        BeginFlowInternal();
    }

    private void BeginFlowInternal()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        activeRoomCode = null;
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

        BindCancelButton();
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;
        }

        StopFootballGameMusic();
        MainMenuMusicController.StopMusic();
        PlayFindingSound();

        matchmakingCoroutine = StartCoroutine(DoMatchmakingSequence());
    }

    /// <summary>
    /// Host, Container_CodeGenerated ekranında kalarak Photon'a zaten bağlandı ve arkadaşını buldu
    /// (bkz. MatchPresetPanelController) — burada tekrar bağlanmaya/beklemeye çalışmadan doğrudan
    /// "bulundu" görselinden (VS reveal + geri sayım + Game sahnesi) devam edilir.
    /// </summary>
    public void ShowMatchFoundAndCountdown(MatchmakingResult matchResult, string code)
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        partyMode = PartyFlowMode.CreateRoom;
        joinCode = null;
        activeRoomCode = code;

        ResetUiState();

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        BindCancelButton();
        if (cancelButton != null)
        {
            cancelButton.gameObject.SetActive(true);
            cancelButton.interactable = true;
        }

        StopFootballGameMusic();
        MainMenuMusicController.StopMusic();

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
        }

        matchmakingCoroutine = StartCoroutine(DoAlreadyMatchedSequence());
    }

    private IEnumerator DoAlreadyMatchedSequence()
    {
        yield return SlidePanelUp();

        isShuffling = false;
        findingObject?.SetActive(false);
        codeContainer?.SetActive(false);
        if (loopImage != null)
        {
            loopImage.rectTransform.localRotation = Quaternion.identity;
            loopImage.gameObject.SetActive(false);
        }

        BotPlayerEntry partyOpponent = MatchSessionContext.CurrentOpponent;
        ShowOpponent(partyOpponent);

        GameAnalytics.Track("matchmaking_completed", new Dictionary<string, string>
        {
            { "opponent_type", "human" },
            { "timed_out", "false" },
            { "reason", "create_room" }
        });

        yield return PlayVsRevealAnimation();

        sayacObject?.SetActive(true);
        yield return RunCountdownAndLoadScene();
    }

    private void ResetUiState()
    {
        rectTransform ??= GetComponent<RectTransform>();
        EnsureStretchToCanvas();
        if (rectTransform != null)
        {
            // Stretch panel: ekranın altında gizli başlangıç.
            float hiddenY = -GetCanvasHeight();
            rectTransform.anchoredPosition = new Vector2(0f, hiddenY);
        }

        loopImage?.gameObject.SetActive(true);
        findingObject?.SetActive(true);
        sayacObject?.SetActive(false);
        PrepareVsHiddenState();

        if (findingText != null)
        {
            findingText.fontSize = 64f;
            findingText.text = partyMode switch
            {
                PartyFlowMode.CreateRoom => LocalizationService.Get("party.waiting_for_friend"),
                PartyFlowMode.JoinRoom => LocalizationService.Get("party.waiting_for_friend"),
                PartyFlowMode.Rematch => LocalizationService.Get("party.waiting_for_friend"),
                _ => OnlineFeatureFlags.OnlineMatchmakingEnabled
                    ? LocalizationService.Get("matching.finding")
                    : LocalizationService.Get("matching.finding_offline")
            };
        }

        // Rematch'te gösterilecek bir kod yok (aynı odaya sessizce tekrar katılıyoruz).
        bool showCode = partyMode == PartyFlowMode.CreateRoom || partyMode == PartyFlowMode.JoinRoom;
        codeContainer?.SetActive(showCode);
        if (showCode)
        {
            codeText?.SetText("------");
        }
    }

    private void EnsureStretchToCanvas()
    {
        rectTransform ??= GetComponent<RectTransform>();
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = Vector2.zero;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private float GetCanvasHeight()
    {
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

        if (rectTransform != null && rectTransform.rect.height > 1f)
        {
            canvasHeight = rectTransform.rect.height;
        }

        return canvasHeight;
    }

    private IEnumerator DoMatchmakingSequence()
    {
        IReadOnlyList<BotPlayerEntry> shufflePool = GetShufflePool();

        yield return SlidePanelUp();

        isShuffling = true;

        MatchmakingResult matchResult = null;
        bool matchmakingDone = false;

        if (partyMode == PartyFlowMode.CreateRoom)
        {
            ResolveCreateRoomAsync(result =>
            {
                matchResult = result;
                matchmakingDone = true;
            });
        }
        else if (partyMode == PartyFlowMode.JoinRoom)
        {
            ResolveJoinRoomAsync(joinCode, result =>
            {
                matchResult = result;
                matchmakingDone = true;
            });
        }
        else if (partyMode == PartyFlowMode.Rematch)
        {
            ResolveRematchAsync(result =>
            {
                matchResult = result;
                matchmakingDone = true;
            });
        }
        else if (OnlineFeatureFlags.OnlineMatchmakingEnabled)
        {
            // Online matchmaking (flag açıksa) — UI shuffle ile paralel
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

        bool isPartyFlow = partyMode != PartyFlowMode.QuickMatch;

        if (isPartyFlow)
        {
            // Arkadaşla oynama: bot shuffle yok, sadece kod + spinner ile bekle.
            // PartyRoomService kendi içinde ~120sn timeout uyguluyor, burada sadece bekliyoruz.
            const float partyWaitCeilingSeconds = 130f;
            float waitElapsed = 0f;
            while (!matchmakingDone && waitElapsed < partyWaitCeilingSeconds)
            {
                yield return new WaitForSeconds(0.25f);
                waitElapsed += 0.25f;
            }

            isShuffling = false;
            if (loopImage != null)
            {
                loopImage.rectTransform.localRotation = Quaternion.identity;
                loopImage.gameObject.SetActive(false);
            }

            if (matchResult == null || !matchResult.Success)
            {
                // Arkadaş bulunamadı/bağlanamadı — bot'a düşmeden hata göster, kullanıcı iptal edebilir.
                if (findingText != null)
                {
                    findingText.text = LocalizationService.Get(
                        partyMode == PartyFlowMode.CreateRoom ? "party.create_failed" : "party.join_failed");
                }

                isRunning = false;
                matchmakingCoroutine = null;
                yield break;
            }

            findingObject?.SetActive(false);
            codeContainer?.SetActive(false);

            BotPlayerEntry partyOpponent = MatchSessionContext.CurrentOpponent;
            ShowOpponent(partyOpponent);

            GameAnalytics.Track("matchmaking_completed", new Dictionary<string, string>
            {
                { "opponent_type", "human" },
                { "timed_out", "false" },
                { "reason", partyMode == PartyFlowMode.CreateRoom ? "create_room" : "join_room" }
            });

            yield return PlayVsRevealAnimation();

            sayacObject?.SetActive(true);
            yield return RunCountdownAndLoadScene();
            yield break;
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
        yield return RunCountdownAndLoadScene();
    }

    /// <summary>Sayaç (countdownStart→0) + boynuz sesi + Game sahnesine geçiş. Quick-match ve Create/Join ortak.</summary>
    private IEnumerator RunCountdownAndLoadScene()
    {
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

    async void ResolveCreateRoomAsync(System.Action<MatchmakingResult> onDone)
    {
        try
        {
            RoomCodeResult created = await PartyRoomService.RequestCreateCodeAsync();
            if (!created.Success)
            {
                onDone?.Invoke(MatchmakingResult.Fail(created.Reason));
                return;
            }

            // Kod RPC'den gelir gelmez göster — arkadaş beklemeden önce.
            activeRoomCode = created.Code;
            codeText?.SetText(created.Code);

            RoomCodeResult connected = await PartyRoomService.ConnectAndWaitForOpponentAsync(created);
            onDone?.Invoke(connected.Success
                ? connected.Match
                : MatchmakingResult.Fail(connected.Reason, timedOut: connected.Reason == "timeout"));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Matching] Create room failed: {ex.Message}");
            onDone?.Invoke(MatchmakingResult.Fail(ex.Message));
        }
    }

    async void ResolveJoinRoomAsync(string code, System.Action<MatchmakingResult> onDone)
    {
        try
        {
            RoomCodeResult joined = await PartyRoomService.RequestJoinCodeAsync(code);
            if (!joined.Success)
            {
                onDone?.Invoke(MatchmakingResult.Fail(joined.Reason));
                return;
            }

            RoomCodeResult connected = await PartyRoomService.ConnectAndWaitForOpponentAsync(joined);
            onDone?.Invoke(connected.Success
                ? connected.Match
                : MatchmakingResult.Fail(connected.Reason, timedOut: connected.Reason == "timeout"));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Matching] Join room failed: {ex.Message}");
            onDone?.Invoke(MatchmakingResult.Fail(ex.Message));
        }
    }

    async void ResolveRematchAsync(System.Action<MatchmakingResult> onDone)
    {
        try
        {
            RoomCodeResult result = await PartyRoomService.RejoinRoomAsync(
                rematchRoomName, rematchOpponentUserId, rematchOpponentDisplayName, rematchOpponentAvatarIndex);

            onDone?.Invoke(result.Success
                ? result.Match
                : MatchmakingResult.Fail(result.Reason, timedOut: result.Reason == "timeout"));
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Matching] Rematch failed: {ex.Message}");
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

        EnsureStretchToCanvas();

        // Eski davranış: alttan yukarı kayarak tam ekranı kapla.
        // Stretch + merkez pivot: gizli = -height, görünür = 0.
        float hiddenY = -GetCanvasHeight();
        const float targetY = 0f;

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
