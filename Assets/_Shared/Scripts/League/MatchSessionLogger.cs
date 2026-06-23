using UnityEngine;

public static class MatchSessionLogger
{
    public static void LogMatchedOpponent(BotPlayerEntry opponent, int playerLeague)
    {
        if (opponent == null)
        {
            Debug.LogWarning("[Match] Eşleşme loglanamadı: rakip yok.");
            return;
        }

        Debug.Log(
            $"[Match] Eşleşme bulundu | Lig={playerLeague} | Rakip={opponent.displayName} | " +
            $"BotId={opponent.id} | Ülke={opponent.countryCode} | Zorluk={opponent.difficultyLevel} | Avatar={opponent.avatarIndex}");
    }
}
