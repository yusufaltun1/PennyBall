using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class IceBoosterController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] Button _iceButton;
    [SerializeField] GameObject _icon;
    [SerializeField] TextMeshProUGUI _remainingText;

    [Header("Booster Süreleri")]
    [SerializeField] IceBoosterTimingSettings _boosterTiming = new();

    [Header("IceUsed Görsel Süreleri")]
    [SerializeField] IceUsedFeedbackTimingSettings _usedFeedbackTiming = new();

    [Header("Audio")]
    [SerializeField] AudioClip _freezeSound;
    [SerializeField] [Range(0f, 1f)] float _freezeSoundVolume = 1f;

    [Header("Frost Visuals")]
    [SerializeField] IceCoinFrostVisual.Settings _frostSettings = new();

    [Header("Used Feedback")]
    [SerializeField] IceUsedBoosterFeedback _iceUsedFeedback;

    Coroutine _activationRoutine;
    AudioSource _audioSource;

    void Awake()
    {
        ResolveReferences();
        ResetReadyVisuals();
    }

    void OnEnable()
    {
        if (_iceButton != null)
        {
            _iceButton.onClick.AddListener(OnIceButtonClicked);
        }
    }

    void OnDisable()
    {
        if (_iceButton != null)
        {
            _iceButton.onClick.RemoveListener(OnIceButtonClicked);
        }
    }

    void OnDestroy()
    {
        if (_activationRoutine != null)
        {
            StopCoroutine(_activationRoutine);
            _activationRoutine = null;
            IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
            IceOpponentFreezeUtility.ResumeOpponentBotAfterIceBooster();
        }
    }

    void ResolveReferences()
    {
        if (_iceButton == null)
        {
            _iceButton = GetComponent<Button>();
        }

        if (_icon == null)
        {
            Transform iconTransform = transform.Find("Icon");
            if (iconTransform != null)
            {
                _icon = iconTransform.gameObject;
            }
        }

        if (_remainingText == null)
        {
            Transform remainingTransform = transform.Find("Remaining");
            if (remainingTransform != null)
            {
                _remainingText = remainingTransform.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_iceUsedFeedback == null)
        {
            GameObject boostersUsed = GameObject.Find("BoostersUsed");
            if (boostersUsed != null)
            {
                _iceUsedFeedback = boostersUsed.GetComponent<IceUsedBoosterFeedback>();
            }
        }
    }

    void OnIceButtonClicked()
    {
        if (_activationRoutine != null)
        {
            return;
        }

        _activationRoutine = StartCoroutine(ActivateIceBoosterRoutine());
    }

    IEnumerator ActivateIceBoosterRoutine()
    {
        SetButtonInteractable(false);

        try
        {
            yield return IceOpponentFreezeUtility.WaitUntilOpponentReadyForIceBooster();

            IceOpponentFreezeUtility.PauseOpponentBotForIceBooster();

            IceOpponentFreezeUtility.FreezeAllOpponentCoins(
                _boosterTiming.FreezeDurationSeconds,
                _frostSettings);
            PlayFreezeSound();
            _iceUsedFeedback?.Play(_usedFeedbackTiming);
            BeginCooldownVisuals(_boosterTiming.CooldownSeconds);

            float startTime = Time.time;
            bool coinsUnfrozen = false;

            while (true)
            {
                float elapsed = Time.time - startTime;
                int remainingSeconds = _boosterTiming.CooldownSeconds - Mathf.FloorToInt(elapsed);
                UpdateRemainingText(Mathf.Max(0, remainingSeconds));

                if (!coinsUnfrozen && elapsed >= _boosterTiming.FreezeDurationSeconds)
                {
                    IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
                    IceOpponentFreezeUtility.ResumeOpponentBotAfterIceBooster();
                    coinsUnfrozen = true;
                }

                if (elapsed >= _boosterTiming.CooldownSeconds)
                {
                    break;
                }

                yield return null;
            }

            UpdateRemainingText(0);
            IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
            ResetReadyVisuals();
        }
        finally
        {
            IceOpponentFreezeUtility.UnfreezeAllOpponentCoins();
            IceOpponentFreezeUtility.ResumeOpponentBotAfterIceBooster();
            _activationRoutine = null;
        }
    }

    void BeginCooldownVisuals(int remainingSeconds)
    {
        if (_icon != null)
        {
            _icon.SetActive(false);
        }

        if (_remainingText != null)
        {
            _remainingText.gameObject.SetActive(true);
            UpdateRemainingText(remainingSeconds);
        }
    }

    void ResetReadyVisuals()
    {
        if (_icon != null)
        {
            _icon.SetActive(true);
        }

        if (_remainingText != null)
        {
            _remainingText.gameObject.SetActive(false);
        }

        SetButtonInteractable(true);
    }

    void UpdateRemainingText(int remainingSeconds)
    {
        if (_remainingText != null)
        {
            _remainingText.text = Mathf.Max(0, remainingSeconds).ToString();
        }
    }

    void SetButtonInteractable(bool interactable)
    {
        if (_iceButton != null)
        {
            _iceButton.interactable = interactable;
        }
    }

    void PlayFreezeSound()
    {
        if (_freezeSound == null)
        {
            return;
        }

        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f;
        }

        _audioSource.PlayOneShot(_freezeSound, _freezeSoundVolume);
    }
}
