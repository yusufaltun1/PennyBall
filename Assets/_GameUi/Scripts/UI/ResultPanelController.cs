using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultPanelController : MonoBehaviour
{
    private enum ResultOutcome
    {
        None,
        Won,
        Lost,
        Draw
    }

    [System.Serializable]
    private struct OutcomeSprites
    {
        public Sprite icon;
        public Sprite p1;
        public Sprite p2;
        public Sprite p3;
        public Sprite resultLabel;
    }

    [Header("Playback")]
    [SerializeField] private bool loop;
    [SerializeField] private bool won;
    [SerializeField] private bool lost;
    [SerializeField] private bool draw;

    [Header("Outcome Sprites")]
    [SerializeField] private OutcomeSprites wonSprites;
    [SerializeField] private OutcomeSprites lostSprites;
    [SerializeField] private OutcomeSprites drawSprites;

    [Header("Animation Targets")]
    [SerializeField] private RectTransform resultLabel;
    [SerializeField] private RectTransform people;
    [SerializeField] private RectTransform icon;
    [SerializeField] private RectTransform p1;
    [SerializeField] private RectTransform p2;
    [SerializeField] private RectTransform p3;
    [SerializeField] private RectTransform buttons;
    [SerializeField] private ConfettiController confetti;
    [SerializeField] private Button continueButton;

    [Header("Navigation")]
    [SerializeField] private GameObject leagueStatusPanel;

    [Header("Rewards")]
    [SerializeField] private GameObject rewards;
    [SerializeField] private RectTransform rewardCoins;
    [SerializeField] private RectTransform rewardXp;
    [SerializeField] private TextMeshProUGUI earnedCoinsLabel;
    [SerializeField] private TextMeshProUGUI earnedXpLabel;
    [SerializeField] private float rewardsEntryOffset = 50f;
    [SerializeField] private float rewardsDuration = 0.5f;

    [Header("Timing")]
    [SerializeField] private float labelDuration = 0.55f;
    [SerializeField] private float peopleScaleDuration = 0.5f;
    [SerializeField] private float iconMoveDuration = 0.5f;
    [SerializeField] private float resultHoldDuration = 0.5f;
    [SerializeField] private float wonRewardsExtraDelay = 1f;
    [SerializeField] private float continueDelay = 1f;
    [SerializeField] private float continueDuration = 0.5f;
    [SerializeField] private float offscreenPadding = 120f;

    private Image resultLabelImage;
    private Image iconImage;
    private Image p1Image;
    private Image p2Image;
    private Image p3Image;

    private RectTransform canvasRect;
    private RectState labelFinal;
    private RectState iconFinal;
    private RectState peopleFinal;
    private readonly RectState[] personFinals = new RectState[3];
    private RectState continueFinal;
    private RectState coinsFinal;
    private RectState xpFinal;

    private CanvasGroup coinsCanvasGroup;
    private CanvasGroup xpCanvasGroup;

    private bool previousWon;
    private bool previousLost;
    private bool previousDraw;
    private Coroutine playCoroutine;

    public void ShowResult(MatchResultType result)
    {
        won  = result == MatchResultType.Win;
        lost = result == MatchResultType.Loss;
        draw = result == MatchResultType.Draw;

        if (earnedCoinsLabel != null)
            earnedCoinsLabel.text = $"+{MatchSessionContext.EarnedCoins}";
        if (earnedXpLabel != null)
        {
            earnedXpLabel.text = MatchSessionContext.LeveledUp
                ? $"+{MatchSessionContext.EarnedXp} XP  Seviye {MatchSessionContext.LevelAfter}!"
                : $"+{MatchSessionContext.EarnedXp} XP";
        }

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);  // OnEnable fires → HandleOutcomeChange çalışır
        else
            HandleOutcomeChange();
    }

    private struct RectState
    {
        public Vector2 AnchoredPosition;
        public Vector3 LocalScale;
    }

    private void Awake()
    {
        canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        resultLabelImage = resultLabel.GetComponent<Image>();
        iconImage = icon.GetComponent<Image>();
        p1Image = p1.GetComponent<Image>();
        p2Image = p2.GetComponent<Image>();
        p3Image = p3.GetComponent<Image>();

        if (confetti == null)
            confetti = GetComponentInChildren<ConfettiController>(true);

        if (continueButton == null && buttons != null)
            continueButton = buttons.GetComponentInChildren<Button>(true);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);

        // Inspector'da atanmamışsa sahnedeki LeagueStatusPresenter'ı bul
        if (leagueStatusPanel == null)
        {
            LeagueStatusPresenter presenter =
                FindFirstObjectByType<LeagueStatusPresenter>(FindObjectsInactive.Include);
            if (presenter != null)
                leagueStatusPanel = presenter.gameObject;
        }

        coinsCanvasGroup = GetOrAddCanvasGroup(rewardCoins);
        xpCanvasGroup = GetOrAddCanvasGroup(rewardXp);

        CacheFinalStates();
    }

    private void OnDestroy()
    {
        if (continueButton != null)
            continueButton.onClick.RemoveListener(OnContinueClicked);
    }

    public void OnContinueClicked()
    {
        if (leagueStatusPanel != null)
        {
            gameObject.SetActive(false);
            leagueStatusPanel.SetActive(true);
        }
        else
        {
            SceneManager.LoadScene(GameSceneNames.MainMenu);
        }
    }

    private void OnEnable()
    {
        previousWon = won;
        previousLost = lost;
        previousDraw = draw;
        HandleOutcomeChange();
    }

    private void OnDisable()
    {
        StopPresentation();
    }

    private void Update()
    {
        if (won == previousWon && lost == previousLost && draw == previousDraw)
        {
            return;
        }

        if (won && !previousWon)
        {
            lost = false;
            draw = false;
        }
        else if (lost && !previousLost)
        {
            won = false;
            draw = false;
        }
        else if (draw && !previousDraw)
        {
            won = false;
            lost = false;
        }

        previousWon = won;
        previousLost = lost;
        previousDraw = draw;

        if (isActiveAndEnabled)
        {
            HandleOutcomeChange();
        }
    }

    private void HandleOutcomeChange()
    {
        StopPresentation();

        ResultOutcome outcome = GetActiveOutcome();
        if (outcome != ResultOutcome.None)
        {
            ApplyOutcomeSprites(outcome);
            playCoroutine = StartCoroutine(RunPresentation());
            return;
        }

        ApplyFinalState();
    }

    private ResultOutcome GetActiveOutcome()
    {
        if (won)
        {
            return ResultOutcome.Won;
        }

        if (lost)
        {
            return ResultOutcome.Lost;
        }

        if (draw)
        {
            return ResultOutcome.Draw;
        }

        return ResultOutcome.None;
    }

    private void ApplyOutcomeSprites(ResultOutcome outcome)
    {
        OutcomeSprites sprites = outcome switch
        {
            ResultOutcome.Won => wonSprites,
            ResultOutcome.Lost => lostSprites,
            ResultOutcome.Draw => drawSprites,
            _ => default
        };

        if (sprites.resultLabel != null)
        {
            resultLabelImage.sprite = sprites.resultLabel;
        }

        if (sprites.icon != null)
        {
            iconImage.sprite = sprites.icon;
        }

        if (sprites.p1 != null)
        {
            p1Image.sprite = sprites.p1;
        }

        if (sprites.p2 != null)
        {
            p2Image.sprite = sprites.p2;
        }

        if (sprites.p3 != null)
        {
            p3Image.sprite = sprites.p3;
        }
    }

    private void StopPresentation()
    {
        if (playCoroutine != null)
        {
            StopCoroutine(playCoroutine);
            playCoroutine = null;
        }

        confetti?.Stop();
    }

    private void CacheFinalStates()
    {
        labelFinal = Capture(resultLabel);
        iconFinal = Capture(icon);
        peopleFinal = Capture(people);
        personFinals[0] = Capture(p1);
        personFinals[1] = Capture(p2);
        personFinals[2] = Capture(p3);
        continueFinal = Capture(buttons);
        coinsFinal = Capture(rewardCoins);
        xpFinal = Capture(rewardXp);
    }

    private static RectState Capture(RectTransform rect)
    {
        return new RectState
        {
            AnchoredPosition = rect.anchoredPosition,
            LocalScale = rect.localScale
        };
    }

    private IEnumerator RunPresentation()
    {
        do
        {
            PrepareInitialState();
            yield return PlayResultSequence();
        }
        while (loop && GetActiveOutcome() != ResultOutcome.None);

        ApplyFinalState(includeResultElements: false);
        playCoroutine = null;
    }

    private void PrepareInitialState()
    {
        float canvasHalfHeight = GetCanvasHalfHeight();

        resultLabel.gameObject.SetActive(true);
        resultLabel.anchoredPosition = new Vector2(
            labelFinal.AnchoredPosition.x,
            -canvasHalfHeight - resultLabel.rect.height * 0.5f - offscreenPadding);
        resultLabel.localScale = Vector3.one * 0.1f;

        people.gameObject.SetActive(false);
        people.anchoredPosition = peopleFinal.AnchoredPosition;
        people.localScale = peopleFinal.LocalScale;
        SetPersonScales(Vector3.one * 0.1f);

        icon.gameObject.SetActive(false);
        icon.anchoredPosition = Vector2.zero;
        icon.localScale = iconFinal.LocalScale;

        buttons.gameObject.SetActive(false);
        buttons.anchoredPosition = new Vector2(
            continueFinal.AnchoredPosition.x,
            continueFinal.AnchoredPosition.y - canvasHalfHeight - buttons.rect.height - offscreenPadding);
        buttons.localScale = continueFinal.LocalScale;

        if (rewards != null)
        {
            rewards.SetActive(false);
        }
    }

    private void ApplyFinalState(bool includeResultElements = true)
    {
        if (includeResultElements)
        {
            resultLabel.gameObject.SetActive(true);
            Apply(resultLabel, labelFinal);
            people.gameObject.SetActive(true);
            Apply(people, peopleFinal);
            Apply(p1, personFinals[0]);
            Apply(p2, personFinals[1]);
            Apply(p3, personFinals[2]);
            icon.gameObject.SetActive(true);
            Apply(icon, iconFinal);
        }
        else
        {
            resultLabel.gameObject.SetActive(false);
            people.gameObject.SetActive(false);
            icon.gameObject.SetActive(false);
        }

        if (rewards != null)
        {
            rewards.SetActive(true);
            Apply(rewardCoins, coinsFinal);
            Apply(rewardXp, xpFinal);
            SetCanvasGroupAlpha(coinsCanvasGroup, 1f);
            SetCanvasGroupAlpha(xpCanvasGroup, 1f);
        }

        buttons.gameObject.SetActive(true);
        Apply(buttons, continueFinal);
    }

    private IEnumerator PlayResultSequence()
    {
        yield return AnimateRect(
            resultLabel,
            resultLabel.anchoredPosition,
            labelFinal.AnchoredPosition,
            Vector3.one * 0.1f,
            labelFinal.LocalScale,
            labelDuration);

        people.gameObject.SetActive(true);
        SetPersonScales(Vector3.one * 0.1f);

        RectTransform[] persons = { p1, p2, p3 };
        for (int i = 0; i < persons.Length; i++)
        {
            yield return AnimateScale(
                persons[i],
                Vector3.one * 0.1f,
                personFinals[i].LocalScale,
                peopleScaleDuration);
        }

        icon.gameObject.SetActive(true);
        icon.anchoredPosition = Vector2.zero;
        yield return AnimatePosition(
            icon,
            Vector2.zero,
            iconFinal.AnchoredPosition,
            iconMoveDuration);

        if (GetActiveOutcome() == ResultOutcome.Won)
        {
            confetti?.Play();
        }

        yield return new WaitForSeconds(resultHoldDuration);

        if (GetActiveOutcome() == ResultOutcome.Won)
        {
            yield return new WaitForSeconds(wonRewardsExtraDelay);
        }

        icon.gameObject.SetActive(false);
        people.gameObject.SetActive(false);
        resultLabel.gameObject.SetActive(false);

        if (rewards != null)
        {
            rewards.SetActive(true);
            yield return AnimateRewardsEntry();
        }

        yield return new WaitForSeconds(continueDelay);

        buttons.gameObject.SetActive(true);
        Vector2 continueStart = buttons.anchoredPosition;
        yield return AnimatePosition(
            buttons,
            continueStart,
            continueFinal.AnchoredPosition,
            continueDuration);
    }

    private IEnumerator AnimateRect(
        RectTransform rect,
        Vector2 fromPosition,
        Vector2 toPosition,
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
            rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        rect.anchoredPosition = toPosition;
        rect.localScale = toScale;
    }

    private IEnumerator AnimatePosition(
        RectTransform rect,
        Vector2 fromPosition,
        Vector2 toPosition,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = Vector2.LerpUnclamped(fromPosition, toPosition, t);
            yield return null;
        }

        rect.anchoredPosition = toPosition;
    }

    private IEnumerator AnimateScale(
        RectTransform rect,
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            rect.localScale = Vector3.LerpUnclamped(fromScale, toScale, t);
            yield return null;
        }

        rect.localScale = toScale;
    }

    private void SetPersonScales(Vector3 scale)
    {
        p1.localScale = scale;
        p2.localScale = scale;
        p3.localScale = scale;
    }

    private IEnumerator AnimateRewardsEntry()
    {
        Vector2 coinsStart = GetRewardStartPosition(coinsFinal);
        Vector2 xpStart = GetRewardStartPosition(xpFinal);

        rewardCoins.anchoredPosition = coinsStart;
        rewardXp.anchoredPosition = xpStart;
        SetCanvasGroupAlpha(coinsCanvasGroup, 0f);
        SetCanvasGroupAlpha(xpCanvasGroup, 0f);

        float elapsed = 0f;

        while (elapsed < rewardsDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / rewardsDuration);
            float moveT = EaseOutBack(progress);

            rewardCoins.anchoredPosition = Vector2.LerpUnclamped(
                coinsStart,
                coinsFinal.AnchoredPosition,
                moveT);
            rewardXp.anchoredPosition = Vector2.LerpUnclamped(
                xpStart,
                xpFinal.AnchoredPosition,
                moveT);
            SetCanvasGroupAlpha(coinsCanvasGroup, progress);
            SetCanvasGroupAlpha(xpCanvasGroup, progress);
            yield return null;
        }

        Apply(rewardCoins, coinsFinal);
        Apply(rewardXp, xpFinal);
        SetCanvasGroupAlpha(coinsCanvasGroup, 1f);
        SetCanvasGroupAlpha(xpCanvasGroup, 1f);
    }

    private Vector2 GetRewardStartPosition(RectState finalState)
    {
        return new Vector2(
            finalState.AnchoredPosition.x,
            finalState.AnchoredPosition.y - rewardsEntryOffset);
    }

    private static CanvasGroup GetOrAddCanvasGroup(RectTransform rect)
    {
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null)
        {
            group = rect.gameObject.AddComponent<CanvasGroup>();
        }

        return group;
    }

    private static void SetCanvasGroupAlpha(CanvasGroup group, float alpha)
    {
        if (group != null)
        {
            group.alpha = alpha;
        }
    }

    private static void Apply(RectTransform rect, RectState state)
    {
        rect.anchoredPosition = state.AnchoredPosition;
        rect.localScale = state.LocalScale;
    }

    private float GetCanvasHalfHeight()
    {
        const float defaultHalfHeight = 1170f;

        if (canvasRect == null || canvasRect.rect.height <= 0f)
        {
            return defaultHalfHeight;
        }

        return canvasRect.rect.height * 0.5f;
    }

    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
