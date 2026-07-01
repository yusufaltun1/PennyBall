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

    [Header("Renkler")]
    [SerializeField] Color _lineColor = new(0.506f, 0.325f, 0.796f, 1f);
    [SerializeField] Color _glowColor = new(0.506f, 0.325f, 0.796f, 1f);

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
    public Color LineColor => _lineColor;
    public Color GlowColor => _glowColor;

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
