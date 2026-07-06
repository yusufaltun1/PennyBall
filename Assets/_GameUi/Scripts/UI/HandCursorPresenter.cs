using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(10000)]
public class HandCursorPresenter : MonoBehaviour
{
    [SerializeField] Sprite _defaultHand;
    [SerializeField] Sprite _pressedHand;
    [SerializeField] float _screenSize = 220f;

    RectTransform _handRect;
    Image _handImage;
    bool _isActive;

    public void Configure(Sprite defaultHand, Sprite pressedHand, float screenSize)
    {
        _defaultHand = defaultHand;
        _pressedHand = pressedHand;
        _screenSize = screenSize;
        EnsureVisual();
        ApplyHandSprite(false);
        SetActiveState(true);
    }

    public void Configure(HandCursorConfig config)
    {
        if (config == null)
        {
            return;
        }

        Configure(config.DefaultHand, config.PressedHand, config.ScreenSize);
    }

    public void SetActiveState(bool active)
    {
        _isActive = active;

        if (_handRect != null)
        {
            _handRect.gameObject.SetActive(active);
        }

        Cursor.visible = !active;
    }

    void OnDestroy()
    {
        Cursor.visible = true;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!_isActive)
        {
            return;
        }

        Cursor.visible = !hasFocus;
    }

    void LateUpdate()
    {
        if (!_isActive || _handRect == null)
        {
            return;
        }

        Cursor.visible = false;

        if (!TryGetPointerState(out Vector2 screenPosition, out bool pressed))
        {
            return;
        }

        _handRect.position = new Vector3(screenPosition.x, screenPosition.y, 0f);
        ApplyHandSprite(pressed);
    }

    void EnsureVisual()
    {
        if (_handRect != null)
        {
            return;
        }

        var canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            gameObject.AddComponent<GraphicRaycaster>().enabled = false;
        }

        RectTransform canvasRect = transform as RectTransform;
        if (canvasRect == null)
        {
            canvasRect = gameObject.AddComponent<RectTransform>();
        }

        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;
        canvasRect.localScale = Vector3.one;
        canvasRect.localPosition = Vector3.zero;

        var handObject = new GameObject("Hand");
        handObject.transform.SetParent(transform, false);
        _handRect = handObject.AddComponent<RectTransform>();
        _handRect.anchorMin = new Vector2(0.5f, 0.5f);
        _handRect.anchorMax = new Vector2(0.5f, 0.5f);

        _handImage = handObject.AddComponent<Image>();
        _handImage.raycastTarget = false;
        _handImage.preserveAspect = true;
    }

    void ApplyHandSprite(bool pressed)
    {
        if (_handImage == null)
        {
            return;
        }

        Sprite targetSprite = pressed ? _pressedHand : _defaultHand;
        if (targetSprite == null)
        {
            targetSprite = _defaultHand;
        }

        if (targetSprite == null)
        {
            return;
        }

        if (_handImage.sprite == targetSprite)
        {
            return;
        }

        _handImage.sprite = targetSprite;
        _handRect.pivot = GetSpritePivot(targetSprite);
        _handImage.SetNativeSize();
        ApplyDisplaySize(targetSprite);
    }

    void ApplyDisplaySize(Sprite sprite)
    {
        if (_handRect == null || sprite == null)
        {
            return;
        }

        Vector2 nativeSize = sprite.rect.size;
        float scale = _screenSize / Mathf.Max(nativeSize.x, nativeSize.y);
        _handRect.sizeDelta = nativeSize * scale;
    }

    static Vector2 GetSpritePivot(Sprite sprite)
    {
        Rect rect = sprite.rect;
        return new Vector2(sprite.pivot.x / rect.width, sprite.pivot.y / rect.height);
    }

    static bool TryGetPointerState(out Vector2 screenPosition, out bool pressed)
    {
        screenPosition = default;
        pressed = false;

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null
            && touchscreen.primaryTouch.press.isPressed)
        {
            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            pressed = true;
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            pressed = mouse.leftButton.isPressed;
            return true;
        }

        if (touchscreen != null)
        {
            screenPosition = touchscreen.primaryTouch.position.ReadValue();
            pressed = touchscreen.primaryTouch.press.isPressed;
            return true;
        }

        Pointer pointer = Pointer.current;
        if (pointer != null)
        {
            screenPosition = pointer.position.ReadValue();
            pressed = pointer.press.isPressed;
            return true;
        }

        return false;
    }
}
