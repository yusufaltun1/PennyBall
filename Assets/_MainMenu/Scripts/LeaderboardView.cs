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

    [Header("Rank Animation")]
    [SerializeField] float _rankAnimDelay    = 0.5f;
    [SerializeField] float _rankAnimDuration = 0.9f;

    ScrollRect    _scrollRect;
    Coroutine     _scrollCoroutine;
    Coroutine     _rankCoroutine;
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
        StopRankAnim();
    }

    public void OnTabSelected()
    {
        StopScroll();
        _scrollCoroutine = StartCoroutine(ScrollToPlayerRoutine());
    }

    void Rebuild()
    {
        if (_botRowPrefab == null || _content == null) return;

        LeagueSaveData save = LeagueService.Instance?.Save;
        if (save?.standings == null) return;

        StopScroll();
        StopRankAnim();

        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        _playerRowRect = null;

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry = save.standings[i];
            LeaderboardRowView prefab = entry.isPlayer && _playerRowPrefab != null
                ? _playerRowPrefab : _botRowPrefab;

            LeaderboardRowView row = Instantiate(prefab, _content);
            row.Bind(rank: i + 1, displayName: entry.displayName,
                     points: entry.points, isPlayer: entry.isPlayer);

            if (entry.isPlayer)
                _playerRowRect = row.GetComponent<RectTransform>();
        }

        // Sıra değişimi var mı — varsa animasyonu başlat
        int rankBefore = MatchSessionContext.RankBefore;
        int rankAfter  = MatchSessionContext.RankAfter;
        if (_playerRowRect != null && rankBefore > 0 && rankBefore != rankAfter)
            _rankCoroutine = StartCoroutine(AnimateRankChange(rankBefore, rankAfter));
    }

    // ── Rank Animasyonu ──────────────────────────────────────────────────────

    IEnumerator AnimateRankChange(int rankBefore, int rankAfter)
    {
        // Layout hesaplasın
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);

        // Oyuncunun yeni sıradaki hedef pozisyonu
        Vector2 targetPos = _playerRowRect.anchoredPosition;
        float   rowH      = _playerRowRect.rect.height;

        // Layout spacing'i bul
        float spacing = 0f;
        VerticalLayoutGroup vlg = _content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) spacing = vlg.spacing;
        float step = rowH + spacing;

        // Eski sıradaki başlangıç Y'si
        // Layout top-anchor: rank 1 → y=0, rank 2 → y = -step, ...
        Vector2 startPos = new Vector2(targetPos.x, -(rankBefore - 1) * step);

        // Placeholder ekle → layout'daki boşluğu koru
        GameObject ph  = new GameObject("_ph", typeof(RectTransform));
        LayoutElement phLE = ph.AddComponent<LayoutElement>();
        phLE.preferredHeight = rowH;
        phLE.flexibleWidth   = 1f;
        ph.transform.SetParent(_content, false);
        ph.transform.SetSiblingIndex(rankAfter - 1);

        // Oyuncu satırını layout dışına al
        LayoutElement le = _playerRowRect.GetComponent<LayoutElement>();
        if (le == null) le = _playerRowRect.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
        _playerRowRect.anchoredPosition = startPos;

        LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);

        yield return new WaitForSeconds(_rankAnimDelay);

        // Yukarı mı aşağı mı
        Debug.Log($"[LeaderboardAnim] {rankBefore}. → {rankAfter}. sıra animasyonu");

        float elapsed = 0f;
        while (elapsed < _rankAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smootherstep(Mathf.Clamp01(elapsed / _rankAnimDuration));
            _playerRowRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        _playerRowRect.anchoredPosition = targetPos;

        // Temizle
        le.ignoreLayout = false;
        Destroy(ph);
        LayoutRebuilder.ForceRebuildLayoutImmediate(_content as RectTransform);
        _rankCoroutine = null;
    }

    static float Smootherstep(float t) =>
        t * t * t * (t * (t * 6f - 15f) + 10f);

    // ── Scroll ───────────────────────────────────────────────────────────────

    void StopScroll()
    {
        if (_scrollCoroutine != null) { StopCoroutine(_scrollCoroutine); _scrollCoroutine = null; }
    }

    void StopRankAnim()
    {
        if (_rankCoroutine != null) { StopCoroutine(_rankCoroutine); _rankCoroutine = null; }
    }

    IEnumerator ScrollToPlayerRoutine()
    {
        if (_scrollRect == null || _playerRowRect == null) yield break;

        RectTransform content  = _content as RectTransform;
        RectTransform viewport = _scrollRect.viewport != null
            ? _scrollRect.viewport
            : (RectTransform)_scrollRect.transform;

        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        float contentH    = content.rect.height;
        float viewportH   = viewport.rect.height;
        float scrollableH = contentH - viewportH;

        if (scrollableH <= 0f) yield break;

        float rowY           = Mathf.Abs(_playerRowRect.anchoredPosition.y);
        rowY                 = Mathf.Max(0f, rowY - viewportH * 0.5f);
        float targetNorm     = 1f - Mathf.Clamp01(rowY / scrollableH);

        _scrollRect.verticalNormalizedPosition = 1f;
        yield return new WaitForSeconds(_scrollDelay);

        float elapsed = 0f;
        while (elapsed < _scrollDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / _scrollDuration);
            _scrollRect.verticalNormalizedPosition = Mathf.Lerp(1f, targetNorm, t);
            yield return null;
        }

        _scrollRect.verticalNormalizedPosition = targetNorm;
        _scrollCoroutine = null;
    }
}
