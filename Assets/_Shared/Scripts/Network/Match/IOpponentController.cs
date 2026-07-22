using System;

/// <summary>
/// Bot ve remote rakip için ortak yüzey.
/// </summary>
public interface IOpponentController
{
    string DisplayName { get; }
    int AvatarIndex { get; }
    bool IsHuman { get; }
    void OnMatchStarted();
    void OnRoundReset();
    void OnMatchEnded();
}
