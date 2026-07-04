using System;
using UnityEditor;
using UnityEngine;

public static class LeagueDebugMenu
{
    [MenuItem("PennyBall/League/Reset League Save")]
    public static void ResetLeagueSave()
    {
        LeagueRepository.Delete();
        Debug.Log("[League] Save silindi. Oyunu yeniden başlat.");
    }

    [MenuItem("PennyBall/League/Log Current Standings")]
    public static void LogStandings()
    {
        LeagueSaveData save = LeagueRepository.Load();
        if (save == null)
        {
            Debug.Log("[League] Kayıt yok.");
            return;
        }

        LeagueStandingsLogger.LogLeagueStandings(save);
    }

    [MenuItem("PennyBall/League/Debug/Set Season End In 10 Seconds")]
    public static void SetSeasonEndIn10Seconds()
    {
        SetSeasonRemaining(TimeSpan.FromSeconds(10));
    }

    [MenuItem("PennyBall/League/Debug/Set Season End In 1 Minute")]
    public static void SetSeasonEndIn1Minute()
    {
        SetSeasonRemaining(TimeSpan.FromMinutes(1));
    }

    [MenuItem("PennyBall/League/Debug/Expire Season Now")]
    public static void ExpireSeasonNow()
    {
        if (!TryGetService(out LeagueService service))
        {
            return;
        }

        service.DebugSetSeasonRemaining(TimeSpan.FromSeconds(-1));
        bool resolved = service.ResolveSeasonIfNeeded();
        Debug.Log(resolved
            ? $"[League] Sezon çözüldü. Pending={service.HasPendingSeasonResult}, League={service.PlayerLeague}"
            : "[League] Sezon zaten aktif (expire olmadı).");

        ShowPendingPanelIfPresent();
    }

    [MenuItem("PennyBall/League/Debug/Force Rank 1 Then Expire Season")]
    public static void ForceRank1ThenExpire()
    {
        if (!TryGetService(out LeagueService service))
        {
            return;
        }

        service.DebugForcePlayerFirstPlace();
        service.DebugSetSeasonRemaining(TimeSpan.FromSeconds(-1));
        service.ResolveSeasonIfNeeded();
        Debug.Log(
            $"[League] Rank1 + expire. Pending={service.HasPendingSeasonResult}, " +
            $"Promoted={(service.TryGetPendingSeasonResult(out LeagueSeasonResult r) && r.Promoted)}, " +
            $"League={service.PlayerLeague}");

        ShowPendingPanelIfPresent();
    }

    [MenuItem("PennyBall/League/Debug/Show Pending LeagueChange Panel")]
    public static void ShowPendingPanel()
    {
        if (!TryGetService(out LeagueService service))
        {
            return;
        }

        service.ResolveSeasonIfNeeded();
        if (!service.HasPendingSeasonResult)
        {
            Debug.LogWarning("[League] Pending sezon sonucu yok. Önce Expire Season Now dene.");
            return;
        }

        ShowPendingPanelIfPresent();
    }

    [MenuItem("PennyBall/League/Debug/Log Pending Season Result")]
    public static void LogPendingResult()
    {
        LeagueSaveData save = Application.isPlaying
            ? LeagueService.Instance?.Save
            : LeagueRepository.Load();

        if (save == null)
        {
            Debug.Log("[League] Kayıt yok.");
            return;
        }

        DateTime seasonStart = new DateTime(save.seasonStartUtcTicks, DateTimeKind.Utc);
        DateTime seasonEnd = seasonStart.AddHours(LeagueConfig.SeasonDurationHours);
        TimeSpan remaining = seasonEnd - DateTime.UtcNow;

        Debug.Log(
            $"[League] League={save.playerLeague}, Remaining={remaining}, " +
            $"Pending={save.hasPendingSeasonResult}, Promoted={save.pendingPromoted}, " +
            $"PrevLeague={save.pendingPreviousLeague}, NewLeague={save.pendingNewLeague}, " +
            $"FinalRank={save.pendingFinalRank}");
    }

    [MenuItem("PennyBall/League/Debug/Clear Pending Season Result")]
    public static void ClearPendingResult()
    {
        if (Application.isPlaying && LeagueService.Instance != null)
        {
            LeagueService.Instance.ConsumePendingSeasonResult();
            Debug.Log("[League] Pending sonuç temizlendi (play mode).");
            return;
        }

        LeagueSaveData save = LeagueRepository.Load();
        if (save == null)
        {
            Debug.Log("[League] Kayıt yok.");
            return;
        }

        save.hasPendingSeasonResult = false;
        save.pendingPromoted = false;
        save.pendingPreviousLeague = 0;
        save.pendingNewLeague = 0;
        save.pendingFinalRank = 0;
        LeagueRepository.Save(save);
        Debug.Log("[League] Pending sonuç temizlendi (save).");
    }

    static void SetSeasonRemaining(TimeSpan remaining)
    {
        if (Application.isPlaying && LeagueService.Instance != null)
        {
            LeagueService.Instance.DebugSetSeasonRemaining(remaining);
            Debug.Log(
                $"[League] Sezon bitişi ayarlandı: remaining={remaining}, " +
                $"nowRemaining={LeagueService.Instance.SeasonRemaining}");
            return;
        }

        LeagueSaveData save = LeagueRepository.Load();
        if (save == null)
        {
            Debug.LogWarning("[League] Kayıt yok. Önce oyunu bir kez çalıştır.");
            return;
        }

        DateTime endUtc = DateTime.UtcNow + remaining;
        save.seasonStartUtcTicks = endUtc.AddHours(-LeagueConfig.SeasonDurationHours).Ticks;
        LeagueRepository.Save(save);
        Debug.Log($"[League] Sezon bitişi ayarlandı (save): {endUtc:u} (remaining {remaining})");
    }

    static bool TryGetService(out LeagueService service)
    {
        service = LeagueService.Instance;
        if (service != null)
        {
            return true;
        }

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[League] Bu komut için Play Mode gerekli (veya save üzerinden ayarlanır).");
            return false;
        }

        Debug.LogError("[League] LeagueService.Instance yok.");
        return false;
    }

    static void ShowPendingPanelIfPresent()
    {
        LeagueChangePresenter panel = UnityEngine.Object.FindFirstObjectByType<LeagueChangePresenter>(FindObjectsInactive.Include);
        if (panel == null)
        {
            Debug.LogWarning("[League] LeagueChange paneli sahnede bulunamadı.");
            return;
        }

        panel.ShowIfPending();
        Debug.Log("[League] LeagueChange paneli gösterildi.");
    }
}
