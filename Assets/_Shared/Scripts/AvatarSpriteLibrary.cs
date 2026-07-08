using UnityEngine;

[CreateAssetMenu(fileName = "AvatarSpriteLibrary", menuName = "PennyBall/Avatar Sprite Library")]
public class AvatarSpriteLibrary : ScriptableObject
{
    public const string DefaultAvatarAssetPath = "Assets/_MainMenu/Textures/Avatars/Avtr_0.png";
    public const string NumberedAvatarsFolder = "Assets/_MainMenu/Textures/Avatars";
    public const int NumberedAvatarMin = 1;
    public const int NumberedAvatarMax = 59;

    [SerializeField] Sprite[] _sprites;

    public int Count => _sprites != null ? _sprites.Length : 0;

    public Sprite Get(int avatarIndex)
    {
        if (_sprites == null || _sprites.Length == 0)
            return null;
        return _sprites[Mathf.Clamp(avatarIndex, 0, _sprites.Length - 1)];
    }

    public static AvatarSpriteLibrary Load()
    {
        return Resources.Load<AvatarSpriteLibrary>(LeagueConfig.AvatarLibraryResourcePath);
    }

    public static string GetNumberedAvatarAssetPath(int number)
    {
        return $"{NumberedAvatarsFolder}/Avtrs_  ({number}).png";
    }
}
