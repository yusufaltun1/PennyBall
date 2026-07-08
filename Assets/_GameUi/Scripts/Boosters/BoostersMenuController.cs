using UnityEngine;

[DisallowMultipleComponent]
public class BoostersMenuController : MonoBehaviour
{
    [Header("Ice")]
    [SerializeField] GameObject _iceLockedOverlay;
    [SerializeField] GameObject _iceBooster;

    [Header("Time")]
    [SerializeField] GameObject _timeLockedOverlay;
    [SerializeField] GameObject _timeBooster;

    [Header("Level Slots")]
    [SerializeField] LevelGatedBoosterSlot _booster3Slot;
    [SerializeField] LevelGatedBoosterSlot _booster4Slot;

    IceBoosterController _iceController;
    TimeBoosterController _timeController;

    void Awake()
    {
        if (ExerciseRuntime.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
    }

    void OnEnable()
    {
        WalletService.Changed += Refresh;
        WalletService.LevelChanged += OnLevelChanged;
        Refresh();
    }

    void OnDisable()
    {
        WalletService.Changed -= Refresh;
        WalletService.LevelChanged -= OnLevelChanged;
    }

    void OnLevelChanged(int levelBefore, int levelAfter)
    {
        Refresh();
    }

    void ResolveReferences()
    {
        if (_iceLockedOverlay == null)
        {
            Transform found = transform.Find("Ice_Unlocked");
            if (found != null)
            {
                _iceLockedOverlay = found.gameObject;
            }
        }

        if (_iceBooster == null)
        {
            Transform found = transform.Find("Ice");
            if (found != null)
            {
                _iceBooster = found.gameObject;
                _iceController = found.GetComponent<IceBoosterController>();
            }
        }
        else if (_iceController == null && _iceBooster != null)
        {
            _iceController = _iceBooster.GetComponent<IceBoosterController>();
        }

        if (_timeLockedOverlay == null)
        {
            Transform found = transform.Find("Time_Unlocked");
            if (found != null)
            {
                _timeLockedOverlay = found.gameObject;
            }
        }

        if (_timeBooster == null)
        {
            Transform found = transform.Find("Time");
            if (found != null)
            {
                _timeBooster = found.gameObject;
                _timeController = found.GetComponent<TimeBoosterController>();
            }
        }
        else if (_timeController == null && _timeBooster != null)
        {
            _timeController = _timeBooster.GetComponent<TimeBoosterController>();
        }

        if (_booster3Slot == null)
        {
            Transform found = transform.Find("Booster3");
            if (found != null)
            {
                _booster3Slot = found.GetComponent<LevelGatedBoosterSlot>();
            }
        }

        if (_booster4Slot == null)
        {
            Transform found = transform.Find("Booster4");
            if (found != null)
            {
                _booster4Slot = found.GetComponent<LevelGatedBoosterSlot>();
            }
        }
    }

    public void Refresh()
    {
        int playerLevel = WalletService.Level;

        bool iceUnlocked = BoosterConfig.IsUnlockedAtLevel(BoosterType.Freeze, playerLevel);
        if (_iceLockedOverlay != null)
        {
            _iceLockedOverlay.SetActive(!iceUnlocked);
        }

        if (_iceBooster != null)
        {
            _iceBooster.SetActive(iceUnlocked);
        }

        bool timeUnlocked = BoosterConfig.IsUnlockedAtLevel(BoosterType.Time, playerLevel);
        if (_timeLockedOverlay != null)
        {
            _timeLockedOverlay.SetActive(!timeUnlocked);
        }

        if (_timeBooster != null)
        {
            _timeBooster.SetActive(timeUnlocked);
        }

        _booster3Slot?.Refresh(playerLevel, BoosterType.GoalKeeper);
        _booster4Slot?.Refresh(playerLevel, BoosterType.LastCoin);

        _iceController?.RefreshWalletState();
        _timeController?.RefreshWalletState();
    }
}
