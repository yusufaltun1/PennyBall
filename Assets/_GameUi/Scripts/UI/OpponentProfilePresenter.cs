using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class OpponentProfilePresenter : MonoBehaviour
{
    [SerializeField] Text _nameLabel;
    [SerializeField] TextMeshProUGUI _nameLabelTMP;
    [SerializeField] Image _avatarImage;
    [SerializeField] AvatarSpriteLibrary _avatarLibrary;

    void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    void OnDisable()
    {
        if (LeagueMatchController.Instance != null)
            LeagueMatchController.Instance.MatchCompleted -= Refresh;
    }

    IEnumerator BindWhenReady()
    {
        while (OpponentBotController.Instance == null)
            yield return null;

        if (LeagueMatchController.Instance != null)
            LeagueMatchController.Instance.MatchCompleted += Refresh;

        Refresh();
    }

    void Refresh(MatchResultType _ = default) => Refresh();

    void Refresh()
    {
        if (OpponentBotController.Instance == null)
            return;

        string name = OpponentBotController.Instance.OpponentDisplayName;
        if (_nameLabelTMP != null)
            _nameLabelTMP.SetText(name);
        else if (_nameLabel != null)
            _nameLabel.text = name;

        if (_avatarImage != null && _avatarLibrary != null)
            _avatarImage.sprite = _avatarLibrary.Get(OpponentBotController.Instance.OpponentAvatarIndex);
    }
}
