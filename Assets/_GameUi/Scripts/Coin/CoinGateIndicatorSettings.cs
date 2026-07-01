using UnityEngine;

[DisallowMultipleComponent]
public class CoinGateIndicatorSettings : MonoBehaviour
{
    [SerializeField] float _lineHeightOffset = 0.004f;
    [SerializeField] float _lineWidth = 0.05f;
    [SerializeField] float _dashLength = 0.055f;
    [SerializeField] float _dashGap = 0.04f;
    [SerializeField] float _animationSpeed = 1.2f;
    [SerializeField] float _glowWidthMultiplier = 3.2f;
    [SerializeField] float _glowAlpha = 0.28f;
    [SerializeField] float _glowPulseSpeed = 2.5f;
    [SerializeField] [Range(0f, 0.35f)] float _glowPulseAmount = 0.12f;
    [SerializeField] [Min(2)] int _showFromShotNumber = 2;

    public float LineHeightOffset => _lineHeightOffset;
    public float LineWidth => _lineWidth;
    public float DashLength => _dashLength;
    public float DashGap => _dashGap;
    public float AnimationSpeed => _animationSpeed;
    public float GlowWidthMultiplier => _glowWidthMultiplier;
    public float GlowAlpha => _glowAlpha;
    public float GlowPulseSpeed => _glowPulseSpeed;
    public float GlowPulseAmount => _glowPulseAmount;
    public int ShowFromShotNumber => _showFromShotNumber;

    public bool ShouldShowForShot(int currentShotNumber)
    {
        return currentShotNumber > GameRulesManager.OpeningShotCount;
    }

    public static CoinGateIndicatorSettings Resolve(Component coinRoot)
    {
        if (coinRoot == null)
        {
            return null;
        }

        CoinGateIndicatorSettings settings = coinRoot.GetComponent<CoinGateIndicatorSettings>();
        if (settings != null)
        {
            return settings;
        }

        return coinRoot.GetComponentInChildren<CoinGateIndicatorSettings>();
    }
}
