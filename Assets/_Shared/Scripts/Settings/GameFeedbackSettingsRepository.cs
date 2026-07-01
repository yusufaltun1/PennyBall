using UnityEngine;

public static class GameFeedbackSettingsRepository
{
    const string SaveKey = "game_feedback_settings_v1";

    public static GameFeedbackSettingsData Load()
    {
        if (!PlayerPrefs.HasKey(SaveKey))
        {
            return new GameFeedbackSettingsData();
        }

        string json = PlayerPrefs.GetString(SaveKey);
        if (string.IsNullOrEmpty(json))
        {
            return new GameFeedbackSettingsData();
        }

        return JsonUtility.FromJson<GameFeedbackSettingsData>(json) ?? new GameFeedbackSettingsData();
    }

    public static void Save(GameFeedbackSettingsData data)
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
