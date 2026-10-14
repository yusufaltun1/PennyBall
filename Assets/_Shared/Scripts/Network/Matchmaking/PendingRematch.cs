/// <summary>
/// Arkadaşla oynanan maç bitip ResultPanel > RePlay'de "Play Again"e basılınca, Game sahnesi
/// kapanmadan önceki maçın Photon oda adı + rakip bilgisi burada saklanır. MainMenu yüklenince
/// UIManager bunu tüketip aynı odaya (yeni kod üretmeden/yazmadan) otomatik tekrar katılır.
/// </summary>
public static class PendingRematch
{
    public static string RoomName { get; private set; }
    public static string OpponentUserId { get; private set; }
    public static string OpponentDisplayName { get; private set; }
    public static int OpponentAvatarIndex { get; private set; }

    public static bool HasPending => !string.IsNullOrEmpty(RoomName);

    public static void Set(string roomName, string opponentUserId, string opponentDisplayName, int opponentAvatarIndex)
    {
        RoomName = roomName;
        OpponentUserId = opponentUserId;
        OpponentDisplayName = opponentDisplayName;
        OpponentAvatarIndex = opponentAvatarIndex;
    }

    public static void Clear()
    {
        RoomName = null;
        OpponentUserId = null;
        OpponentDisplayName = null;
        OpponentAvatarIndex = 0;
    }
}
