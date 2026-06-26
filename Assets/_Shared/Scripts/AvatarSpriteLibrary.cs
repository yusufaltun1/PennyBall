using UnityEngine;

[CreateAssetMenu(fileName = "AvatarSpriteLibrary", menuName = "PennyBall/Avatar Sprite Library")]
public class AvatarSpriteLibrary : ScriptableObject
{
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
}
