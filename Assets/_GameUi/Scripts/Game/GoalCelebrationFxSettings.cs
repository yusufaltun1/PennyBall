using UnityEngine;

[System.Serializable]
public class GoalCelebrationBurstSettings
{
    [SerializeField] bool _enabled = true;

    [Header("Süre ve Hız")]
    [Tooltip("Efekt süresi (saniye)")]
    [SerializeField] float _duration = 2.2f;
    [Tooltip("Particle animasyon oynatma hızı")]
    [SerializeField] [Range(0.1f, 3f)] float _simulationSpeed = 1f;
    [SerializeField] Vector2 _lifetimeRange = new(1.4f, 2.6f);
    [SerializeField] Vector2 _speedRange = new(4f, 9f);

    [Header("Boyut")]
    [SerializeField] Vector2 _sizeRange = new(0.06f, 0.14f);
    [SerializeField] float _shapeRadius = 0.35f;
    [SerializeField] float _coneAngle = 38f;

    [Header("Patlama")]
    [SerializeField] int _burstMin = 120;
    [SerializeField] int _burstMax = 160;
    [SerializeField] float _burstDelay;
    [SerializeField] float _burstInterval = 0.05f;
    [SerializeField] int _maxParticles = 280;
    [SerializeField] float _gravityModifier;
    [SerializeField] bool _additive;

    public bool Enabled => _enabled;
    public float Duration => _duration;
    public float SimulationSpeed => _simulationSpeed;
    public Vector2 LifetimeRange => _lifetimeRange;
    public Vector2 SpeedRange => _speedRange;
    public Vector2 SizeRange => _sizeRange;
    public int BurstMin => _burstMin;
    public int BurstMax => _burstMax;
    public float BurstDelay => _burstDelay;
    public float BurstInterval => _burstInterval;
    public int MaxParticles => _maxParticles;
    public float GravityModifier => _gravityModifier;
    public float ShapeRadius => _shapeRadius;
    public float ConeAngle => _coneAngle;
    public bool Additive => _additive;

    public void ApplyConfettiDefaults()
    {
        _enabled = true;
        _duration = 2.2f;
        _simulationSpeed = 1f;
        _lifetimeRange = new Vector2(1.4f, 2.6f);
        _speedRange = new Vector2(4f, 9f);
        _sizeRange = new Vector2(0.06f, 0.14f);
        _burstMin = 120;
        _burstMax = 160;
        _burstDelay = 0f;
        _burstInterval = 0.05f;
        _maxParticles = 280;
        _gravityModifier = 0.85f;
        _shapeRadius = 0.35f;
        _coneAngle = 38f;
        _additive = false;
    }

    public void ApplySparksDefaults()
    {
        _enabled = true;
        _duration = 1.2f;
        _simulationSpeed = 1f;
        _lifetimeRange = new Vector2(0.25f, 0.7f);
        _speedRange = new Vector2(6f, 14f);
        _sizeRange = new Vector2(0.03f, 0.09f);
        _burstMin = 70;
        _burstMax = 90;
        _burstDelay = 0f;
        _burstInterval = 0.02f;
        _maxParticles = 160;
        _gravityModifier = 0f;
        _shapeRadius = 0.15f;
        _coneAngle = 25f;
        _additive = true;
    }

    public void ApplyStarFountainDefaults()
    {
        _enabled = true;
        _duration = 1.6f;
        _simulationSpeed = 1f;
        _lifetimeRange = new Vector2(0.5f, 1.1f);
        _speedRange = new Vector2(3f, 7f);
        _sizeRange = new Vector2(0.08f, 0.18f);
        _burstMin = 35;
        _burstMax = 45;
        _burstDelay = 0.05f;
        _burstInterval = 0.08f;
        _maxParticles = 80;
        _gravityModifier = 0.35f;
        _shapeRadius = 0.2f;
        _coneAngle = 22f;
        _additive = true;
    }

    public void ApplySmokeDefaults()
    {
        _enabled = true;
        _duration = 1.4f;
        _simulationSpeed = 1f;
        _lifetimeRange = new Vector2(0.6f, 1.2f);
        _speedRange = new Vector2(0.5f, 2f);
        _sizeRange = new Vector2(0.25f, 0.55f);
        _burstMin = 18;
        _burstMax = 24;
        _burstDelay = 0f;
        _burstInterval = 0.04f;
        _maxParticles = 40;
        _gravityModifier = 0f;
        _shapeRadius = 0.25f;
        _coneAngle = 25f;
        _additive = false;
    }
}

[System.Serializable]
public class GoalCelebrationRingSettings
{
    [SerializeField] bool _enabled = true;
    [SerializeField] float _duration = 1.1f;
    [SerializeField] [Range(0.1f, 3f)] float _simulationSpeed = 1f;
    [SerializeField] float _lifetime = 0.55f;
    [SerializeField] float _startSize = 0.5f;
    [SerializeField] float _shapeRadius = 0.2f;
    [SerializeField] AnimationCurve _sizeOverLifetime = CreateDefaultRingSizeCurve();

    public bool Enabled => _enabled;
    public float Duration => _duration;
    public float SimulationSpeed => _simulationSpeed;
    public float Lifetime => _lifetime;
    public float StartSize => _startSize;
    public float ShapeRadius => _shapeRadius;
    public AnimationCurve SizeOverLifetime => _sizeOverLifetime;

    public void ApplyDefaults()
    {
        _enabled = true;
        _duration = 1.1f;
        _simulationSpeed = 1f;
        _lifetime = 0.55f;
        _startSize = 0.5f;
        _shapeRadius = 0.2f;
        _sizeOverLifetime = CreateDefaultRingSizeCurve();
    }

    static AnimationCurve CreateDefaultRingSizeCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.4f),
            new Keyframe(0.35f, 2.8f),
            new Keyframe(1f, 4.5f));
    }
}

