using UnityEngine;

public static class WalletRepository
{
    const string Key = "wallet_v1";

    // Kullanıcının ilk girişinde (daha önce hiç kayıt yoksa) verilen başlangıç coin'i.
    const int StartingCoins = 500;

    public static WalletData Load()
    {
        string json = PlayerPrefs.GetString(Key, null);
        if (string.IsNullOrEmpty(json))
            return CreateDefaultWallet();
        return JsonUtility.FromJson<WalletData>(json) ?? CreateDefaultWallet();
    }

    static WalletData CreateDefaultWallet()
    {
        return new WalletData { totalCoins = StartingCoins };
    }

    public static void Save(WalletData data)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
