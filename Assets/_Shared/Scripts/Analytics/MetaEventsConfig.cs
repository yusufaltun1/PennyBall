/// <summary>
/// Meta App Events kimlikleri. Login kullanılmaz — sadece ölçüm / reklam optimizasyonu.
/// Dashboard: developers.facebook.com → Settings → Basic
/// </summary>
public static class MetaEventsConfig
{
    public const string AppId = "1525809872892937";
    public const string ClientToken = "d2ab6af73cad0dbbc4c3f41c2d7bebc5";
    public const string DisplayName = "PennyBall";

    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppId)
        && !string.IsNullOrWhiteSpace(ClientToken);
}
