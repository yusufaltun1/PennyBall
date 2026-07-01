using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class PlayerGoalEffectController : MonoBehaviour
{
    public static PlayerGoalEffectController Instance { get; private set; }

    [Header("References")]
    [SerializeField] RectTransform _goalImg;
    [SerializeField] RectTransform _particleBurstRoot;

    [Header("Goal Image")]
    [SerializeField] float _goalScaleAnimationDuration = 1f;
    [SerializeField] float _goalStartScale = 0.05f;

    [Header("Burst Particles")]
    [SerializeField] int _particleCount = 28;
    [SerializeField, Range(0.3f, 1f)] float _burstCoverage = 0.92f;
    [SerializeField] Vector2 _particleSizeRange = new(18f, 42f);
    [SerializeField] float _fallDuration = 1.35f;
    [SerializeField] float _gravity = 1450f;
    [SerializeField] float _horizontalDrift = 180f;
    [SerializeField] Color[] _particleColors =
    {
        new(1f, 0.84f, 0.1f, 1f),
        new(0.2f, 0.95f, 0.45f, 1f),
        new(1f, 0.45f, 0.1f, 1f),
        new(0.35f, 0.75f, 1f, 1f),
        new(1f, 0.3f, 0.55f, 1f),
    };

    static readonly Dictionary<ParticleShape, Sprite> ShapeSprites = new();

    Coroutine _playRoutine;
    MonoBehaviour _routineHost;
    Vector3 _goalTargetScale = Vector3.one;
    readonly List<ParticlePiece> _activeParticles = new();

    enum ParticleShape
    {
        Circle,
        Square,
        Triangle,
        Diamond,
        Pentagon,
    }

    sealed class ParticlePiece
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 BurstOffset;
        public Vector2 Velocity;
        public float SpinSpeed;
        public float BaseSize;
    }

    public static PlayerGoalEffectController EnsureInstance()
    {
        if (Instance != null)
        {
            return Instance;
        }

        return FindFirstObjectByType<PlayerGoalEffectController>(FindObjectsInactive.Include);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        CacheGoalTargetScale();
    }

    void OnDestroy()
    {
        StopActiveRoutine();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    MonoBehaviour ResolveRoutineHost()
    {
        if (GameRulesManager.Instance != null)
        {
            return GameRulesManager.Instance;
        }

        return this;
    }

    void StopActiveRoutine()
    {
        if (_playRoutine == null)
        {
            return;
        }

        MonoBehaviour host = _routineHost != null ? _routineHost : ResolveRoutineHost();
        if (host != null)
        {
            host.StopCoroutine(_playRoutine);
        }

        _playRoutine = null;
        _routineHost = null;
    }

    void CacheGoalTargetScale()
    {
        if (_goalImg != null)
        {
            _goalTargetScale = _goalImg.localScale;
            if (_goalTargetScale.sqrMagnitude < 0.0001f)
            {
                _goalTargetScale = Vector3.one;
            }
        }
    }

    public bool CanPlay()
    {
        return _goalImg != null;
    }

    public void Play(Action onComplete)
    {
        if (_goalImg == null)
        {
            onComplete?.Invoke();
            return;
        }

        StopActiveRoutine();
        CacheGoalTargetScale();
        ClearParticles();

        if (_particleBurstRoot == null)
        {
            _particleBurstRoot = transform as RectTransform;
        }

        EnsureParticleArea();
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _goalImg.localScale = _goalTargetScale * _goalStartScale;

        _routineHost = ResolveRoutineHost();
        if (_routineHost == null)
        {
            gameObject.SetActive(false);
            onComplete?.Invoke();
            return;
        }

        _playRoutine = _routineHost.StartCoroutine(PlayRoutine(onComplete));
    }

    IEnumerator PlayRoutine(Action onComplete)
    {
        SpawnBurstParticles();

        float elapsed = 0f;
        while (elapsed < _goalScaleAnimationDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = _goalScaleAnimationDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / _goalScaleAnimationDuration);
            float eased = EaseOutBounce(normalized);

            _goalImg.localScale = Vector3.LerpUnclamped(
                _goalTargetScale * _goalStartScale,
                _goalTargetScale,
                eased);

            UpdateBurstParticles(eased);
            yield return null;
        }

        _goalImg.localScale = _goalTargetScale;
        UpdateBurstParticles(1f);

        yield return FallParticlesRoutine();

        ClearParticles();
        gameObject.SetActive(false);
        _playRoutine = null;
        _routineHost = null;
        onComplete?.Invoke();
    }

    void EnsureParticleArea()
    {
        RectTransform container = transform as RectTransform;
        if (container == null)
        {
            return;
        }

        if (_particleBurstRoot == null)
        {
            _particleBurstRoot = container;
        }

        _particleBurstRoot.anchorMin = Vector2.zero;
        _particleBurstRoot.anchorMax = Vector2.one;
        _particleBurstRoot.offsetMin = Vector2.zero;
        _particleBurstRoot.offsetMax = Vector2.zero;
        _particleBurstRoot.pivot = new Vector2(0.5f, 0.5f);
        _particleBurstRoot.anchoredPosition = Vector2.zero;

        if (GetComponent<RectMask2D>() == null)
        {
            gameObject.AddComponent<RectMask2D>();
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(container);
    }

    Vector2 GetBurstExtents()
    {
        RectTransform root = _particleBurstRoot != null ? _particleBurstRoot : transform as RectTransform;
        if (root == null)
        {
            return Vector2.one * 220f;
        }

        Rect rect = root.rect;
        float coverage = Mathf.Clamp01(_burstCoverage);
        return new Vector2(rect.width * 0.5f * coverage, rect.height * 0.5f * coverage);
    }

    void SpawnBurstParticles()
    {
        RectTransform root = _particleBurstRoot != null ? _particleBurstRoot : transform as RectTransform;
        if (root == null)
        {
            return;
        }

        Vector2 burstExtents = GetBurstExtents();

        for (int i = 0; i < _particleCount; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float spread = Mathf.Sqrt(UnityEngine.Random.Range(0.12f, 1f));
            Vector2 burstOffset = new Vector2(
                Mathf.Cos(angle) * burstExtents.x * spread,
                Mathf.Sin(angle) * burstExtents.y * spread);
            float size = UnityEngine.Random.Range(_particleSizeRange.x, _particleSizeRange.y);
            burstOffset = ClampOffsetToContainer(burstOffset, burstExtents, size);
            ParticleShape shape = (ParticleShape)UnityEngine.Random.Range(0, Enum.GetValues(typeof(ParticleShape)).Length);
            Color color = _particleColors.Length > 0
                ? _particleColors[UnityEngine.Random.Range(0, _particleColors.Length)]
                : Color.white;

            var pieceObject = new GameObject($"BurstPiece_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pieceObject.transform.SetParent(root, false);

            var rect = pieceObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(size, size);
            rect.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));

            Image image = pieceObject.GetComponent<Image>();
            image.raycastTarget = false;
            image.sprite = GetShapeSprite(shape);
            image.color = color;

            _activeParticles.Add(new ParticlePiece
            {
                Rect = rect,
                Image = image,
                BurstOffset = burstOffset,
                BaseSize = size,
                SpinSpeed = UnityEngine.Random.Range(-240f, 240f),
            });
        }
    }

    static Vector2 ClampOffsetToContainer(Vector2 offset, Vector2 extents, float particleSize)
    {
        float margin = particleSize * 0.5f;
        return new Vector2(
            Mathf.Clamp(offset.x, -extents.x + margin, extents.x - margin),
            Mathf.Clamp(offset.y, -extents.y + margin, extents.y - margin));
    }

    void UpdateBurstParticles(float eased)
    {
        for (int i = 0; i < _activeParticles.Count; i++)
        {
            ParticlePiece piece = _activeParticles[i];
            if (piece.Rect == null)
            {
                continue;
            }

            piece.Rect.anchoredPosition = piece.BurstOffset * eased;
            float scale = EaseOutBounce(eased);
            piece.Rect.localScale = Vector3.one * scale;
            piece.Rect.Rotate(0f, 0f, piece.SpinSpeed * Time.unscaledDeltaTime);
        }
    }

    IEnumerator FallParticlesRoutine()
    {
        if (_activeParticles.Count == 0)
        {
            yield break;
        }

        RectTransform root = _particleBurstRoot != null ? _particleBurstRoot : transform as RectTransform;
        float fallLimit = root != null
            ? -root.rect.height * 0.5f - _particleSizeRange.y
            : -1400f;

        for (int i = 0; i < _activeParticles.Count; i++)
        {
            ParticlePiece piece = _activeParticles[i];
            if (piece.Rect == null)
            {
                continue;
            }

            piece.Velocity = new Vector2(
                UnityEngine.Random.Range(-_horizontalDrift, _horizontalDrift),
                UnityEngine.Random.Range(-80f, 120f));
        }

        float elapsed = 0f;
        bool anyVisible = true;
        while (elapsed < _fallDuration && anyVisible)
        {
            elapsed += Time.unscaledDeltaTime;
            anyVisible = false;

            for (int i = 0; i < _activeParticles.Count; i++)
            {
                ParticlePiece piece = _activeParticles[i];
                if (piece.Rect == null)
                {
                    continue;
                }

                piece.Velocity.y -= _gravity * Time.unscaledDeltaTime;
                Vector2 position = piece.Rect.anchoredPosition + piece.Velocity * Time.unscaledDeltaTime;
                piece.Rect.anchoredPosition = position;
                piece.Rect.Rotate(0f, 0f, piece.SpinSpeed * Time.unscaledDeltaTime);

                Color color = piece.Image.color;
                color.a = Mathf.Lerp(color.a, 0f, Time.unscaledDeltaTime * 1.4f);
                piece.Image.color = color;

                if (position.y > fallLimit && color.a > 0.02f)
                {
                    anyVisible = true;
                }
            }

            yield return null;
        }
    }

    void ClearParticles()
    {
        for (int i = 0; i < _activeParticles.Count; i++)
        {
            ParticlePiece piece = _activeParticles[i];
            if (piece.Rect != null)
            {
                Destroy(piece.Rect.gameObject);
            }
        }

        _activeParticles.Clear();
    }

    static Sprite GetShapeSprite(ParticleShape shape)
    {
        if (ShapeSprites.TryGetValue(shape, out Sprite cached))
        {
            return cached;
        }

        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size * 0.5f, size * 0.5f);
        float radius = size * 0.42f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                bool inside = shape switch
                {
                    ParticleShape.Circle => Vector2.Distance(point, center) <= radius,
                    ParticleShape.Square => Mathf.Abs(point.x - center.x) <= radius && Mathf.Abs(point.y - center.y) <= radius,
                    ParticleShape.Triangle => IsInsideTriangle(point, center + Vector2.up * radius, center + new Vector2(-radius, -radius * 0.75f), center + new Vector2(radius, -radius * 0.75f)),
                    ParticleShape.Diamond => Mathf.Abs(point.x - center.x) / radius + Mathf.Abs(point.y - center.y) / radius <= 1f,
                    ParticleShape.Pentagon => IsInsidePolygon(point, center, radius, 5, -Mathf.PI * 0.5f),
                    _ => false,
                };

                pixels[y * size + x] = inside ? Color.white : Color.clear;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        cached = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
        ShapeSprites[shape] = cached;
        return cached;
    }

    static bool IsInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float Sign(Vector2 p1, Vector2 p2, Vector2 p3) =>
            (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);

        float d1 = Sign(point, a, b);
        float d2 = Sign(point, b, c);
        float d3 = Sign(point, c, a);
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    static bool IsInsidePolygon(Vector2 point, Vector2 center, float radius, int sides, float rotation)
    {
        Vector2 first = center + new Vector2(Mathf.Cos(rotation), Mathf.Sin(rotation)) * radius;
        Vector2 previous = first;

        for (int i = 1; i <= sides; i++)
        {
            float angle = rotation + i * Mathf.PI * 2f / sides;
            Vector2 next = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            if (IsInsideTriangle(point, center, previous, next))
            {
                return true;
            }

            previous = next;
        }

        return false;
    }

    static float EaseOutBounce(float t)
    {
        const float n1 = 7.5625f;
        const float d1 = 2.75f;

        if (t < 1f / d1)
        {
            return n1 * t * t;
        }

        if (t < 2f / d1)
        {
            t -= 1.5f / d1;
            return n1 * t * t + 0.75f;
        }

        if (t < 2.5f / d1)
        {
            t -= 2.25f / d1;
            return n1 * t * t + 0.9375f;
        }

        t -= 2.625f / d1;
        return n1 * t * t + 0.984375f;
    }
}
