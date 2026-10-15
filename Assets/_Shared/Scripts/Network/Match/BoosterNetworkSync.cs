using UnityEngine;

/// <summary>
/// Booster efektlerini online maçta karşı tarafa da uygular. Örn. dondurma şimdiye kadar
/// sadece aktive eden tarafın kendi ekranında (CoinTeam.Opponent paraları, zaten sürüklenemez)
/// çalışıyordu — hedeflenen oyuncunun kendi ekranında hiçbir etkisi olmuyordu. Artık aktive
/// edildiğinde karşı tarafa bildiriliyor, karşı taraf KENDİ CoinTeam.Player paralarını donduruyor.
/// </summary>
public static class BoosterNetworkSync
{
    static MatchShotNetworkRelay _boundRelay;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        OnlineMatchSession.SessionCleared += Unbind;
    }

    /// <summary>OnlineMatchCoordinator.Update()'ten, diğer *Sync sınıflarıyla aynı şekilde her frame çağrılır.</summary>
    public static void EnsureBound()
    {
        if (ReferenceEquals(_boundRelay, MatchShotNetworkRelay.Instance))
        {
            return;
        }

        if (_boundRelay != null)
        {
            _boundRelay.BoosterFreezeReceived -= OnBoosterFreezeReceived;
        }

        _boundRelay = MatchShotNetworkRelay.Instance;

        if (_boundRelay != null)
        {
            _boundRelay.BoosterFreezeReceived += OnBoosterFreezeReceived;
        }
    }

    static void Unbind()
    {
        if (_boundRelay != null)
        {
            _boundRelay.BoosterFreezeReceived -= OnBoosterFreezeReceived;
            _boundRelay = null;
        }
    }

    /// <summary>IceBoosterController, dondurmayı aktive ettiğinde (online'da) çağırır.</summary>
    public static void SendFreezeOpponent(float durationSeconds)
    {
        if (!OnlineMatchSession.IsOnlineMatch)
        {
            return;
        }

        MatchShotNetworkRelay.Instance?.TrySendBoosterFreeze(durationSeconds);
    }

    static void OnBoosterFreezeReceived(float durationSeconds)
    {
        Debug.Log($"[BoosterSync] Rakip dondurma kullandı — kendi paralarımız donduruluyor ({durationSeconds}s).");
        IceOpponentFreezeUtility.FreezeCoinsForTeam(
            CoinTeam.Player, durationSeconds, new IceCoinFrostVisual.Settings());
    }
}
