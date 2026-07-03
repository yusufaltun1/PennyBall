using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LeagueChangePresenter : MonoBehaviour
{
    [SerializeField] RectTransform titleText;
    [SerializeField] RectTransform coins;
    [SerializeField] RectTransform xp;
    [SerializeField] RectTransform buttons;

    [SerializeField] float stepDelay = 0.25f;
    [SerializeField] float buttonsDelayAfterRewards = 0.25f;
    [SerializeField] float entryDuration = 0.5f;
    [SerializeField] float entryOffset = 200f;
    [SerializeField] float buttonsOffscreenPadding = 120f;

    readonly Dictionary<RectTransform, RectState> _finalStates = new();
    RectTransform _canvasRect;
    Coroutine _routine;

    struct RectState
    {
        public Vector2 AnchoredPosition;
        public Vector3 LocalScale;
    }

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
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

        _finalStates[rect] = new RectState
        {
            AnchoredPosition = rect.anchoredPosition,
            LocalScale = rect.localScale
        };
    }

    IEnumerator PlaySequence()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        ResolveReferences();
        CacheFinalStates();

        HideUntilAnimated(titleText);
        HideUntilAnimated(coins);
        HideUntilAnimated(xp);
        HideUntilAnimated(buttons);

        if (titleText != null)
        {
            yield return AnimateSlideIn(titleText, useCanvasOffscreen: false);
        }

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
}
