using System;
using UnityEngine;

/// <summary>
/// Aktif maç oturumunu takip eder; abandon recovery ve çift kayıt koruması sağlar.
/// </summary>
public static class MatchSessionTracker
{
    const string ActiveMatchIdKey = "pennyball.match.active_id";

    static string _currentMatchId;
    static bool _resultPending;
    static string _pendingAbandonReason;
    static float _matchStartRealtime;

    public static string CurrentMatchId => _currentMatchId;
    public static bool HasPendingResult => _resultPending;
    public static float ElapsedSeconds => _resultPending ? Mathf.Max(0f, Time.realtimeSinceStartup - _matchStartRealtime) : 0f;

    public static void BeginMatch()
    {
        _currentMatchId = Guid.NewGuid().ToString("N");
        _resultPending = true;
        _pendingAbandonReason = null;
        _matchStartRealtime = Time.realtimeSinceStartup;

        PlayerPrefs.SetString(ActiveMatchIdKey, _currentMatchId);
        PlayerPrefs.Save();
    }

    public static void MarkAbandon(string reason)
    {
        if (!_resultPending)
        {
            return;
        }

        _pendingAbandonReason = string.IsNullOrEmpty(reason) ? "unknown" : reason;
    }

    public static bool TryConsumeResult(out string matchId, out string abandonReason, out int elapsedSeconds)
    {
        matchId = _currentMatchId;
        abandonReason = _pendingAbandonReason;
        elapsedSeconds = Mathf.RoundToInt(ElapsedSeconds);

        if (!_resultPending)
        {
            return false;
        }

        _resultPending = false;
        _pendingAbandonReason = null;
        _currentMatchId = null;

        PlayerPrefs.DeleteKey(ActiveMatchIdKey);
        PlayerPrefs.Save();
        return true;
    }

    public static bool HasPersistedActiveMatch()
    {
        return PlayerPrefs.HasKey(ActiveMatchIdKey);
    }

    public static string GetPersistedMatchId()
    {
        return PlayerPrefs.GetString(ActiveMatchIdKey, null);
    }

    public static void ClearPersistedActiveMatch()
    {
        PlayerPrefs.DeleteKey(ActiveMatchIdKey);
        PlayerPrefs.Save();
        _resultPending = false;
        _currentMatchId = null;
        _pendingAbandonReason = null;
    }

    public static bool TryRestorePersistedMatch(string abandonReason, out string matchId)
    {
        matchId = GetPersistedMatchId();
        if (string.IsNullOrEmpty(matchId))
        {
            return false;
        }

        _currentMatchId = matchId;
        _resultPending = true;
        _pendingAbandonReason = abandonReason;
        _matchStartRealtime = Time.realtimeSinceStartup;

        PlayerPrefs.DeleteKey(ActiveMatchIdKey);
        PlayerPrefs.Save();
        return true;
    }
}
