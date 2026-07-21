using System;
using UnityEditor;
using UnityEngine;

public class SeasonDebugEditorWindow : EditorWindow
{
    const float DefaultSeasonHours = LeagueConfig.SeasonDurationHours;

    float _remainingHours = DefaultSeasonHours;
    float _remainingMinutes;
    int _playerLeaguePoints;
    bool _autoApply;

    [MenuItem("PennyBall/League/Open Season Debug")]
    public static void Open()
    {
        SeasonDebugEditorWindow window = GetWindow<SeasonDebugEditorWindow>("Season Debug");
        window.RefreshFromSave();
        window.Show();
    }

    [MenuItem("PennyBall/League/Open League Points Debug")]
    public static void OpenLeaguePoints()
    {
        Open();
    }

    void OnEnable()
    {
        RefreshFromSave();
        EditorApplication.update += OnEditorUpdate;
    }

    void OnDisable()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    void OnEditorUpdate()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Repaint();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Sezon Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Play modunda kalan süreyi değiştirip 'Sezon Kontrol Et' ile popup akışını test edebilirsin."
                : "Edit modunda kayda yazar; Play'e basınca yüklenir. Popup testi için Play modu önerilir.",
            MessageType.Info);

        DrawCurrentState();
        EditorGUILayout.Space(8f);
        DrawLeaguePointsSection();
        EditorGUILayout.Space(8f);

        _autoApply = EditorGUILayout.Toggle("Otomatik uygula", _autoApply);

        EditorGUILayout.LabelField("Kalan Süre Ayarla", EditorStyles.boldLabel);
        _remainingHours = EditorGUILayout.FloatField("Saat", _remainingHours);
        _remainingMinutes = EditorGUILayout.FloatField("Dakika", _remainingMinutes);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply"))
        {
            ApplyRemainingTime();
        }

        if (GUILayout.Button("Refresh"))
        {
            RefreshFromSave();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Hızlı Presetler", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("72s (full)"))
        {
            SetPresetRemaining(DefaultSeasonHours, 0f);
        }

        if (GUILayout.Button("1 saat"))
        {
            SetPresetRemaining(1f, 0f);
        }

        if (GUILayout.Button("5 dk"))
        {
            SetPresetRemaining(0f, 5f);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("1 dk"))
        {
            SetPresetRemaining(0f, 1f);
        }

        if (GUILayout.Button("Süre doldu (0)"))
        {
            SetPresetRemaining(0f, 0f);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Sezon Akışı", EditorStyles.boldLabel);

        if (GUILayout.Button("Sezon Kontrol Et (EnsureSeasonActive)"))
        {
            ForceSeasonCheck();
        }

        if (GUILayout.Button("Sezonu Bitir + Kontrol Et"))
        {
            ExpireAndCheck();
        }

        if (GUILayout.Button("1. Sıraya Al"))
        {
            ForceRankOne();
        }

        if (GUILayout.Button("1. Sıra + Sezon Bitir (Promote Test)"))
        {
            ForceRankOneAndExpire();
        }

        if (Application.isPlaying && GUILayout.Button("Bekleyen Popup'ı Göster"))
        {
            ShowPendingPopup();
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.HelpBox(
            "Popup testi:\n" +
            "1) Play DURDUR → PennyBall/League/Setup LeagueChange Panel On MainMenu çalıştır\n" +
            "2) Play → 1. sıra değilsen '1. Sıra + Sezon Bitir' kullan\n" +
            "3) Süreyi 0 yapınca Main Menu'de beklemen yeterli (1 sn içinde kontrol edilir)\n" +
            "4) Doğru panel: LeagueChange (Continue / Ad x2 butonları)",
            MessageType.None);
    }

    void DrawCurrentState()
    {
        EditorGUILayout.LabelField("Durum", EditorStyles.boldLabel);

        if (Application.isPlaying && LeagueService.Instance != null)
        {
            TimeSpan seasonRemaining = LeagueService.Instance.SeasonRemaining;
            int league = LeagueService.Instance.PlayerLeague;
            int rank = LeagueService.Instance.GetPlayerRank();

            EditorGUILayout.LabelField("Kalan", FormatRemaining(seasonRemaining));
            EditorGUILayout.LabelField("Lig", $"{league} — {LeagueConfig.GetLeagueName(league)}");
            EditorGUILayout.LabelField("Sıra", $"#{rank}");
            EditorGUILayout.LabelField("Lig puanı", GetPlayerLeaguePoints().ToString());
            EditorGUILayout.LabelField("Sezon süresi", $"{DefaultSeasonHours} saat");
            EditorGUILayout.LabelField(
                "Bekleyen sezon sonucu",
                LeagueService.Instance.HasPendingSeasonResult ? "Var" : "Yok");
            return;
        }

        LeagueSaveData save = LeagueRepository.Load();
        if (save == null)
        {
            EditorGUILayout.LabelField("Kayıt", "Yok — oyunu bir kez başlat");
            return;
        }

        DateTime seasonStart = new DateTime(save.seasonStartUtcTicks, DateTimeKind.Utc);
        DateTime seasonEnd = seasonStart.AddHours(DefaultSeasonHours);
        TimeSpan savedRemaining = seasonEnd - DateTime.UtcNow;

        EditorGUILayout.LabelField("Kalan", FormatRemaining(savedRemaining));
        EditorGUILayout.LabelField("Lig", $"{save.playerLeague} — {LeagueConfig.GetLeagueName(save.playerLeague)}");
        EditorGUILayout.LabelField("Lig puanı", GetPlayerLeaguePoints(save).ToString());
        EditorGUILayout.LabelField("Sezon başlangıç (UTC)", seasonStart.ToString("yyyy-MM-dd HH:mm"));
        EditorGUILayout.LabelField("Sezon bitiş (UTC)", seasonEnd.ToString("yyyy-MM-dd HH:mm"));
    }

    void DrawLeaguePointsSection()
    {
        EditorGUILayout.LabelField("Lig Puanı", EditorStyles.boldLabel);
        _playerLeaguePoints = EditorGUILayout.IntField("Puan", _playerLeaguePoints);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Puanı Uygula"))
        {
            ApplyPlayerLeaguePoints();
        }

        if (GUILayout.Button("Puanı Yenile"))
        {
            RefreshPlayerLeaguePoints();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("0"))
        {
            SetPlayerLeaguePointsPreset(0);
        }

        if (GUILayout.Button("15"))
        {
            SetPlayerLeaguePointsPreset(15);
        }

        if (GUILayout.Button("30"))
        {
            SetPlayerLeaguePointsPreset(30);
        }

        if (GUILayout.Button("1. Sıra"))
        {
            ForceRankOnePoints();
        }
        EditorGUILayout.EndHorizontal();
    }

    void RefreshPlayerLeaguePoints()
    {
        _playerLeaguePoints = GetPlayerLeaguePoints();
        Repaint();
    }

    void SetPlayerLeaguePointsPreset(int points)
    {
        _playerLeaguePoints = points;
        ApplyPlayerLeaguePoints();
    }

    void ForceRankOnePoints()
    {
        if (Application.isPlaying && LeagueService.Instance != null)
        {
            LeagueService.Instance.DebugForcePlayerFirstPlace();
            RefreshPlayerLeaguePoints();
            Debug.Log("[SeasonDebug] Oyuncu 1. sıraya alındı (puan güncellendi).");
            Repaint();
            return;
        }

        LeagueSaveData save = LeagueRepository.Load();
        if (save?.standings == null)
        {
            Debug.LogWarning("[SeasonDebug] Lig kaydı yok.");
            return;
        }

        int maxPoints = 0;
        for (int i = 0; i < save.standings.Length; i++)
        {
            maxPoints = Mathf.Max(maxPoints, save.standings[i].points);
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                save.standings[i].points = maxPoints + 10;
                break;
            }
        }

        SortStandingsInSave(save);
        LeagueRepository.Save(save);
        RefreshPlayerLeaguePoints();
        Debug.Log("[SeasonDebug] Kayda yazıldı → oyuncu 1. sıra puanı.");
        Repaint();
    }

    void ApplyPlayerLeaguePoints()
    {
        _playerLeaguePoints = Mathf.Max(0, _playerLeaguePoints);

        if (Application.isPlaying && LeagueService.Instance != null)
        {
            LeagueService.Instance.DebugSetPlayerLeaguePoints(_playerLeaguePoints);
            Debug.Log($"[SeasonDebug] Lig puanı ayarlandı → {_playerLeaguePoints}");
            Repaint();
            return;
        }

        LeagueSaveData save = LeagueRepository.Load();
        if (save?.standings == null)
        {
            Debug.LogWarning("[SeasonDebug] Lig kaydı yok. Önce oyunu bir kez başlat.");
            return;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                save.standings[i].points = _playerLeaguePoints;
                break;
            }
        }

        SortStandingsInSave(save);
        LeagueRepository.Save(save);
        Debug.Log($"[SeasonDebug] Kayda yazıldı → lig puanı {_playerLeaguePoints}");
        Repaint();
    }

    static int GetPlayerLeaguePoints(LeagueSaveData save = null)
    {
        if (Application.isPlaying && LeagueService.Instance != null)
        {
            LeagueStandingEntry player = FindPlayerInSave(LeagueService.Instance.Save);
            return player?.points ?? 0;
        }

        save ??= LeagueRepository.Load();
        LeagueStandingEntry savedPlayer = FindPlayerInSave(save);
        return savedPlayer?.points ?? 0;
    }

    static LeagueStandingEntry FindPlayerInSave(LeagueSaveData save)
    {
        if (save?.standings == null)
        {
            return null;
        }

        for (int i = 0; i < save.standings.Length; i++)
        {
            if (save.standings[i].isPlayer)
            {
                return save.standings[i];
            }
        }

        return null;
    }

    static void SortStandingsInSave(LeagueSaveData save)
    {
        if (save?.standings == null)
        {
            return;
        }

        Array.Sort(save.standings, (a, b) =>
        {
            int pointsDelta = b.points - a.points;
            if (pointsDelta != 0)
            {
                return pointsDelta;
            }

            if (a.isPlayer)
            {
                return -1;
            }

            if (b.isPlayer)
            {
                return 1;
            }

            return string.Compare(a.displayName, b.displayName, StringComparison.Ordinal);
        });
    }

    void RefreshFromSave()
    {
        TimeSpan remaining;

        if (Application.isPlaying && LeagueService.Instance != null)
        {
            remaining = LeagueService.Instance.SeasonRemaining;
        }
        else
        {
            LeagueSaveData save = LeagueRepository.Load();
            if (save == null)
            {
                _remainingHours = DefaultSeasonHours;
                _remainingMinutes = 0f;
                _playerLeaguePoints = 0;
                Repaint();
                return;
            }

            DateTime seasonStart = new DateTime(save.seasonStartUtcTicks, DateTimeKind.Utc);
            DateTime seasonEnd = seasonStart.AddHours(DefaultSeasonHours);
            remaining = seasonEnd - DateTime.UtcNow;
        }

        if (remaining < TimeSpan.Zero)
        {
            remaining = TimeSpan.Zero;
        }

        _remainingHours = (float)remaining.TotalHours;
        _remainingMinutes = remaining.Minutes + remaining.Seconds / 60f;
        _playerLeaguePoints = GetPlayerLeaguePoints();
        Repaint();
    }

    void SetPresetRemaining(float hours, float minutes)
    {
        _remainingHours = hours;
        _remainingMinutes = minutes;
        ApplyRemainingTime();
    }

    void ApplyRemainingTime()
    {
        _remainingHours = Mathf.Max(0f, _remainingHours);
        _remainingMinutes = Mathf.Max(0f, _remainingMinutes);
        TimeSpan remaining = TimeSpan.FromHours(_remainingHours) + TimeSpan.FromMinutes(_remainingMinutes);

        if (Application.isPlaying && LeagueService.Instance != null)
        {
            LeagueService.Instance.DebugSetSeasonRemaining(remaining);
            Debug.Log($"[SeasonDebug] Kalan süre ayarlandı → {FormatRemaining(remaining)}");
            Repaint();
            return;
        }

        LeagueSaveData save = LeagueRepository.Load();
        if (save == null)
        {
            Debug.LogWarning("[SeasonDebug] Lig kaydı yok. Önce oyunu bir kez başlat.");
            return;
        }

        DateTime seasonEnd = DateTime.UtcNow.Add(remaining);
        DateTime seasonStart = seasonEnd.AddHours(-DefaultSeasonHours);
        save.seasonStartUtcTicks = seasonStart.Ticks;
        LeagueRepository.Save(save);

        Debug.Log($"[SeasonDebug] Kayda yazıldı → kalan {FormatRemaining(remaining)}");
        Repaint();
    }

    void ForceSeasonCheck()
    {
        if (!Application.isPlaying || LeagueService.Instance == null)
        {
            EditorUtility.DisplayDialog(
                "Season Debug",
                "Sezon kontrolü için Play modunda olman gerekiyor.",
                "Tamam");
            return;
        }

        LeagueService.Instance.DebugForceSeasonCheck();
        Debug.Log("[SeasonDebug] EnsureSeasonActive tetiklendi.");
        RefreshFromSave();
    }

    void ExpireAndCheck()
    {
        if (!Application.isPlaying || LeagueService.Instance == null)
        {
            _remainingHours = 0f;
            _remainingMinutes = 0f;
            ApplyRemainingTime();
            EditorUtility.DisplayDialog(
                "Season Debug",
                "Süre kayda 0 olarak yazıldı. Play'e basınca sezon kontrolü çalışır.",
                "Tamam");
            return;
        }

        LeagueService.Instance.DebugExpireSeasonNow();
        Debug.Log("[SeasonDebug] Sezon bitirildi ve kontrol edildi.");
        RefreshFromSave();
    }

    void ForceRankOne()
    {
        if (!Application.isPlaying || LeagueService.Instance == null)
        {
            EditorUtility.DisplayDialog("Season Debug", "Play modunda olman gerekiyor.", "Tamam");
            return;
        }

        LeagueService.Instance.DebugForcePlayerFirstPlace();
        Debug.Log("[SeasonDebug] Oyuncu 1. sıraya alındı.");
        RefreshFromSave();
    }

    void ForceRankOneAndExpire()
    {
        if (!Application.isPlaying || LeagueService.Instance == null)
        {
            EditorUtility.DisplayDialog("Season Debug", "Play modunda olman gerekiyor.", "Tamam");
            return;
        }

        LeagueService.Instance.DebugForceRankOneAndExpireSeason();
        Debug.Log("[SeasonDebug] 1. sıra + sezon bitişi tetiklendi.");
        RefreshFromSave();
    }

    void ShowPendingPopup()
    {
        LeagueSeasonResultController controller = UnityEngine.Object.FindFirstObjectByType<LeagueSeasonResultController>();
        if (controller == null)
        {
            Debug.LogWarning("[SeasonDebug] LeagueSeasonResultController bulunamadı. Setup menüsünü çalıştır.");
            return;
        }

        controller.TryShowPendingResult();
    }

    static string FormatRemaining(TimeSpan remaining)
    {
        if (remaining <= TimeSpan.Zero)
        {
            return "0d 0h 0m (doldu)";
        }

        return $"{remaining.Days}d {remaining.Hours}h {remaining.Minutes}m {remaining.Seconds}s";
    }
}
