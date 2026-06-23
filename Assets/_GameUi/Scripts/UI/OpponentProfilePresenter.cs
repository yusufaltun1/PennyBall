using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Maç ekranında rakip adını ve avatarını gösterir.
/// Inspector'dan Text ve Image referanslarını bağla.
/// </summary>
public class OpponentProfilePresenter : MonoBehaviour
{
    [SerializeField] Text _nameLabel;
    [SerializeField] Image _avatarImage;
    [SerializeField] Sprite[] _avatarSprites;

    void OnEnable()
    {
        StartCoroutine(BindWhenReady());
    }

    void OnDisable()
    {
        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.MatchCompleted -= OnMatchCompleted;
        }
    }

    IEnumerator BindWhenReady()
    {
        while (OpponentBotController.Instance == null)
        {
            yield return null;
        }

        if (LeagueMatchController.Instance != null)
        {
            LeagueMatchController.Instance.MatchCompleted += OnMatchCompleted;
        }

        Refresh();
    }

    void OnMatchCompleted(MatchResultType result)
    {
        Refresh();
    }

    void Refresh()
    {
        if (OpponentBotController.Instance == null)
        {
            return;
        }

        if (_nameLabel != null)
        {
            _nameLabel.text = OpponentBotController.Instance.OpponentDisplayName;
        }

        if (_avatarImage != null && _avatarSprites != null && _avatarSprites.Length > 0)
        {
            int avatarIndex = OpponentBotController.Instance.OpponentAvatarIndex;
            avatarIndex = Mathf.Clamp(avatarIndex, 0, _avatarSprites.Length - 1);
            _avatarImage.sprite = _avatarSprites[avatarIndex];
        }
    }
}
