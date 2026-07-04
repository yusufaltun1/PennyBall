using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class UIParticleBurstSettings
{
    public int particleCount = 28;
    [Range(0.3f, 1f)] public float burstCoverage = 0.92f;
    public Vector2 particleSizeRange = new(18f, 42f);
    public float expandDuration = 1.5f;
    public float fallDuration = 1.35f;
    public float gravity = 1450f;
    public float horizontalDrift = 180f;
    public Color[] particleColors =
    {
        new(1f, 0.84f, 0.1f, 1f),
        new(0.2f, 0.95f, 0.45f, 1f),
        new(1f, 0.45f, 0.1f, 1f),
        new(0.35f, 0.75f, 1f, 1f),
        new(1f, 0.3f, 0.55f, 1f),
    };
}

public class UIParticleBurstPlayer
{
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
    }

    static readonly Dictionary<ParticleShape, Sprite> ShapeSprites = new();

    readonly List<ParticlePiece> _activeParticles = new();
    RectTransform _burstRoot;
    UIParticleBurstSettings _settings;

    public void Prepare(RectTransform burstRoot, UIParticleBurstSettings settings)
    {
        _burstRoot = burstRoot;
        _settings = settings ?? new UIParticleBurstSettings();
        Clear();
        EnsureParticleArea();
    }

    public void Spawn()
    {
        if (_burstRoot == null)
        {
            return;
        }

        Vector2 burstExtents = GetBurstExtents();

        for (int i = 0; i < _settings.particleCount; i++)
        {
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            float spread = Mathf.Sqrt(UnityEngine.Random.Range(0.12f, 1f));
            Vector2 burstOffset = new Vector2(
                Mathf.Cos(angle) * burstExtents.x * spread,
                Mathf.Sin(angle) * burstExtents.y * spread);
            float size = UnityEngine.Random.Range(_settings.particleSizeRange.x, _settings.particleSizeRange.y);
            burstOffset = ClampOffsetToContainer(burstOffset, burstExtents, size);
            ParticleShape shape = (ParticleShape)UnityEngine.Random.Range(0, Enum.GetValues(typeof(ParticleShape)).Length);
            Color color = _settings.particleColors.Length > 0
                ? _settings.particleColors[UnityEngine.Random.Range(0, _settings.particleColors.Length)]
                : Color.white;

            var pieceObject = new GameObject($"BurstPiece_{i}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            pieceObject.transform.SetParent(_burstRoot, false);

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
                SpinSpeed = UnityEngine.Random.Range(-240f, 240f),
            });
        }
    }

    public void UpdateExpand(float eased)
    {
        float deltaTime = Time.unscaledDeltaTime;

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
            piece.Rect.Rotate(0f, 0f, piece.SpinSpeed * deltaTime);
        }
    }

    public IEnumerator PlayRoutine()
    {
        Spawn();

        float expandDuration = Mathf.Max(0.01f, _settings.expandDuration);
        float elapsed = 0f;

        while (elapsed < expandDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            UpdateExpand(Mathf.Clamp01(elapsed / expandDuration));
            yield return null;
        }

        UpdateExpand(1f);
        yield return FallAndClear();
    }

    public IEnumerator FallAndClear()
    {
        yield return FallRoutine();
        Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < _activeParticles.Count; i++)
        {
            ParticlePiece piece = _activeParticles[i];
            if (piece.Rect != null)
            {
                UnityEngine.Object.Destroy(piece.Rect.gameObject);
            }
        }

        _activeParticles.Clear();
    }

    void EnsureParticleArea()
    {
        if (_burstRoot == null)
        {
            return;
        }

        _burstRoot.anchorMin = Vector2.zero;
        _burstRoot.anchorMax = Vector2.one;
        _burstRoot.offsetMin = Vector2.zero;
        _burstRoot.offsetMax = Vector2.zero;
        _burstRoot.pivot = new Vector2(0.5f, 0.5f);
        _burstRoot.anchoredPosition = Vector2.zero;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(_burstRoot);
    }

    Vector2 GetBurstExtents()
    {
        if (_burstRoot == null)
        {
            return Vector2.one * 220f;
        }

        Rect rect = _burstRoot.rect;
        float coverage = Mathf.Clamp01(_settings.burstCoverage);
        return new Vector2(rect.width * 0.5f * coverage, rect.height * 0.5f * coverage);
    }

    IEnumerator FallRoutine()
    {
        if (_activeParticles.Count == 0)
        {
            yield break;
        }

        float fallLimit = _burstRoot != null
            ? -_burstRoot.rect.height * 0.5f - _settings.particleSizeRange.y
            : -1400f;

        for (int i = 0; i < _activeParticles.Count; i++)
        {
            ParticlePiece piece = _activeParticles[i];
            if (piece.Rect == null)
            {
                continue;
            }

            piece.Velocity = new Vector2(
                UnityEngine.Random.Range(-_settings.horizontalDrift, _settings.horizontalDrift),
                UnityEngine.Random.Range(-80f, 120f));
        }

        float elapsed = 0f;
        bool anyVisible = true;

        while (elapsed < _settings.fallDuration && anyVisible)
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

                piece.Velocity.y -= _settings.gravity * Time.unscaledDeltaTime;
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

    static Vector2 ClampOffsetToContainer(Vector2 offset, Vector2 extents, float particleSize)
    {
        float margin = particleSize * 0.5f;
        return new Vector2(
            Mathf.Clamp(offset.x, -extents.x + margin, extents.x - margin),
            Mathf.Clamp(offset.y, -extents.y + margin, extents.y - margin));
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
