using UnityEngine;

public static class HowToPlayProgress
{
    const string SeenKey = "pennyball.how_to_play.seen";

    // Bu ekran eklenmeden önce onboarding'i bitirmiş oyuncular kuralları görmüş sayılır.
    public static bool IsSeen =>
        PlayerPrefs.GetInt(SeenKey, 0) == 1 || OnboardingProgress.IsCompleted;

    public static void MarkSeen()
    {
        PlayerPrefs.SetInt(SeenKey, 1);
        PlayerPrefs.Save();
    }

    public static void ResetAll()
    {
        PlayerPrefs.DeleteKey(SeenKey);
        PlayerPrefs.Save();
    }
}
