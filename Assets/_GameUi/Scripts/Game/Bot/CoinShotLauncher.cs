using UnityEngine;

/// <summary>
/// Bot atışı — oyuncu ile aynı CoinDragController limitlerini kullanır.
/// </summary>
public static class CoinShotLauncher
{
    public static bool TryLaunch(CoinDragController coin, Vector3 direction, float pullDistance)
    {
        return coin != null && coin.LaunchInDirection(direction, pullDistance);
    }
}
