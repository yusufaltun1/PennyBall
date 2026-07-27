using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OnlineMultiplayerDebugMenu
{
    [MenuItem("PennyBall/Online/Toggle Matchmaking Flag")]
    static void ToggleMatchmaking()
    {
        OnlineFeatureFlags.OnlineMatchmakingEnabled = !OnlineFeatureFlags.OnlineMatchmakingEnabled;
        Debug.Log($"[Online] MatchmakingEnabled={OnlineFeatureFlags.OnlineMatchmakingEnabled}");
    }

    [MenuItem("PennyBall/Online/Toggle Matchmaking Flag", true)]
    static bool ToggleMatchmakingValidate()
    {
        Menu.SetChecked("PennyBall/Online/Toggle Matchmaking Flag", OnlineFeatureFlags.OnlineMatchmakingEnabled);
        return true;
    }

    [MenuItem("PennyBall/Online/Enable Matchmaking (Play test)")]
    static void EnableMatchmakingForPlay()
    {
        OnlineFeatureFlags.OnlineMatchmakingEnabled = true;
        OnlineFeatureFlags.OnlineOnlyMatches = false;
        Debug.Log(
            "[Online] Matchmaking ON. Play → Photon quick-match (Nakama varsa onu dener). " +
            "ParrelSync: iki client aynı ~2 dk içinde Play'e bassın.");
    }

    [MenuItem("PennyBall/Online/Toggle Online-Only (no bot fallback)")]
    static void ToggleOnlineOnly()
    {
        OnlineFeatureFlags.OnlineOnlyMatches = !OnlineFeatureFlags.OnlineOnlyMatches;
        Debug.Log($"[Online] OnlineOnlyMatches={OnlineFeatureFlags.OnlineOnlyMatches}");
    }

    [MenuItem("PennyBall/Online/Toggle Online Wallet")]
    static void ToggleWallet()
    {
        OnlineWalletService.UseOnlineWallet = !OnlineWalletService.UseOnlineWallet;
        Debug.Log($"[Online] UseOnlineWallet={OnlineWalletService.UseOnlineWallet}");
    }

    [MenuItem("PennyBall/Online/Toggle Online Wallet", true)]
    static bool ToggleWalletValidate()
    {
        Menu.SetChecked("PennyBall/Online/Toggle Online Wallet", OnlineWalletService.UseOnlineWallet);
        return true;
    }

    [MenuItem("PennyBall/Online/Toggle Online League")]
    static void ToggleLeague()
    {
        OnlineLeagueService.UseOnlineLeague = !OnlineLeagueService.UseOnlineLeague;
        Debug.Log($"[Online] UseOnlineLeague={OnlineLeagueService.UseOnlineLeague}");
    }

    [MenuItem("PennyBall/Online/Toggle Online League", true)]
    static bool ToggleLeagueValidate()
    {
        Menu.SetChecked("PennyBall/Online/Toggle Online League", OnlineLeagueService.UseOnlineLeague);
        return true;
    }

    [MenuItem("PennyBall/Online/Pull Online Wallet Now")]
    static void PullWalletNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Online] Play Mode gerekli.");
            return;
        }

        _ = PullWalletAsync();
    }

    static async Task PullWalletAsync()
    {
        bool ok = await OnlineWalletService.PullAndApplyAsync();
        Debug.Log(ok
            ? $"[Online] Wallet pulled: {WalletService.TotalCoins}c / {WalletService.TotalXp}xp"
            : "[Online] Wallet pull failed (flag/auth/Nakama).");
    }

    [MenuItem("PennyBall/Online/Authenticate Nakama Now")]
    static void AuthenticateNow()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Online] Play Mode gerekli.");
            return;
        }

        NetworkBootstrap.Instance?.BootAsync();
    }

    const string FixedTestRoom = "pb_debug_room";

    [MenuItem("PennyBall/Online/Create Photon Test Room And Load Game")]
    static void CreateTestRoom()
    {
        MatchShotRelayPrefabSetup.EnsurePrefabExists(log: true);

        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Online] Play Mode gerekli.");
            return;
        }

        string roomName = FixedTestRoom;
        BeginPendingTestRoom(roomName, "Waiting...", 1);
    }

    [MenuItem("PennyBall/Online/Join Fixed Debug Room (pb_debug_room)")]
    static void JoinFixedDebugRoom()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Online] Play Mode gerekli.");
            return;
        }

        BeginPendingTestRoom(FixedTestRoom, "Host Player", 2);
    }

    [MenuItem("PennyBall/Online/Create/Join Fixed Debug Room (pb_debug_room)")]
    static void CreateOrJoinFixedDebugRoom()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Online] Play Mode gerekli.");
            return;
        }

        BeginPendingTestRoom(FixedTestRoom, "Opponent", 1);
    }

    [MenuItem("PennyBall/Online/Join Photon Test Room From Clipboard")]
    static void JoinTestRoomFromClipboard()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[Online] Play Mode gerekli.");
            return;
        }

        string room = EditorGUIUtility.systemCopyBuffer;
        if (string.IsNullOrWhiteSpace(room))
        {
            Debug.LogWarning(
                "[Online] Clipboard boş. ParrelSync clone clipboard paylaşmaz — " +
                "PennyBall/Online/Create/Join Fixed Debug Room kullan.");
            return;
        }

        BeginPendingTestRoom(room.Trim(), "Host Player", 2);
    }

    static void BeginPendingTestRoom(string roomName, string opponentName, int avatarIndex)
    {
        PendingPhotonSession.SetForTestRoom(roomName, opponentName, avatarIndex);
        MatchSessionContext.SetOpponent(new BotPlayerEntry
        {
            id = avatarIndex,
            displayName = opponentName,
            avatarIndex = avatarIndex,
            difficultyLevel = 12,
            homeLeague = 1,
            countryCode = "XX"
        });
        MatchSessionContext.SetOnlineMatch(true, "pending", roomName);

        EditorGUIUtility.systemCopyBuffer = roomName;
        Debug.Log($"[Online] Pending Photon room '{roomName}'. Loading Game — join after scene load.");
        SceneManager.LoadScene(GameSceneNames.Game);
    }

    [MenuItem("PennyBall/Online/Create NetworkConfig Asset")]
    static void CreateNetworkConfigAsset()
    {
        const string dir = "Assets/_Shared/Resources";
        if (!AssetDatabase.IsValidFolder("Assets/_Shared/Resources"))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Shared"))
            {
                AssetDatabase.CreateFolder("Assets", "_Shared");
            }

            AssetDatabase.CreateFolder("Assets/_Shared", "Resources");
        }

        string path = dir + "/NetworkConfig.asset";
        NetworkConfig existing = AssetDatabase.LoadAssetAtPath<NetworkConfig>(path);
        if (existing != null)
        {
            Selection.activeObject = existing;
            Debug.Log("[Online] NetworkConfig zaten var: " + path);
            return;
        }

        NetworkConfig config = ScriptableObject.CreateInstance<NetworkConfig>();
        config.photonFusionAppId = "038975f7-44f7-4eda-a0bc-08a9d3cc124a";
        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = config;
        Debug.Log("[Online] NetworkConfig oluşturuldu: " + path);
    }
}
