using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardView : MonoBehaviour
{
    [SerializeField] LeaderboardRowView _botRowPrefab;
    [SerializeField] LeaderboardRowView _playerRowPrefab;
    [SerializeField] Transform _content;
    [SerializeField] float _scrollDelay    = 0.3f;
    [SerializeField] float _scrollDuration = 1.0f;

    ScrollRect _scrollRect;
    Coroutine _scrollCoroutine;
    RectTransform _playerRowRect;

    void Awake()
    {
        _scrollRect = GetComponent<ScrollRect>();
    }

    void OnEnable()
    {
        if (LeagueService.Instance != null)
            LeagueService.Instance.StandingsUpdated += Rebuild;

        Rebuild();
    }

    void OnDisable()
    {
        if (LeagueService.Instance != null)
            LeagueService.Instance.StandingsUpdated -= Rebuild;

        StopScroll();
    }

    public void OnTabSelected()
    {
        StopScroll();
        _scrollCoroutine = StartCoroutine(ScrollToPlayerRoutine());
    }

    void Rebuild()
    {
        if (_botRowPrefab == null || _content == null)
            return;

        LeagueSaveData save = LeagueService.Instance?.Save;
        if (save?.standings == null)
            return;

        StopScroll();

        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        _playerRowRect = null;

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];

            LeaderboardRowView prefab = entry.isPlayer && _playerRowPrefab != null
                ? _playerRowPrefab
                : _botRowPrefab;

            LeaderboardRowView row = Instantiate(prefab, _content);
            row.Bind(rank: i + 1, displayName: entry.displayName, points: entry.points, isPlayer: entry.isPlayer);

            if (entry.isPlayer)
                _playerRowRect = row.GetComponent<RectTransform>();
        }
    }

    void StopScroll()
    {
        if (_scrollCoroutine != null)
        {
            StopCoroutine(_scrollCoroutine);
            _scrollCoroutine = null;
        }
    }

    IEnumerator ScrollToPlayerRoutine()
    {
        if (_scrollRect == null)
        {
            Debug.LogWarning("[Leaderboard] ScrollRect bulunamadı.");
            yield break;
        }

        if (_playerRowRect == null)
        {
            Debug.LogWarning("[Leaderboard] Player satırı bulunamadı.");
            yield break;
        }

        RectTransform content = _content as RectTransform;
        // viewport atanmamışsa ScrollRect'in kendi RectTransform'unu kullan
        RectTransform viewport = _scrollRect.viewport != null
            ? _scrollRect.viewport
            : (RectTransform)_scrollRect.transform;

        // Layout yerleşsin
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentH  = content.rect.height;
        float viewportH = viewport.rect.height;
        float scrollableH = contentH - viewportH;

        Debug.Log($"[Leaderboard] contentH={contentH:F0}  viewportH={viewportH:F0}  scrollableH={scrollableH:F0}  playerY={_playerRowRect.anchoredPosition.y:F0}");

        if (scrollableH <= 0f)
        {
            Debug.LogWarning("[Leaderboard] Scroll edilecek alan yok.");
            yield break;
        }

        float rowY = Mathf.Abs(_playerRowRect.anchoredPosition.y);
        rowY = Mathf.Max(0f, rowY - viewportH * 0.5f);
        float targetNormalized = 1f - Mathf.Clamp01(rowY / scrollableH);

        Debug.Log($"[Leaderboard] rowY={rowY:F0}  target={targetNormalized:F3}");

        _scrollRect.verticalNormalizedPosition = 1f;
        yield return new WaitForSeconds(_scrollDelay);

        float elapsed = 0f;
        while (elapsed < _scrollDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _scrollDuration);
            _scrollRect.verticalNormalizedPosition = Mathf.Lerp(1f, targetNormalized, t);
            yield return null;
        }

        _scrollRect.verticalNormalizedPosition = targetNormalized;
        _scrollCoroutine = null;
    }
}
