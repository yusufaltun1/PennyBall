using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerProfilePresenter : MonoBehaviour
{
    const float FakeAvatarAlpha = 0.5019608f;

    [SerializeField] Image _avatarImage;
    [SerializeField] Image _fakeLeftAvatarImage;
    [SerializeField] Image _fakeRightAvatarImage;
    [SerializeField] AvatarSpriteLibrary _avatarLibrary;
    [SerializeField] TextMeshProUGUI _nameLabelTMP;
    [SerializeField] Text _nameLabel;
    [SerializeField] TextMeshProUGUI _leaguePointsText;

    int _cachedActiveAvatarIndex = -1;
    int _cachedFakeLeftIndex = -1;
    int _cachedFakeRightIndex = -1;
    bool _leagueEventsBound;

    void Awake()
    {
        ResolveFakeAvatarReferences();
        ResolveLeaguePointsText();
    }

    void Start()
    {
        StartCoroutine(BindLeagueEventsWhenReady());
    }

    void OnEnable()
    {
        Refresh();
    }

    void OnDisable()
    {
        UnbindLeagueEvents();
    }

    IEnumerator BindLeagueEventsWhenReady()
    {
        while (LeagueService.Instance == null)
        {
            yield return null;
        }

        BindLeagueEvents();
        Refresh();
    }

    void BindLeagueEvents()
    {
        if (_leagueEventsBound || LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.AvatarChanged += Refresh;
        LeagueService.Instance.DisplayNameChanged += Refresh;
        LeagueService.Instance.StandingsUpdated += Refresh;
        _leagueEventsBound = true;
    }

    void UnbindLeagueEvents()
    {
        if (!_leagueEventsBound || LeagueService.Instance == null)
        {
            return;
        }

        LeagueService.Instance.AvatarChanged -= Refresh;
        LeagueService.Instance.DisplayNameChanged -= Refresh;
        LeagueService.Instance.StandingsUpdated -= Refresh;
        _leagueEventsBound = false;
    }

    void ResolveFakeAvatarReferences()
    {
        Transform avatarRoot = transform.parent;
        if (avatarRoot == null)
        {
            return;
        }

        if (_fakeLeftAvatarImage == null)
        {
            Transform left = avatarRoot.Find("Fake_Sol/AvatarMask/AvatarImage");
            if (left != null)
            {
                _fakeLeftAvatarImage = left.GetComponent<Image>();
            }
        }

        if (_fakeRightAvatarImage == null)
        {
            Transform right = avatarRoot.Find("Fake_Sag/AvatarMask/AvatarImage");
            if (right != null)
            {
                _fakeRightAvatarImage = right.GetComponent<Image>();
            }
        }
    }

    void ResolveLeaguePointsText()
    {
        if (_leaguePointsText != null)
        {
            return;
        }

        Transform profileRoot = transform.parent != null ? transform.parent.parent : null;
        if (profileRoot == null)
        {
            return;
        }

        Transform puanText = profileRoot.Find("PuanBar/Texts/PuanText");
        if (puanText != null)
        {
            _leaguePointsText = puanText.GetComponent<TextMeshProUGUI>();
        }
    }

    void Refresh()
    {
        if (LeagueService.Instance == null)
            return;

        Sprite avatarSprite = _avatarLibrary != null
            ? _avatarLibrary.Get(LeagueService.Instance.PlayerAvatarIndex)
            : null;

        if (_avatarImage != null && avatarSprite != null)
            _avatarImage.sprite = avatarSprite;

        RefreshFakeAvatars(LeagueService.Instance.PlayerAvatarIndex);

        string name = LeagueService.Instance.Save?.playerDisplayName ?? "Player";
        if (_nameLabelTMP != null)
            _nameLabelTMP.SetText(name);
        else if (_nameLabel != null)
            _nameLabel.text = name;

        RefreshLeaguePoints();
    }

    void RefreshLeaguePoints()
    {
        if (_leaguePointsText == null)
        {
            ResolveLeaguePointsText();
        }

        if (_leaguePointsText == null)
        {
            return;
        }

        _leaguePointsText.SetText(LeagueService.Instance.GetPlayerPoints().ToString());
    }

    void RefreshFakeAvatars(int activeIndex)
    {
        if (_avatarLibrary == null || _avatarLibrary.Count <= 1)
        {
            ApplyFakeAvatar(_fakeLeftAvatarImage, null);
            ApplyFakeAvatar(_fakeRightAvatarImage, null);
            return;
        }

        if (_cachedActiveAvatarIndex != activeIndex
            || _cachedFakeLeftIndex < 0
            || _cachedFakeRightIndex < 0)
        {
            _cachedFakeLeftIndex = PickInactiveAvatarIndex(activeIndex, excludeIndex: -1);
            _cachedFakeRightIndex = PickInactiveAvatarIndex(activeIndex, _cachedFakeLeftIndex);
            _cachedActiveAvatarIndex = activeIndex;
        }

        ApplyFakeAvatar(_fakeLeftAvatarImage, _avatarLibrary.Get(_cachedFakeLeftIndex));
        ApplyFakeAvatar(_fakeRightAvatarImage, _avatarLibrary.Get(_cachedFakeRightIndex));
    }

    int PickInactiveAvatarIndex(int activeIndex, int excludeIndex)
    {
        int count = _avatarLibrary.Count;
        int inactiveCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (i != activeIndex && i != excludeIndex)
            {
                inactiveCount++;
            }
        }

        if (inactiveCount <= 0)
        {
            return (activeIndex + 1) % count;
        }

        int pick = Random.Range(0, inactiveCount);

        for (int i = 0; i < count; i++)
        {
            if (i == activeIndex || i == excludeIndex)
            {
                continue;
            }

            if (pick == 0)
            {
                return i;
            }

            pick--;
        }

        return (activeIndex + 1) % count;
    }

    static void ApplyFakeAvatar(Image image, Sprite avatarSprite)
    {
        if (image == null || avatarSprite == null)
        {
            return;
        }

        image.sprite = avatarSprite;

        Color color = image.color;
        color.a = FakeAvatarAlpha;
        image.color = color;
    }
}
