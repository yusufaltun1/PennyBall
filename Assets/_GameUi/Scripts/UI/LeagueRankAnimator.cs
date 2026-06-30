using System.Collections;
using UnityEngine;

/// <summary>
/// Maç sonrası lig sıralamasını gösterir ve oyuncunun sırasını
/// eski konumundan yeni konumuna animasyonlu olarak taşır.
/// Content objesine VerticalLayoutGroup ekleme — bu script konumlandırır.
/// </summary>
public class LeagueRankAnimator : MonoBehaviour
{
    [Header("Row Prefabs")]
    [SerializeField] LeaderboardRowView _botRowPrefab;
    [SerializeField] LeaderboardRowView _playerRowPrefab;

    [Header("Layout")]
    [SerializeField] RectTransform _content;
    [SerializeField] float _rowHeight  = 80f;
    [SerializeField] float _rowSpacing = 8f;

    [Header("Animation")]
    [SerializeField] float _animDelay    = 0.4f;
    [SerializeField] float _animDuration = 0.8f;

    Coroutine _anim;

    void OnEnable()
    {
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(BuildAndAnimate());
    }

    void OnDisable()
    {
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
    }

    IEnumerator BuildAndAnimate()
    {
        if (_content == null || _botRowPrefab == null) yield break;

        for (int i = _content.childCount - 1; i >= 0; i--)
            Destroy(_content.GetChild(i).gameObject);

        yield return null;

        LeagueSaveData save = LeagueService.Instance?.Save;
        if (save?.standings == null) yield break;

        int   rankBefore = MatchSessionContext.RankBefore;
        int   rankAfter  = MatchSessionContext.RankAfter;
        float step       = _rowHeight + _rowSpacing;

        RectTransform playerRect = null;

        for (int i = 0; i < save.standings.Length; i++)
        {
            LeagueStandingEntry entry  = save.standings[i];
            LeaderboardRowView  prefab = entry.isPlayer && _playerRowPrefab != null
                ? _playerRowPrefab : _botRowPrefab;

            LeaderboardRowView row     = Instantiate(prefab, _content);
            RectTransform      rowRect = row.GetComponent<RectTransform>();

            rowRect.anchorMin        = new Vector2(0f, 1f);
            rowRect.anchorMax        = new Vector2(1f, 1f);
            rowRect.pivot            = new Vector2(0.5f, 1f);
            rowRect.sizeDelta        = new Vector2(0f, _rowHeight);
            rowRect.anchoredPosition = new Vector2(0f, -i * step);

            row.Bind(rank: i + 1, displayName: entry.displayName,
                     points: entry.points, isPlayer: entry.isPlayer);

            if (entry.isPlayer) playerRect = rowRect;
        }

        _content.sizeDelta = new Vector2(_content.sizeDelta.x, save.standings.Length * step);

        if (playerRect == null || rankBefore <= 0 || rankBefore == rankAfter) yield break;

        Vector2 targetPos = playerRect.anchoredPosition;
        Vector2 startPos  = new Vector2(0f, -(rankBefore - 1) * step);
        playerRect.anchoredPosition = startPos;

        yield return new WaitForSeconds(_animDelay);

        Debug.Log($"[RankAnim] {rankBefore}. → {rankAfter}. sıra animasyonu");

        float elapsed = 0f;
        while (elapsed < _animDuration)
        {
            elapsed += Time.deltaTime;
            float t = Smootherstep(Mathf.Clamp01(elapsed / _animDuration));
            playerRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }

        playerRect.anchoredPosition = targetPos;
        _anim = null;
    }

    static float Smootherstep(float t) =>
        t * t * t * (t * (t * 6f - 15f) + 10f);
}
