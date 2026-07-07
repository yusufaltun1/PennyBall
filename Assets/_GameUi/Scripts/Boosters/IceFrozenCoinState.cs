using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class IceFrozenCoinState : MonoBehaviour
{
    Rigidbody _rigidbody;
    CoinDragController _dragController;
    IceCoinFrostVisual _frostVisual;

    bool _isFrozen;
    float _unfreezeTime;
    Vector3 _frozenPosition;
    Quaternion _frozenRotation;

    public bool IsFrozen => _isFrozen;

    public void BeginFreeze(float durationSeconds, IceCoinFrostVisual.Settings visualSettings)
    {
        if (_rigidbody == null)
        {
            _rigidbody = GetComponent<Rigidbody>();
        }

        if (_dragController == null)
        {
            _dragController = GetComponent<CoinDragController>();
        }

        if (_rigidbody == null)
        {
            return;
        }

        _isFrozen = true;
        _unfreezeTime = Time.time + durationSeconds;
        _frozenPosition = transform.position;
        _frozenRotation = transform.rotation;

        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        _rigidbody.isKinematic = true;

        EnsureFrostVisual();
        _frostVisual.Apply(visualSettings);
    }

    public void EndFreeze()
    {
        if (!_isFrozen)
        {
            return;
        }

        _isFrozen = false;

        if (_rigidbody != null && (_dragController == null || !_dragController.IsAiming))
        {
            _rigidbody.isKinematic = false;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
        }

        if (_frostVisual != null)
        {
            _frostVisual.Remove();
        }
    }

    void Awake()
    {
        _rigidbody = GetComponent<Rigidbody>();
        _dragController = GetComponent<CoinDragController>();
    }

    void FixedUpdate()
    {
        if (!_isFrozen)
        {
            return;
        }

        if (Time.time >= _unfreezeTime)
        {
            EndFreeze();
            return;
        }

        if (_dragController != null && _dragController.IsAiming)
        {
            _dragController.CancelAim();
        }

        if (_rigidbody == null)
        {
            return;
        }

        _rigidbody.isKinematic = true;
        _rigidbody.linearVelocity = Vector3.zero;
        _rigidbody.angularVelocity = Vector3.zero;
        transform.SetPositionAndRotation(_frozenPosition, _frozenRotation);
    }

    void EnsureFrostVisual()
    {
        if (_frostVisual == null)
        {
            _frostVisual = GetComponent<IceCoinFrostVisual>();
            if (_frostVisual == null)
            {
                _frostVisual = gameObject.AddComponent<IceCoinFrostVisual>();
            }
        }
    }
}

public static class IceOpponentFreezeUtility
{
    public static bool AnyOpponentCoinMoving()
    {
        CoinIdentity[] coins = UnityEngine.Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            CoinIdentity identity = coins[i];
            if (identity == null || identity.Team != CoinTeam.Opponent)
            {
                continue;
            }

            CoinDragController drag = identity.DragController;
            if (drag == null)
            {
                continue;
            }

            if (drag.IsAiming || drag.IsSliding)
            {
                return true;
            }
        }

        return false;
    }

    public static List<CoinIdentity> CollectOpponentCoins()
    {
        var opponents = new List<CoinIdentity>();
        CoinIdentity[] coins = UnityEngine.Object.FindObjectsByType<CoinIdentity>(FindObjectsSortMode.None);
        for (int i = 0; i < coins.Length; i++)
        {
            CoinIdentity identity = coins[i];
            if (identity != null && identity.Team == CoinTeam.Opponent)
            {
                opponents.Add(identity);
            }
        }

        return opponents;
    }

    public static bool IsOpponentReadyForIceBooster()
    {
        if (AnyOpponentCoinMoving())
        {
            return false;
        }

        OpponentBotController bot = OpponentBotController.Instance;
        if (bot != null && bot.IsResolving)
        {
            return false;
        }

        GameRulesManager rules = GameRulesManager.Instance;
        if (rules != null && rules.IsResolvingMove)
        {
            return false;
        }

        return true;
    }

    public static IEnumerator WaitUntilOpponentReadyForIceBooster()
    {
        while (!IsOpponentReadyForIceBooster())
        {
            yield return null;
        }
    }

    public static IEnumerator WaitUntilOpponentCoinsStopped()
    {
        while (AnyOpponentCoinMoving())
        {
            yield return null;
        }
    }

    public static void PauseOpponentBotForIceBooster()
    {
        OpponentBotController bot = OpponentBotController.Instance;
        if (bot != null)
        {
            bot.FreezeMatch();
        }

        CancelOpponentAims();
    }

    public static void ResumeOpponentBotAfterIceBooster()
    {
        OpponentBotController.Instance?.ResumePlayIfIdle();
    }

    static void CancelOpponentAims()
    {
        List<CoinIdentity> opponents = CollectOpponentCoins();
        for (int i = 0; i < opponents.Count; i++)
        {
            CoinIdentity identity = opponents[i];
            if (identity == null)
            {
                continue;
            }

            CoinDragController drag = identity.DragController;
            if (drag != null && drag.IsAiming)
            {
                drag.CancelAim();
            }
        }
    }

    public static void FreezeAllOpponentCoins(float durationSeconds, IceCoinFrostVisual.Settings visualSettings)
    {
        List<CoinIdentity> opponents = CollectOpponentCoins();
        for (int i = 0; i < opponents.Count; i++)
        {
            CoinIdentity identity = opponents[i];
            if (identity == null)
            {
                continue;
            }

            IceFrozenCoinState frozenState = identity.GetComponent<IceFrozenCoinState>();
            if (frozenState == null)
            {
                frozenState = identity.gameObject.AddComponent<IceFrozenCoinState>();
            }

            frozenState.BeginFreeze(durationSeconds, visualSettings);
        }
    }

    public static void UnfreezeAllOpponentCoins()
    {
        IceFrozenCoinState[] frozenStates = UnityEngine.Object.FindObjectsByType<IceFrozenCoinState>(FindObjectsSortMode.None);
        for (int i = 0; i < frozenStates.Length; i++)
        {
            if (frozenStates[i] != null)
            {
                frozenStates[i].EndFreeze();
            }
        }
    }
}
