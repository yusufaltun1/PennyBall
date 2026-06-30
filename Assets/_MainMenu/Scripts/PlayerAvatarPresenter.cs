using UnityEngine;
using UnityEngine.UI;

public class PlayerAvatarPresenter : MonoBehaviour
{
    [SerializeField] Image _avatarImage;

    AvatarSpriteLibrary _library;

    void Awake()
    {
        _library = AvatarSpriteLibrary.Load();
    }

    void OnEnable()
    {
        Refresh();
        if (LeagueService.Instance != null)
            LeagueService.Instance.AvatarChanged += Refresh;
    }

    void OnDisable()
    {
        if (LeagueService.Instance != null)
            LeagueService.Instance.AvatarChanged -= Refresh;
    }

    void Refresh()
    {
        if (_avatarImage == null || _library == null || LeagueService.Instance == null) return;
        _avatarImage.sprite = _library.Get(LeagueService.Instance.PlayerAvatarIndex);
    }
}
