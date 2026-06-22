using UnityEngine;
using UnityEngine.InputSystem;

public class CoinInputHandler : MonoBehaviour
{
    [SerializeField] Camera _camera;
    [SerializeField] float _tableHeight = 0.139f;
    [SerializeField] float _maxPickDistance = 50f;

    readonly RaycastHit[] _raycastHits = new RaycastHit[8];

    CoinDragController _activeCoin;
    Plane _tablePlane;

    void Awake()
    {
        ResolveCamera();
        _tablePlane = new Plane(Vector3.up, new Vector3(0f, _tableHeight, 0f));
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
        if (!TryReadPointer(out Vector2 screenPosition, out bool isPressed, out bool pressedThisFrame, out bool releasedThisFrame))
        {
            return;
        }

        if (pressedThisFrame)
        {
        if (GameRulesManager.Instance != null && GameRulesManager.Instance.IsResolvingMove)
        {
            return;
        }

        TryBeginAim(screenPosition);
        }
        else if (isPressed && _activeCoin != null)
        {
            if (TryGetTablePosition(screenPosition, out Vector3 worldPosition))
            {
                _activeCoin.UpdateAim(worldPosition);
            }
        }
        else if (releasedThisFrame && _activeCoin != null)
        {
            CoinDragController releasedCoin = _activeCoin;
            CoinIdentity identity = releasedCoin.GetComponent<CoinIdentity>();

            releasedCoin.ReleaseAim();

            if (identity != null && releasedCoin.IsSliding)
            {
                if (GameRulesManager.Instance != null)
                {
                    GameRulesManager.Instance.OnShotReleased(identity);
                }
            }

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
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
        {
            var touch = touchscreen.primaryTouch;
            screenPosition = touch.position.ReadValue();
            isPressed = touch.press.isPressed;
            pressedThisFrame = touch.press.wasPressedThisFrame;
            releasedThisFrame = touch.press.wasReleasedThisFrame;
            return true;
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
        if (_activeCoin != null)
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
            CoinDragController coin = _raycastHits[i].collider.GetComponentInParent<CoinDragController>();
            if (coin == null || coin.IsAiming || coin.IsSliding)
            {
                continue;
            }

            CoinIdentity identity = coin.GetComponent<CoinIdentity>();
            if (identity == null || identity.Team != CoinTeam.Player)
            {
                continue;
            }

            if (identity.IsPassive)
            {
                continue;
            }

            if (GameRulesManager.Instance != null && !GameRulesManager.Instance.CanSelectCoin(identity))
            {
                continue;
            }

            _activeCoin = coin;
            _activeCoin.BeginAim();

            if (TryGetTablePosition(screenPosition, out Vector3 worldPosition))
            {
                _activeCoin.UpdateAim(worldPosition);
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
