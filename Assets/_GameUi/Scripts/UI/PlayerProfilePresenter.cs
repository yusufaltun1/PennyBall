using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfilePresenter : MonoBehaviour
{
    [SerializeField] Image _avatarImage;
    [SerializeField] AvatarSpriteLibrary _avatarLibrary;
    [SerializeField] TextMeshProUGUI _nameLabelTMP;
    [SerializeField] Text _nameLabel;

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
        if (LeagueService.Instance == null)
            return;

        if (_avatarImage != null && _avatarLibrary != null)
            _avatarImage.sprite = _avatarLibrary.Get(LeagueService.Instance.PlayerAvatarIndex);

        string name = LeagueService.Instance.Save?.playerDisplayName ?? "Player";
        if (_nameLabelTMP != null)
            _nameLabelTMP.SetText(name);
        else if (_nameLabel != null)
            _nameLabel.text = name;
    }
}
