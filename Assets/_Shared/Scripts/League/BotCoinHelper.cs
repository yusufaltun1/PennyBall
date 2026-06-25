using UnityEngine;

/// <summary>
/// Bot görüntülenirken gösterilecek coin bakiyesini hesaplar.
/// Değer deterministiktir — aynı bot her zaman aynı miktarı gösterir.
/// </summary>
public static class BotCoinHelper
{
    // Her lig için [min, max] coin aralığı. İndeks = lig numarası (1–10).
    static readonly int[] MinCoins = { 0, 100,   500,  2_000,  5_000, 15_000,  40_000, 100_000,  250_000,  600_000, 1_500_000 };
    static readonly int[] MaxCoins = { 0, 5_000, 15_000, 40_000, 100_000, 250_000, 600_000, 1_500_000, 3_500_000, 8_000_000, 20_000_000 };

    public static int GetCoinCount(BotPlayerEntry bot)
    {
        if (bot == null)
        {
            return 0;
        }

        // Knuth çarpımsal hash — hızlı, deterministik, iyi dağılım
        uint hash = unchecked((uint)(bot.id * 2654435761u + bot.homeLeague * 40503u));
        float t = (hash >> 16) / 65535f; // [0, 1]

        int league = Mathf.Clamp(bot.homeLeague, 1, MinCoins.Length - 1);
        int raw = Mathf.RoundToInt(Mathf.Lerp(MinCoins[league], MaxCoins[league], t));

        // En yakın 100'e yuvarla — daha temiz görünür
        return Mathf.Max(100, (raw / 100) * 100);
    }

    public static string Format(int coins)
    {
        if (coins >= 1_000_000)
        {
            return $"{coins / 1_000_000f:F1}M";
        }

        if (coins >= 10_000)
        {
            return $"{coins / 1_000f:F1}K";
        }

        if (coins >= 1_000)
        {
            return $"{coins / 1_000f:F2}K".TrimEnd('0').TrimEnd('.');
        }

        return coins.ToString();
    }
}
