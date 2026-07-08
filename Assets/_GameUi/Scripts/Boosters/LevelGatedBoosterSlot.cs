using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class LevelGatedBoosterSlot : MonoBehaviour
{
    [SerializeField] GameObject _levelLabelRoot;
    [SerializeField] TextMeshProUGUI _levelLabel;
    [SerializeField] GameObject _unlockedContent;
    [SerializeField] Image _iconImage;
    [SerializeField] Sprite _lockedIconSprite;
    [SerializeField] Sprite _unlockedIconSprite;
    [SerializeField] Button _button;

    public void Refresh(int playerLevel, BoosterType boosterType)
    {
        int unlockLevel = BoosterConfig.GetUnlockLevel(boosterType);
        bool unlocked = playerLevel >= unlockLevel;

        if (_levelLabelRoot != null)
        {
            _levelLabelRoot.SetActive(!unlocked);
        }

        if (_levelLabel != null)
        {
            _levelLabel.text = $"Lvl. {unlockLevel}";
        }

        // Icon her zaman görünür; kilitliyken BW, açıkken renkli sprite.
        if (_unlockedContent != null)
        {
            _unlockedContent.SetActive(true);
        }

        ApplyIconSprite(unlocked);

        if (_button != null)
        {
            _button.interactable = unlocked;
        }
    }

    void ApplyIconSprite(bool unlocked)
    {
        if (_iconImage == null)
        {
            return;
        }

        Sprite sprite = unlocked ? _unlockedIconSprite : _lockedIconSprite;
        if (sprite == null)
        {
            sprite = unlocked ? _lockedIconSprite : _unlockedIconSprite;
        }

        if (sprite != null)
        {
            _iconImage.sprite = sprite;
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (_levelLabelRoot == null)
        {
            Transform label = transform.Find("Text (TMP)");
            if (label != null)
            {
                _levelLabelRoot = label.gameObject;
                _levelLabel = label.GetComponent<TextMeshProUGUI>();
            }
        }

        if (_unlockedContent == null)
        {
            Transform icon = transform.Find("Icon");
            if (icon != null)
            {
                _unlockedContent = icon.gameObject;
            }
        }

        if (_iconImage == null && _unlockedContent != null)
        {
            _iconImage = _unlockedContent.GetComponent<Image>();
        }

        if (_button == null)
        {
            _button = GetComponent<Button>();
        }
    }
#endif
}
