using UnityEngine;
using UnityEngine.InputSystem;

public class CoinInputHandler : MonoBehaviour
{
    [SerializeField] Camera _camera;
    [SerializeField] float _tableHeight = 0.139f;
    [SerializeField] float _maxPickDistance = 50f;
    [SerializeField] CameraAimZoom _cameraZoom;
    [SerializeField] float _touchPickRadius = 0.08f;

    readonly RaycastHit[] _raycastHits = new RaycastHit[16];
    static CoinDragController[] _coinBuffer;

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
        if (MatchBeginningCountdownController.IsActive || MatchIntroCameraFlythrough.IsActive)
        {
            _cameraZoom?.SetDragState(0f);
            return;
        }

        if (!TryReadPointer(
                out Vector2 screenPosition,
                out bool isPressed,
                out bool pressedThisFrame,
                out bool releasedThisFrame,
                out bool isFromTouch))
        {
            _cameraZoom?.SetDragState(0f);
            return;
        }

        float pullRatio = 0f;
        float sideRatio = 0f;

        if (pressedThisFrame)
        {
            if (GameRulesManager.Instance != null && GameRulesManager.Instance.IsResolvingMove)
            {
                return;
            }

            TryBeginAim(screenPosition, isFromTouch);
        }
        else if (isPressed && _activeCoin != null)
        {
            if (TryGetTablePosition(screenPosition, out Vector3 worldPosition))
            {
                _activeCoin.UpdateAim(worldPosition);
                Vector3 pullVec = _activeCoin.transform.position - worldPosition;
                pullVec.y = 0f;
                float pull = pullVec.magnitude;
                pullRatio = Mathf.InverseLerp(
                    _activeCoin.MinPullDistance,
                    _activeCoin.MaxPullDistance,
                    pull);
                // X ekseni = sağ/sol; ne kadar yataysa sideRatio o kadar büyük
                Vector3 pullFlat = new Vector3(pullVec.x, 0f, pullVec.z);
                if (pullFlat.sqrMagnitude > 0.0001f)
                    sideRatio = Mathf.Abs(pullFlat.normalized.x);
            }
        }
        else if (releasedThisFrame && _activeCoin != null)
        {
            CoinDragController releasedCoin = _activeCoin;
            CoinIdentity identity = releasedCoin.GetComponent<CoinIdentity>();

            releasedCoin.ReleaseAim();
            GateIndicator.Instance?.Hide();

            if (identity != null && releasedCoin.IsSliding)
            {
                if (GameRulesManager.Instance != null)
                {
                    GameRulesManager.Instance.OnShotReleased(identity);
                }
            }

            _activeCoin = null;
        }

