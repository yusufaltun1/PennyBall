using System;
using System.Collections;
using UnityEngine;

public class MatchIntroCameraFlythrough : MonoBehaviour
{
    public static bool IsActive { get; private set; }
    public static event Action Finished;

    [SerializeField] float _duration = 2.5f;
    [SerializeField] float _startBackOffset = 1.8f;
    [SerializeField] float _startUpOffset = 2f;
    [SerializeField] float _startSideOffset = 0.45f;
    [SerializeField] [Range(0f, 1f)] float _startLookBlend = 0.35f;

    CameraAimZoom _aimZoom;
    Vector3 _homePosition;
    Quaternion _homeRotation;
    Vector3 _startPosition;
    Quaternion _startRotation;
    Coroutine _routine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStaticState()
    {
        IsActive = false;
        Finished = null;
    }

    void Awake()
    {
        IsActive = true;
        _aimZoom = GetComponent<CameraAimZoom>();
        if (_aimZoom != null)
        {
            _aimZoom.enabled = false;
        }

        _homePosition = transform.position;
        _homeRotation = transform.rotation;
        _startPosition = ComputeStartPosition();
        _startRotation = ComputeStartRotation();
        transform.SetPositionAndRotation(_startPosition, _startRotation);
    }

    void Start()
    {
        _routine = StartCoroutine(PlayFlythrough());
    }

    void OnDestroy()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
        }

        if (IsActive)
        {
            IsActive = false;
            Finished?.Invoke();
        }
    }

    Vector3 ComputeStartPosition()
    {
        return _homePosition
               - transform.forward * _startBackOffset
               + transform.up * _startUpOffset
               + transform.right * _startSideOffset;
    }

    Quaternion ComputeStartRotation()
    {
        Vector3 lookDirection = _homePosition - _startPosition;
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            return _homeRotation;
        }

        Quaternion lookAtHome = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        return Quaternion.Slerp(_homeRotation, lookAtHome, _startLookBlend);
    }

    IEnumerator PlayFlythrough()
    {
        float duration = Mathf.Max(0.01f, _duration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = SmoothStep01(elapsed / duration);
            transform.position = Vector3.Lerp(_startPosition, _homePosition, t);
            transform.rotation = Quaternion.Slerp(_startRotation, _homeRotation, t);
            yield return null;
        }

        transform.SetPositionAndRotation(_homePosition, _homeRotation);

        if (_aimZoom != null)
        {
            _aimZoom.enabled = true;
        }

        IsActive = false;
        Finished?.Invoke();
        _routine = null;
    }

    static float SmoothStep01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t);
    }
}
