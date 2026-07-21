using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeagueChangePresenter : MonoBehaviour
{
    [SerializeField] RectTransform titleText;
    [SerializeField] RectTransform coins;
    [SerializeField] RectTransform xp;
    [SerializeField] RectTransform buttons;
    [SerializeField] TextMeshProUGUI titleLabel;
    [SerializeField] TextMeshProUGUI coinsLabel;
    [SerializeField] TextMeshProUGUI xpLabel;
    [SerializeField] Button continueButton;
    [SerializeField] Button adButton;

    [SerializeField] float stepDelay = 0.25f;
    [SerializeField] float buttonsDelayAfterRewards = 0.25f;
    [SerializeField] float entryDuration = 0.5f;
    [SerializeField] float entryOffset = 200f;
    [SerializeField] float buttonsOffscreenPadding = 120f;

    readonly Dictionary<RectTransform, RectState> _finalStates = new();
    RectTransform _canvasRect;
    Coroutine _routine;
    bool _closeBound;
    bool _claimed;

    struct RectState
    {
        public Vector2 AnchoredPosition;
        public Vector3 LocalScale;
    }

    void Awake()
    {
        ResolveReferences();
        BindCloseButtons();
    }

    void OnEnable()
    {
        ApplyPendingResultToUi();

        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        _routine = StartCoroutine(PlaySequence());
    }

    void OnDisable()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    void OnDestroy()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveListener(OnClaimClicked);
        }

        if (adButton != null)
        {
            adButton.onClick.RemoveListener(OnClaimX2Clicked);
        }
    }

    public bool ShowIfPending()
    {
        if (LeagueService.Instance == null)
        {
            Hide();
            return false;
        }

        LeagueService.Instance.ResolveSeasonIfNeeded();

        if (!LeagueService.Instance.HasPendingSeasonResult)
        {
            Hide();
            return false;
        }

        Show();
        return true;
    }

    public void Show()
    {
        _claimed = false;
        ResolveReferences();
        BindCloseButtons();
        ApplyPendingResultToUi();
        SetButtonsInteractable(true);
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Close()
    {
        ClaimAndClose(1);
    }

    void OnClaimClicked()
    {
        ClaimAndClose(1);
    }

    void OnClaimX2Clicked()
    {
        if (_claimed)
        {
            return;
        }

        SetButtonsInteractable(false);

        if (AdsService.Instance == null)
        {
            Debug.LogWarning("[LeagueChange] AdsService yok, x2 reklam gösterilemedi.");
            SetButtonsInteractable(true);
            return;
        }

        AdsService.Instance.ShowRewarded(
            onRewarded: () => ClaimAndClose(2),
            onFailed: () =>
            {
                Debug.LogWarning("[LeagueChange] Rewarded reklam başarısız / izlenmedi.");
                SetButtonsInteractable(true);
            });
    }

    void ClaimAndClose(int multiplier)
    {
        if (_claimed)
        {
            return;
        }

        _claimed = true;
        SetButtonsInteractable(false);

        if (LeagueService.Instance != null && LeagueService.Instance.HasPendingSeasonResult)
        {
            LeagueService.Instance.ClaimPendingSeasonReward(multiplier);
        }

        Hide();
    }

    void SetButtonsInteractable(bool interactable)
    {
        if (continueButton != null)
        {
            continueButton.interactable = interactable;
        }

        if (adButton != null)
        {
            adButton.interactable = interactable;
        }
    }

    void ApplyPendingResultToUi()
    {
        ResolveReferences();

        if (LeagueService.Instance != null
            && LeagueService.Instance.TryGetPendingSeasonResult(out LeagueSeasonResult result))
        {
            if (titleLabel != null)
            {
                if (result.Promoted)
                {
                    titleLabel.SetText($"You promoted to\n{LeagueConfig.GetLeagueName(result.NewLeague)}!");
                }
                else if (result.FinalRank == 1 && result.NewLeague >= LeagueConfig.LeagueCount)
                {
                    titleLabel.SetText($"You finished 1st in\n{LeagueConfig.GetLeagueName(result.NewLeague)}!");
                }
                else
                {
                    titleLabel.SetText("Your league did not change!");
                }
            }

            bool showRewards = result.RewardCoins > 0 || result.RewardXp > 0;
            if (coins != null)
            {
                coins.gameObject.SetActive(showRewards);
            }

            if (xp != null)
            {
                xp.gameObject.SetActive(showRewards);
            }

            if (adButton != null)
            {
                adButton.gameObject.SetActive(showRewards);
            }

            if (showRewards)
            {
                if (coinsLabel != null)
                {
                    coinsLabel.SetText($"{result.RewardCoins} Coins");
                }

                if (xpLabel != null)
                {
                    xpLabel.SetText($"+{result.RewardXp} XP");
                }
            }

            return;
        }

        if (titleLabel != null)
        {
            titleLabel.SetText("Your league did not change!");
        }
    }

    void BindCloseButtons()
    {
        if (_closeBound)
        {
            return;
        }

        if (continueButton == null)
        {
            continueButton = FindButtonByName("Btn_Continue");
        }

        if (adButton == null)
        {
            adButton = FindButtonByName("Btn_Ad");
        }

        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnClaimClicked);
        }

        if (adButton != null)
        {
            adButton.onClick.AddListener(OnClaimX2Clicked);
        }

        _closeBound = continueButton != null || adButton != null;
    }

    Button FindButtonByName(string buttonName)
    {
        Transform found = FindDeepChild(transform, buttonName);
        return found != null ? found.GetComponent<Button>() : null;
    }

    void ResolveReferences()
    {
        RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (rect == transform as RectTransform)
            {
                continue;
            }

            switch (rect.name)
            {
                case "Text (TMP)":
                case "Title":
                    if (titleText == null)
                    {
                        titleText = rect;
                    }

                    if (titleLabel == null)
                    {
                        titleLabel = rect.GetComponent<TextMeshProUGUI>();
                    }
                    break;
                case "Coins" when coins == null:
                    coins = rect;
                    break;
                case "XP" when xp == null:
                    xp = rect;
                    break;
                case "Buttons" when rect.parent == transform && buttons == null:
                    buttons = rect;
                    break;
            }
        }

        if (titleLabel == null && titleText != null)
        {
            titleLabel = titleText.GetComponent<TextMeshProUGUI>();
        }

        if (coinsLabel == null && coins != null)
        {
            coinsLabel = FindRewardLabel(coins, "Coin");
        }

        if (xpLabel == null && xp != null)
        {
            xpLabel = FindRewardLabel(xp, "XP");
        }
    }

    static TextMeshProUGUI FindRewardLabel(RectTransform root, string keyword)
    {
        if (root == null)
        {
            return null;
        }

        TextMeshProUGUI[] labels = root.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = 0; i < labels.Length; i++)
        {
            string text = labels[i].text;
            if (text.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0
                || text.Contains("+"))
            {
                return labels[i];
            }
        }

        return labels.Length > 0 ? labels[labels.Length - 1] : null;
    }

    void CacheFinalStates()
    {
        _finalStates.Clear();
        _canvasRect = GetComponentInParent<Canvas>()?.GetComponent<RectTransform>();
        CacheElement(titleText);
        CacheElement(coins);
        CacheElement(xp);
        CacheElement(buttons);
    }

    void CacheElement(RectTransform rect)
    {
        if (rect == null)
        {
            return;
        }

        bool wasActive = rect.gameObject.activeSelf;
        rect.gameObject.SetActive(true);
        _finalStates[rect] = new RectState
        {
            AnchoredPosition = rect.anchoredPosition,
            LocalScale = rect.localScale
        };
        if (!wasActive)
        {
            rect.gameObject.SetActive(false);
        }
    }

    IEnumerator PlaySequence()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        ResolveReferences();
        CacheFinalStates();

        bool showRewards = LeagueService.Instance != null
            && LeagueService.Instance.TryGetPendingSeasonResult(out LeagueSeasonResult result)
            && (result.RewardCoins > 0 || result.RewardXp > 0);

        HideUntilAnimated(titleText);
        if (showRewards)
        {
            HideUntilAnimated(coins);
            HideUntilAnimated(xp);
        }
        else
        {
            if (coins != null)
            {
                coins.gameObject.SetActive(false);
            }

            if (xp != null)
            {
                xp.gameObject.SetActive(false);
            }
        }

        HideUntilAnimated(buttons);

        if (titleText != null)
        {
            yield return AnimateSlideIn(titleText, useCanvasOffscreen: false);
        }

        if (showRewards)
        {
            if (stepDelay > 0f)
            {
                yield return new WaitForSeconds(stepDelay);
            }

            if (coins != null)
            {
                yield return AnimateSlideIn(coins, useCanvasOffscreen: false);
            }

            if (xp != null)
            {
                yield return AnimateSlideIn(xp, useCanvasOffscreen: false);
            }
        }

        if (buttonsDelayAfterRewards > 0f)
        {
            yield return new WaitForSeconds(buttonsDelayAfterRewards);
        }

        if (buttons != null)
        {
            yield return AnimateSlideIn(buttons, useCanvasOffscreen: true);
        }

        _routine = null;
    }

    static void HideUntilAnimated(RectTransform rect)
    {
        if (rect != null)
        {
            rect.gameObject.SetActive(false);
        }
    }

    Vector2 GetStartPosition(RectTransform rect, RectState finalState, bool useCanvasOffscreen)
    {
        if (useCanvasOffscreen)
        {
            float canvasHalfHeight = GetCanvasHalfHeight();
            return new Vector2(
                finalState.AnchoredPosition.x,
                finalState.AnchoredPosition.y - canvasHalfHeight - rect.rect.height - buttonsOffscreenPadding);
        }

        return new Vector2(
            finalState.AnchoredPosition.x,
            finalState.AnchoredPosition.y - entryOffset);
    }

    IEnumerator AnimateSlideIn(RectTransform rect, bool useCanvasOffscreen)
    {
        if (rect == null || !_finalStates.TryGetValue(rect, out RectState finalState))
        {
            yield break;
        }

        rect.gameObject.SetActive(true);
        Vector2 startPosition = GetStartPosition(rect, finalState, useCanvasOffscreen);
        rect.anchoredPosition = startPosition;
        rect.localScale = finalState.LocalScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, entryDuration);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutBack(Mathf.Clamp01(elapsed / duration));
            rect.anchoredPosition = Vector2.LerpUnclamped(
                startPosition,
                finalState.AnchoredPosition,
                t);
            yield return null;
        }

        rect.anchoredPosition = finalState.AnchoredPosition;
        rect.localScale = finalState.LocalScale;
    }

    float GetCanvasHalfHeight()
    {
        const float defaultHalfHeight = 1170f;

        if (_canvasRect == null || _canvasRect.rect.height <= 0f)
        {
            return defaultHalfHeight;
        }

        return _canvasRect.rect.height * 0.5f;
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
