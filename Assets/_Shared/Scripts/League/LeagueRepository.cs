using UnityEngine;

public static class LeagueRepository
{
    public static LeagueSaveData Load()
    {
        if (!PlayerPrefs.HasKey(LeagueConfig.SaveKey))
        {
            return null;
        }

        string json = PlayerPrefs.GetString(LeagueConfig.SaveKey);
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        return JsonUtility.FromJson<LeagueSaveData>(json);
    }

    public static void Save(LeagueSaveData data)
    {
        if (data == null)
        {
            return;
        }

        PlayerPrefs.SetString(LeagueConfig.SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    public static void Delete()
    {
        PlayerPrefs.DeleteKey(LeagueConfig.SaveKey);
        PlayerPrefs.Save();
    }
}
