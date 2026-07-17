using UnityEditor;
using UnityEngine;

public class WalletDebugEditorWindow : EditorWindow
{
    enum EditSource
    {
        None,
        Coins,
        Level,
        XpInLevel,
        TotalXp,
    }

    int _coins;
    int _totalXp;
    int _currentLevel = 1;
    int _xpInCurrentLevel;
    bool _autoApply = true;

    [MenuItem("PennyBall/Wallet/Open Wallet Debug")]
    public static void Open()
    {
        WalletDebugEditorWindow window = GetWindow<WalletDebugEditorWindow>("Wallet Debug");
        window.RefreshFromSave();
        window.Show();
    }

    [MenuItem("PennyBall/Wallet/Reset Wallet")]
    public static void ResetWallet()
    {
        SaveTotals(0, 0);
        Debug.Log("[Wallet] Cüzdan sıfırlandı.");
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

        WalletData data = WalletService.Data;
        if (data.totalCoins == _coins && data.totalXp == _totalXp)
        {
            return;
        }

        RefreshFromSave();
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Cüzdan Debug", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? "Play modunda değişiklikler anında oyuna yansır."
                : "Edit modunda PlayerPrefs'e kaydedilir; oyunu başlatınca yüklenir.",
            MessageType.Info);

        _autoApply = EditorGUILayout.Toggle("Otomatik uygula", _autoApply);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Değerler", EditorStyles.boldLabel);

        EditSource editSource = EditSource.None;

        EditorGUI.BeginChangeCheck();
        _coins = EditorGUILayout.IntField("Coins", _coins);
        if (EditorGUI.EndChangeCheck())
        {
            editSource = EditSource.Coins;
            _coins = Mathf.Max(0, _coins);
        }

        EditorGUI.BeginChangeCheck();
        _currentLevel = EditorGUILayout.IntSlider(
            "Mevcut Seviye",
            _currentLevel,
            1,
            PlayerLevelProgression.MaxLevel);
        if (EditorGUI.EndChangeCheck())
        {
            editSource = EditSource.Level;
        }

        EditorGUI.BeginChangeCheck();
        _xpInCurrentLevel = EditorGUILayout.IntField("XP (bu seviyede)", _xpInCurrentLevel);
        if (EditorGUI.EndChangeCheck())
        {
            editSource = EditSource.XpInLevel;
        }

        EditorGUI.BeginChangeCheck();
        _totalXp = EditorGUILayout.IntField("Total XP", _totalXp);
        if (EditorGUI.EndChangeCheck())
        {
            editSource = EditSource.TotalXp;
            _totalXp = Mathf.Max(0, _totalXp);
        }

        if (editSource is EditSource.Level or EditSource.XpInLevel or EditSource.TotalXp)
        {
            SyncFromEdit(editSource);
        }

        DrawCurrentStats();

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Hızlı İşlemler", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Apply"))
        {
            ApplyTotals();
        }

        if (GUILayout.Button("Refresh"))
        {
            RefreshFromSave();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Coins = 0"))
        {
            SetCoins(0);
        }

        if (GUILayout.Button("Coins = 200"))
        {
            SetCoins(200);
        }

        if (GUILayout.Button("Coins = 1000"))
        {
            SetCoins(1000);
        }
        EditorGUILayout.EndHorizontal();

        if (editSource != EditSource.None && _autoApply)
        {
            ApplyTotals();
        }
    }

    void SyncFromEdit(EditSource source)
    {
        switch (source)
        {
            case EditSource.TotalXp:
                _currentLevel = PlayerLevelProgression.GetLevelFromTotalXp(_totalXp);
                _xpInCurrentLevel = PlayerLevelProgression.GetXpInCurrentLevel(_totalXp);
                break;

            case EditSource.Level:
            case EditSource.XpInLevel:
                if (_currentLevel >= PlayerLevelProgression.MaxLevel)
                {
                    _xpInCurrentLevel = 0;
                }
                else
                {
                    int maxXpInLevel = PlayerLevelProgression.GetXpRequiredForNextLevel(_currentLevel);
                    _xpInCurrentLevel = Mathf.Clamp(_xpInCurrentLevel, 0, Mathf.Max(0, maxXpInLevel - 1));
                }

                _totalXp = PlayerLevelProgression.GetTotalXpForLevel(_currentLevel) + _xpInCurrentLevel;
                break;
        }
    }

    void DrawCurrentStats()
    {
        int level = PlayerLevelProgression.GetLevelFromTotalXp(_totalXp);
        int xpInLevel = PlayerLevelProgression.GetXpInCurrentLevel(_totalXp);
        int xpToNext = PlayerLevelProgression.GetXpRequiredForNextLevel(level);
        float progress = PlayerLevelProgression.GetProgressInCurrentLevel(_totalXp);

        EditorGUILayout.Space(8f);
        EditorGUILayout.LabelField("Özet", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Hesaplanan Level", level.ToString());
        EditorGUILayout.LabelField("XP (mevcut seviye)", $"{xpInLevel} / {xpToNext}");
        EditorGUILayout.LabelField("İlerleme", $"{progress * 100f:0.#}%");
    }

    void RefreshFromSave()
    {
        WalletData data = Application.isPlaying ? WalletService.Data : WalletRepository.Load();
        _coins = data.totalCoins;
        _totalXp = data.totalXp;
        _currentLevel = PlayerLevelProgression.GetLevelFromTotalXp(_totalXp);
        _xpInCurrentLevel = PlayerLevelProgression.GetXpInCurrentLevel(_totalXp);
        Repaint();
    }

    void SetCoins(int coins)
    {
        _coins = Mathf.Max(0, coins);
        ApplyTotals();
    }

    void ApplyTotals()
    {
        _coins = Mathf.Max(0, _coins);
        _totalXp = Mathf.Max(0, _totalXp);
        _currentLevel = PlayerLevelProgression.GetLevelFromTotalXp(_totalXp);
        _xpInCurrentLevel = PlayerLevelProgression.GetXpInCurrentLevel(_totalXp);

        SaveTotals(_coins, _totalXp);

        Debug.Log($"[Wallet] Güncellendi → Coins={_coins}, TotalXp={_totalXp}, Level={_currentLevel}");
        Repaint();
    }

    static void SaveTotals(int coins, int totalXp)
    {
        if (Application.isPlaying)
        {
            WalletService.SetTotals(coins, totalXp);
            return;
        }

        WalletRepository.Save(new WalletData
        {
            totalCoins = Mathf.Max(0, coins),
            totalXp = Mathf.Max(0, totalXp),
        });

        // Edit modunda kaydettikten sonra bellek cache'ini temizle;
        // aksi halde Play'de eski level (ör. 36) geri gelebilir.
        WalletService.InvalidateCache();
    }
}
