using UnityEngine;

public static class WalletRepository
{
    const string Key = "wallet_v1";

    public static WalletData Load()
    {
        string json = PlayerPrefs.GetString(Key, null);
        if (string.IsNullOrEmpty(json))
            return new WalletData();
        return JsonUtility.FromJson<WalletData>(json) ?? new WalletData();
    }

    public static void Save(WalletData data)
    {
        PlayerPrefs.SetString(Key, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
