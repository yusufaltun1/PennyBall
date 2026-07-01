using UnityEngine;

[DisallowMultipleComponent]
public class CoinAimIndicatorSettings : MonoBehaviour
{
    [Header("Oklar")]
    [SerializeField] Texture2D _arrowTexture;
    [SerializeField] float _arrowWidth = 0.055f;
    [Tooltip("Oklar arası boşluk. Ok uzunluğu texture en-boy oranından otomatik hesaplanır.")]
    [SerializeField] float _arrowGap = 0.02f;
    [Tooltip("Texture içinde ok grafiği tüm alanı doldurmuyorsa ince ayar (1 = tam texture).")]
    [SerializeField] [Min(0.05f)] float _arrowLengthMultiplier = 1f;
    [SerializeField] float _animationSpeed = 2.5f;
    [SerializeField] bool _flipArrowDirection;
    [Tooltip("Ok akış yönünü ters çevirir.")]
    [SerializeField] bool _reverseArrowAnimation;

    [Header("Glow Çizgisi")]
    [SerializeField] float _glowLineWidth = 0.035f;
    [SerializeField] float _glowWidthMultiplier = 1f;
    [SerializeField] [Range(0f, 1f)] float _glowAlpha = 0.35f;

    [Header("Genel")]
    [SerializeField] float _lineHeightOffset = 0.003f;

    [Header("Renkler")]
    [SerializeField] Color _weakPowerColor = new(0f, 0.9f, 1f, 1f);
    [SerializeField] Color _maxPowerColor = new(1f, 0.96f, 0f, 0.95f);
    [SerializeField] [Range(0f, 1f)] float _endAlphaFactor = 0.85f;
    [SerializeField] [Range(0f, 1f)] float _bounceAlphaFactor = 0.55f;

    [Header("Kaynak Glow")]
    [SerializeField] float _originGlowRadius = 0.035f;
    [SerializeField] float _originGlowWidth = 0.012f;
    [SerializeField] [Range(0f, 1f)] float _originGlowAlpha = 0.55f;

    [Header("Hedef")]
    [SerializeField] float _targetFillRadius = 0.028f;
    [SerializeField] float _targetRing1Radius = 0.042f;
    [SerializeField] float _targetRing2Radius = 0.058f;
    [SerializeField] float _targetRingWidth = 0.004f;
    [SerializeField] [Range(0f, 1f)] float _targetAlpha = 0.95f;
    [SerializeField] [Min(8)] int _ringSegments = 32;

    public Texture2D ArrowTexture => _arrowTexture;
    public float ArrowWidth => _arrowWidth;
    public float ArrowGap => _arrowGap;
    public float ArrowLengthMultiplier => _arrowLengthMultiplier;
    public float AnimationSpeed => _animationSpeed;
    public bool FlipArrowDirection => _flipArrowDirection;
    public bool ReverseArrowAnimation => _reverseArrowAnimation;

    public float GetArrowLength()
    {
        float aspect = 1f;
        if (_arrowTexture != null && _arrowTexture.height > 0)
        {
            aspect = (float)_arrowTexture.width / _arrowTexture.height;
        }

        return _arrowWidth * aspect * _arrowLengthMultiplier;
    }

    public float GetArrowStep()
    {
        return GetArrowLength() + _arrowGap;
    }

    public float GlowLineWidth => _glowLineWidth;
    public float GlowWidthMultiplier => _glowWidthMultiplier;
    public float GlowAlpha => _glowAlpha;
    public float LineHeightOffset => _lineHeightOffset;
    public Color WeakPowerColor => _weakPowerColor;
    public Color MaxPowerColor => _maxPowerColor;
    public float EndAlphaFactor => _endAlphaFactor;
    public float BounceAlphaFactor => _bounceAlphaFactor;
    public float OriginGlowRadius => _originGlowRadius;
    public float OriginGlowWidth => _originGlowWidth;
    public float OriginGlowAlpha => _originGlowAlpha;
    public float TargetFillRadius => _targetFillRadius;
    public float TargetRing1Radius => _targetRing1Radius;
    public float TargetRing2Radius => _targetRing2Radius;
    public float TargetRingWidth => _targetRingWidth;
    public float TargetAlpha => _targetAlpha;
    public int RingSegments => _ringSegments;

    public static CoinAimIndicatorSettings Resolve(Component coinRoot)
    {
        if (coinRoot == null)
        {
            return null;
        }

        CoinAimIndicatorSettings settings = coinRoot.GetComponent<CoinAimIndicatorSettings>();
        if (settings != null)
        {
            return settings;
        }

        return coinRoot.GetComponentInChildren<CoinAimIndicatorSettings>();
    }
}
