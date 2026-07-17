using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class BoostersMenuController : MonoBehaviour
{
    const float ComingSoonVisibleSeconds = 1.5f;

    [Header("Ice")]
    [SerializeField] GameObject _iceLockedOverlay;
    [SerializeField] GameObject _iceBooster;

    [Header("Time")]
    [SerializeField] GameObject _timeLockedOverlay;
    [SerializeField] GameObject _timeBooster;

    [Header("Level Slots")]
    [SerializeField] LevelGatedBoosterSlot _booster3Slot;
    [SerializeField] LevelGatedBoosterSlot _booster4Slot;

    [Header("Coming Soon")]
    [SerializeField] GameObject _comingSoon;

    IceBoosterController _iceController;
    TimeBoosterController _timeController;
    Coroutine _comingSoonRoutine;

    void Awake()
    {
        if (ExerciseRuntime.IsActive)
        {
            Destroy(gameObject);
            return;
        }

        ResolveReferences();
        WireComingSoonBoosters();
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

        if (_comingSoonRoutine != null)
        {
            StopCoroutine(_comingSoonRoutine);
            _comingSoonRoutine = null;
        }

        if (_comingSoon != null)
        {
            _comingSoon.SetActive(false);
        }
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

        if (_comingSoon == null)
        {
            if (transform.parent != null)
            {
                Transform sibling = transform.parent.Find("ComingSoon");
                if (sibling != null)
                {
                    _comingSoon = sibling.gameObject;
                }
            }

            if (_comingSoon == null)
            {
                Canvas parentCanvas = GetComponentInParent<Canvas>();
                if (parentCanvas != null)
                {
                    Transform found = parentCanvas.transform.Find("ComingSoon");
                    if (found != null)
                    {
                        _comingSoon = found.gameObject;
                    }
                }
            }
        }
    }

    void WireComingSoonBoosters()
    {
        WireComingSoonButton(_booster3Slot);
        WireComingSoonButton(_booster4Slot);
    }

    void WireComingSoonButton(LevelGatedBoosterSlot slot)
    {
        Button button = slot != null ? slot.Button : null;
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(OnComingSoonBoosterClicked);
        button.onClick.AddListener(OnComingSoonBoosterClicked);
    }

    void OnComingSoonBoosterClicked()
    {
        // Booster3 / Booster4 henüz aktif değil — coin harcanmaz.
        if (_comingSoonRoutine != null)
        {
            StopCoroutine(_comingSoonRoutine);
        }

        _comingSoonRoutine = StartCoroutine(ShowComingSoonRoutine());
    }

    IEnumerator ShowComingSoonRoutine()
    {
        ResolveReferences();
        if (_comingSoon == null)
        {
            _comingSoonRoutine = null;
            yield break;
        }

        _comingSoon.SetActive(true);
        yield return new WaitForSecondsRealtime(ComingSoonVisibleSeconds);
        if (_comingSoon != null)
        {
            _comingSoon.SetActive(false);
        }

        _comingSoonRoutine = null;
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

        _booster3Slot?.Refresh(playerLevel, BoosterType.GoalKeeper, requiresCoins: false);
        _booster4Slot?.Refresh(playerLevel, BoosterType.LastCoin, requiresCoins: false);

        _iceController?.RefreshWalletState();
        _timeController?.RefreshWalletState();
    }
}
