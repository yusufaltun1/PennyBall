using UnityEngine;

[System.Serializable]
public class CoinHitDustSettings
{
    [SerializeField] bool _enabled = true;

    [Header("Yerleşim")]
    [SerializeField] float _heightOffset = 0.012f;

    [Header("Parçacık — Ana")]
    [SerializeField] float _duration = 0.45f;
    [SerializeField] Vector2 _lifetimeRange = new(0.25f, 0.55f);
    [SerializeField] Vector2 _speedRange = new(0.15f, 0.55f);
    [SerializeField] Vector2 _sizeRange = new(0.018f, 0.045f);
    [SerializeField] Vector2 _rotationRange = new(0f, 360f);
    [SerializeField] float _gravityModifier = 0.05f;
    [SerializeField] int _maxParticles = 48;
    [SerializeField] Color _startColor = new(0.72f, 0.64f, 0.52f, 0.35f);

    [Header("Parçacık — Şekil")]
    [SerializeField] ParticleSystemShapeType _shapeType = ParticleSystemShapeType.Circle;
    [Tooltip("Circle / Sphere için yarıçap")]
    [SerializeField] float _shapeRadius = 0.025f;
    [Tooltip("Box / Cone vb. için boyut. Y değerini küçük tut (masa üstü patlama)")]
    [SerializeField] Vector3 _shapeScale = new(0.05f, 0.01f, 0.05f);
    [Tooltip("Circle için genelde (90,0,0); Box için (0,0,0)")]
    [SerializeField] Vector3 _shapeRotation = new(90f, 0f, 0f);

    [Header("Patlama")]
    [SerializeField] int _burstCountMin = 4;
    [SerializeField] int _burstCountMax = 14;

    [Header("Ömür Boyu")]
    [SerializeField] Gradient _colorOverLifetime = CreateDefaultColorGradient();
    [SerializeField] AnimationCurve _sizeOverLifetime = CreateDefaultSizeCurve();

    [Header("Render")]
    [SerializeField] ParticleSystemRenderMode _renderMode = ParticleSystemRenderMode.Billboard;
    [SerializeField] bool _additiveBlend;
    [SerializeField] Material _customMaterial;

    public bool Enabled => _enabled;
    public float HeightOffset => _heightOffset;
    public float Duration => _duration;
    public Vector2 LifetimeRange => _lifetimeRange;
    public Vector2 SpeedRange => _speedRange;
    public Vector2 SizeRange => _sizeRange;
    public Vector2 RotationRange => _rotationRange;
    public float GravityModifier => _gravityModifier;
    public int MaxParticles => _maxParticles;
    public Color StartColor => _startColor;
    public ParticleSystemShapeType ShapeType => _shapeType;
    public float ShapeRadius => _shapeRadius;
    public Vector3 ShapeScale => _shapeScale;
    public Vector3 ShapeRotation => _shapeRotation;
    public int BurstCountMin => _burstCountMin;
    public int BurstCountMax => _burstCountMax;
    public Gradient ColorOverLifetime => _colorOverLifetime;
    public AnimationCurve SizeOverLifetime => _sizeOverLifetime;
    public ParticleSystemRenderMode RenderMode => _renderMode;
    public bool AdditiveBlend => _additiveBlend;
    public Material CustomMaterial => _customMaterial;

    static Gradient CreateDefaultColorGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.78f, 0.7f, 0.55f), 0f),
                new GradientColorKey(new Color(0.62f, 0.56f, 0.46f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.38f, 0f),
                new GradientAlphaKey(0.22f, 0.45f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    static AnimationCurve CreateDefaultSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.6f),
            new Keyframe(0.35f, 1f),
            new Keyframe(1f, 0.15f));
    }
}
