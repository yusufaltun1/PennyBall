using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class OnboardingCoinInputHandler : MonoBehaviour
{
    [SerializeField] Camera _camera;
    [SerializeField] OnboardingController _controller;
    [SerializeField] float _tableHeight = 0.139f;
    [SerializeField] float _maxPickDistance = 50f;

    readonly RaycastHit[] _raycastHits = new RaycastHit[8];
    Plane _tablePlane;
    OnboardingCoinDragController _activeCoin;
    OnboardingAimFeedback _lastValidFeedback;
    Vector3 _lastValidPullPosition;
    bool _hasLastValidPull;

    void Awake()
    {
        ResolveCamera();
        _tablePlane = new Plane(Vector3.up, new Vector3(0f, _tableHeight, 0f));

        if (_controller == null)
        {
            _controller = FindFirstObjectByType<OnboardingController>();
        }
    }

    public void Bind(Camera camera, OnboardingController controller)
    {
        _camera = camera;
        _controller = controller;
    }

    void ResolveCamera()
    {
        if (_camera != null)
        {
            return;
        }

        _camera = GetComponent<Camera>();
        if (_camera == null)
        {
            _camera = Camera.main;
        }
    }

    void Update()
    {
        if (_controller == null)
        {
            return;
        }

        if (!TryReadPointer(
                out Vector2 screenPosition,
                out bool isPressed,
                out bool pressedThisFrame,
                out bool releasedThisFrame))
        {
            return;
        }

        if (pressedThisFrame)
        {
            TryBeginAim(screenPosition);
        }
        else if (isPressed && _activeCoin != null)
        {
            if (TryGetTablePosition(screenPosition, out Vector3 worldPosition))
            {
                _activeCoin.UpdateAim(worldPosition, default);
                _controller.ValidateAim(_activeCoin, out OnboardingAimFeedback feedback);
                if (feedback.IsFullyValid)
                {
                    _lastValidFeedback = feedback;
                    _lastValidPullPosition = worldPosition;
                    _hasLastValidPull = true;
                }
                else
                {
                    _lastValidFeedback = feedback;
                    _lastValidPullPosition = worldPosition;
                    _hasLastValidPull = false;
                }

                _activeCoin.UpdateAim(worldPosition, feedback);
            }
        }
        else if (releasedThisFrame && _activeCoin != null)
        {
            OnboardingCoinDragController releasedCoin = _activeCoin;

            if (TryGetTablePosition(screenPosition, out Vector3 releasePosition))
            {
                releasedCoin.UpdateAim(releasePosition, default);
                _controller.ValidateAim(releasedCoin, out OnboardingAimFeedback releaseFeedback);
                _lastValidFeedback = releaseFeedback;
                _lastValidPullPosition = releasePosition;
                _hasLastValidPull = releaseFeedback.IsFullyValid;
                releasedCoin.UpdateAim(releasePosition, releaseFeedback);
            }

            bool canRelease = _hasLastValidPull && _lastValidFeedback.IsFullyValid;
            if (canRelease)
            {
                releasedCoin.UpdateAim(_lastValidPullPosition, _lastValidFeedback);

                if (!releasedCoin.TryReleaseAimAt(_lastValidPullPosition))
                {
                    releasedCoin.CancelAim();
                }
            }
            else
            {
                releasedCoin.CancelAim();
            }

            _lastValidFeedback = default;
            _hasLastValidPull = false;
            _activeCoin = null;
        }
    }

    static bool TryReadPointer(
        out Vector2 screenPosition,
        out bool isPressed,
        out bool pressedThisFrame,
        out bool releasedThisFrame)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null)
        {
            var touch = touchscreen.primaryTouch;
            screenPosition = touch.position.ReadValue();
            isPressed = touch.press.isPressed;
            pressedThisFrame = touch.press.wasPressedThisFrame;
            releasedThisFrame = touch.press.wasReleasedThisFrame;

            if (isPressed || pressedThisFrame || releasedThisFrame)
            {
                return true;
            }
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            isPressed = mouse.leftButton.isPressed;
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            return true;
        }

        screenPosition = default;
        isPressed = false;
        pressedThisFrame = false;
        releasedThisFrame = false;
        return false;
    }

    void TryBeginAim(Vector2 screenPosition)
    {
        if (_activeCoin != null || (_controller != null && _controller.IsBusy))
        {
            return;
        }

        if (_camera == null)
        {
            ResolveCamera();
        }

        if (_camera == null)
        {
            return;
        }

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        int hitCount = Physics.RaycastNonAlloc(ray, _raycastHits, _maxPickDistance);

        for (int i = 0; i < hitCount; i++)
        {
            OnboardingCoinDragController coin = _raycastHits[i].collider.GetComponentInParent<OnboardingCoinDragController>();
            if (coin == null || coin.IsAiming || coin.IsSliding)
            {
                continue;
            }

            OnboardingCoin identity = coin.GetComponent<OnboardingCoin>();
            if (identity == null || !_controller.CanSelectCoin(identity))
            {
                continue;
            }

            _activeCoin = coin;
            _lastValidFeedback = default;
            _hasLastValidPull = false;
            _activeCoin.BeginAim();

            if (TryGetTablePosition(screenPosition, out Vector3 worldPosition))
            {
                _controller.ValidateAim(_activeCoin, out OnboardingAimFeedback feedback);
                _activeCoin.UpdateAim(worldPosition, feedback);
            }

            return;
        }
    }

    bool TryGetTablePosition(Vector2 screenPosition, out Vector3 worldPosition)
    {
        if (_camera == null)
        {
            worldPosition = default;
            return false;
        }

        Ray ray = _camera.ScreenPointToRay(screenPosition);
        if (_tablePlane.Raycast(ray, out float enter))
        {
            worldPosition = ray.GetPoint(enter);
            return true;
        }

        worldPosition = default;
        return false;
    }
}
