using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfilePresenter : MonoBehaviour
{
    [SerializeField] Image _avatarImage;
    [SerializeField] AvatarSpriteLibrary _avatarLibrary;
    [SerializeField] TextMeshProUGUI _nameLabelTMP;
    [SerializeField] Text _nameLabel;
    [SerializeField] TextMeshProUGUI _leaguePointsLabel;

    void Awake()
    {
        ResolveReferences();
    }

    void OnEnable()
    {
        Subscribe();
        Refresh();
        StartCoroutine(SubscribeWhenReady());
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    IEnumerator SubscribeWhenReady()
    {
        while (LeagueService.Instance == null)
        {
            yield return null;
        }

        Subscribe();
        Refresh();
    }

    void Subscribe()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.AvatarChanged -= Refresh;
        LeagueService.Instance.StandingsUpdated -= Refresh;
        LeagueService.Instance.DisplayNameChanged -= Refresh;
        LeagueService.Instance.AvatarChanged += Refresh;
        LeagueService.Instance.StandingsUpdated += Refresh;
        LeagueService.Instance.DisplayNameChanged += Refresh;
    }

    void Unsubscribe()
    {
        if (LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.AvatarChanged -= Refresh;
        LeagueService.Instance.StandingsUpdated -= Refresh;
        LeagueService.Instance.DisplayNameChanged -= Refresh;
    }

    void ResolveReferences()
    {
        if (_leaguePointsLabel != null)
        {
            return;
        }

        Transform matchingScore = transform.Find("PlayerLabel/ScoreContainer/ScoreHolder/Score");
        if (matchingScore != null)
        {
            _leaguePointsLabel = matchingScore.GetComponent<TextMeshProUGUI>();
            return;
        }

        Transform profileRoot = transform.parent;
        while (profileRoot != null && profileRoot.name != "Player_Profile")
        {
            profileRoot = profileRoot.parent;
        }

        if (profileRoot == null)
        {
            return;
        }

        Transform pointsText = profileRoot.Find("PuanBar/Texts/PuanText");
        if (pointsText != null)
        {
            _leaguePointsLabel = pointsText.GetComponent<TextMeshProUGUI>();
        }
    }

    void Refresh()
    {
        ResolveReferences();

        LeagueSaveData save = LeagueService.Instance?.Save ?? LeagueRepository.Load();

        if (LeagueService.Instance != null && _avatarImage != null && _avatarLibrary != null)
        {
            int avatarIndex = ExerciseRuntime.IsActive
                ? 0
                : LeagueService.Instance.PlayerAvatarIndex;
            _avatarImage.sprite = _avatarLibrary.Get(avatarIndex);
        }

        string name = save?.playerDisplayName ?? "Player";
        if (_nameLabelTMP != null)
        {
            _nameLabelTMP.SetText(name);
        }
        else if (_nameLabel != null)
        {
            _nameLabel.text = name;
        }

        if (_leaguePointsLabel != null)
        {
            _leaguePointsLabel.SetText(LeaguePlayerStats.GetPlayerPoints(save).ToString());
        }
    }
}