        _cameraZoom?.SetDragState(pullRatio, sideRatio);
    }

    void LateUpdate()
    {
        if (_activeCoin != null)
        {
            return;
        }

        GateIndicator indicator = GateIndicator.Instance;
        if (indicator != null && indicator.IsVisible)
        {
            indicator.Hide();
        }
    }

    static bool TryReadPointer(
        out Vector2 screenPosition,
        out bool isPressed,
        out bool pressedThisFrame,
        out bool releasedThisFrame,
        out bool isFromTouch)
    {
        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null &&
            (touchscreen.primaryTouch.press.isPressed || touchscreen.primaryTouch.press.wasReleasedThisFrame))
        {
            var touch = touchscreen.primaryTouch;
            screenPosition = touch.position.ReadValue();
            isPressed = touch.press.isPressed;
            pressedThisFrame = touch.press.wasPressedThisFrame;
            releasedThisFrame = touch.press.wasReleasedThisFrame;
            isFromTouch = true;
            return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            screenPosition = mouse.position.ReadValue();
            isPressed = mouse.leftButton.isPressed;
            pressedThisFrame = mouse.leftButton.wasPressedThisFrame;
            releasedThisFrame = mouse.leftButton.wasReleasedThisFrame;
            isFromTouch = false;
            return true;
        }

        screenPosition = default;
        isPressed = false;
        pressedThisFrame = false;
        releasedThisFrame = false;
        isFromTouch = false;
        return false;
    }

    void TryBeginAim(Vector2 screenPosition, bool isFromTouch)
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
        if (!TryPickCoin(ray, screenPosition, isFromTouch, out CoinDragController coin))
        {
            return;
        }

        _activeCoin = coin;
        _activeCoin.BeginAim();

        CoinIdentity identity = _activeCoin.GetComponent<CoinIdentity>();
        TryShowGateIndicator(identity, _activeCoin);

        if (TryGetTablePosition(screenPosition, out Vector3 worldPosition))
        {
            _activeCoin.UpdateAim(worldPosition);
        }
    }

    bool TryPickCoin(Ray ray, Vector2 screenPosition, bool isFromTouch, out CoinDragController coin)
    {
        if (TryPhysicsPick(ray, 0f, out coin))
        {
            return true;
        }

        if (!isFromTouch || _touchPickRadius <= 0f)
        {
            coin = null;
            return false;
        }

        if (TryPhysicsPick(ray, _touchPickRadius, out coin))
        {
            return true;
        }

        return TryPickNearestOnTable(screenPosition, out coin);
    }

    bool TryPhysicsPick(Ray ray, float sphereRadius, out CoinDragController selectedCoin)
    {
        int hitCount = sphereRadius > 0.0001f
            ? Physics.SphereCastNonAlloc(ray, sphereRadius, _raycastHits, _maxPickDistance)
            : Physics.RaycastNonAlloc(ray, _raycastHits, _maxPickDistance);

        float bestDistance = float.MaxValue;
        selectedCoin = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _raycastHits[i];
            if (hit.distance >= bestDistance)
            {
                continue;
            }

            CoinDragController candidate = hit.collider.GetComponentInParent<CoinDragController>();
            if (!IsSelectablePlayerCoin(candidate))
            {
                continue;
            }

            bestDistance = hit.distance;
            selectedCoin = candidate;
        }

        return selectedCoin != null;
    }

    bool TryPickNearestOnTable(Vector2 screenPosition, out CoinDragController selectedCoin)
    {
        selectedCoin = null;

        if (!TryGetTablePosition(screenPosition, out Vector3 tapPosition))
        {
            return false;
        }

        _coinBuffer ??= new CoinDragController[16];
        int coinCount = FillSelectablePlayerCoins(_coinBuffer);
        if (coinCount == 0)
        {
            return false;
        }

        float radiusSq = _touchPickRadius * _touchPickRadius;
        float bestDistanceSq = float.MaxValue;

        for (int i = 0; i < coinCount; i++)
        {
            CoinDragController candidate = _coinBuffer[i];
            Vector3 coinPosition = candidate.transform.position;
            float deltaX = coinPosition.x - tapPosition.x;
            float deltaZ = coinPosition.z - tapPosition.z;
            float distanceSq = deltaX * deltaX + deltaZ * deltaZ;
            if (distanceSq > radiusSq || distanceSq >= bestDistanceSq)
            {
                continue;
            }

            bestDistanceSq = distanceSq;
            selectedCoin = candidate;
        }

        return selectedCoin != null;
    }

    static int FillSelectablePlayerCoins(CoinDragController[] buffer)
    {
        CoinDragController[] coins = Object.FindObjectsByType<CoinDragController>(FindObjectsSortMode.None);
        int count = 0;

        for (int i = 0; i < coins.Length && count < buffer.Length; i++)
        {
            if (!IsSelectablePlayerCoin(coins[i]))
            {
                continue;
            }

            buffer[count++] = coins[i];
        }

        return count;
    }

    static bool IsSelectablePlayerCoin(CoinDragController coin)
    {
        if (coin == null || coin.IsAiming || coin.IsSliding)
        {
            return false;
        }

        CoinIdentity identity = coin.GetComponent<CoinIdentity>();
        if (identity == null || identity.Team != CoinTeam.Player || identity.IsPassive)
        {
            return false;
        }

        return GameRulesManager.Instance == null || GameRulesManager.Instance.CanSelectCoin(identity);
    }

    void TryShowGateIndicator(CoinIdentity shooter, CoinDragController shooterController)
    {
        GateIndicator indicator = GateIndicator.Instance;
        GameRulesManager rules = GameRulesManager.Instance;
        if (indicator == null || rules == null)
        {
            return;
        }

        if (!rules.ShouldShowGateIndicatorForNextShot)
        {
            return;
        }

        if (!rules.TryGetGateCoins(shooter, out CoinIdentity gateA, out CoinIdentity gateB))
        {
            return;
        }

        CoinGateIndicatorSettings settings = CoinGateIndicatorSettings.Resolve(shooterController);
        indicator.Show(gateA, gateB, settings, animate: true);
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
