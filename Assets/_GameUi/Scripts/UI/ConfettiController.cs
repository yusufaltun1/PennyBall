using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class ConfettiController : MonoBehaviour
{
    private enum ConfettiShape
    {
        Square,
        Circle,
        Triangle,
        Diamond,
        Star
    }

    [Header("Projectile")]
    [SerializeField] private int projectileCount = 3;
    [SerializeField] private float projectileSize = 44f;
    [SerializeField] private float flyDuration = 0.7f;
    [SerializeField] private float flyStartY = -1250f;
    [SerializeField] private float flyStartXSpread = 120f;
    [SerializeField] private Vector2 targetXRange = new(-420f, 420f);
    [SerializeField] private Vector2 targetYRange = new(520f, 980f);
    [SerializeField] private float projectileStagger = 0.08f;
    [SerializeField] private Color projectileColor = new(1f, 0.92f, 0.35f, 1f);

    [Header("Burst")]
    [SerializeField] private int piecesPerBurst = 30;
    [SerializeField] private Vector2 pieceSizeRange = new(16f, 34f);
    [SerializeField] private float burstSpeedMin = 180f;
    [SerializeField] private float burstSpeedMax = 620f;
    [SerializeField] private float burstUpwardBias = 0.35f;
    [SerializeField] private float gravity = 1100f;
    [SerializeField] private float airDrag = 0.15f;
    [SerializeField] private float pieceLifetime = 2.8f;
    [SerializeField] private float spinSpeedMin = -420f;
    [SerializeField] private float spinSpeedMax = 420f;
    [SerializeField] private float despawnBelowY = -1400f;

    [Header("Rendering")]
    [SerializeField] private int sortingOrder = 500;

    [Header("Colors")]
    [SerializeField] private Color[] confettiColors =
    {
        new(1f, 0.23f, 0.33f, 1f),
        new(1f, 0.84f, 0.18f, 1f),
        new(0.2f, 0.85f, 0.95f, 1f),
        new(0.45f, 0.95f, 0.35f, 1f),
        new(0.72f, 0.35f, 0.98f, 1f),
        new(1f, 0.55f, 0.12f, 1f)
    };

    private static Sprite fallbackSprite;

    private readonly Dictionary<ConfettiShape, Sprite> shapeSprites = new();
    private readonly List<ConfettiPiece> activePieces = new();
    private readonly List<GameObject> spawnedObjects = new();

    private RectTransform container;
    private Canvas overlayCanvas;
    private bool isInitialized;

    private struct ConfettiPiece
    {
        public RectTransform Rect;
        public Image Image;
        public Vector2 Velocity;
        public float AngularVelocity;
        public float Lifetime;
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play()
    {
        EnsureInitialized();
        Stop();
        StartCoroutine(PlaySequence());
    }

    public void Stop()
    {
        StopAllCoroutines();
        activePieces.Clear();

        for (int i = spawnedObjects.Count - 1; i >= 0; i--)
        {
            if (spawnedObjects[i] != null)
            {
                Destroy(spawnedObjects[i]);
            }
        }

        spawnedObjects.Clear();
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
        {
            return;
        }

        container = GetComponent<RectTransform>();
        EnsureOverlayCanvas();
        BuildShapeSprites();
        isInitialized = true;
    }

    private void EnsureOverlayCanvas()
    {
        overlayCanvas = GetComponent<Canvas>();
        if (overlayCanvas == null)
        {
            overlayCanvas = gameObject.AddComponent<Canvas>();
        }

        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = sortingOrder;
    }

    private IEnumerator PlaySequence()
    {
        for (int i = 0; i < projectileCount; i++)
        {
            Vector2 target = new(
                Random.Range(targetXRange.x, targetXRange.y),
                Random.Range(targetYRange.x, targetYRange.y));

            float startX = Random.Range(-flyStartXSpread, flyStartXSpread);
            StartCoroutine(FlyProjectileAndBurst(new Vector2(startX, flyStartY), target));

            if (projectileStagger > 0f && i < projectileCount - 1)
            {
                yield return new WaitForSeconds(projectileStagger);
            }
        }
    }

    private IEnumerator FlyProjectileAndBurst(Vector2 start, Vector2 target)
    {
        Image projectile = CreateImage("Projectile", GetShapeSprite(ConfettiShape.Circle), projectileColor, projectileSize);
        RectTransform projectileRect = projectile.rectTransform;
        projectileRect.anchoredPosition = start;
        projectileRect.SetAsLastSibling();

        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOutCubic(Mathf.Clamp01(elapsed / flyDuration));
            projectileRect.anchoredPosition = Vector2.LerpUnclamped(start, target, t);
            projectileRect.localScale = Vector3.one * Mathf.Lerp(0.65f, 1.1f, t);
            yield return null;
        }

        projectileRect.anchoredPosition = target;
        DestroyTracked(projectile.gameObject);
        SpawnBurst(target);
        StartCoroutine(SimulatePieces());
    }

    private void SpawnBurst(Vector2 origin)
    {
        for (int i = 0; i < piecesPerBurst; i++)
        {
            ConfettiShape shape = (ConfettiShape)Random.Range(0, 5);
            float size = Random.Range(pieceSizeRange.x, pieceSizeRange.y);
            Color color = confettiColors[Random.Range(0, confettiColors.Length)];

            Image image = CreateImage("ConfettiPiece", GetShapeSprite(shape), color, size);
            RectTransform rect = image.rectTransform;
            rect.anchoredPosition = origin;
            rect.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            rect.SetAsLastSibling();

            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(burstSpeedMin, burstSpeedMax);
            Vector2 direction = new(Mathf.Cos(angle), Mathf.Sin(angle));
            direction.y += burstUpwardBias;
            direction.Normalize();

            activePieces.Add(new ConfettiPiece
            {
                Rect = rect,
                Image = image,
                Velocity = direction * speed,
                AngularVelocity = Random.Range(spinSpeedMin, spinSpeedMax),
                Lifetime = pieceLifetime
            });
        }
    }

    private IEnumerator SimulatePieces()
    {
        while (activePieces.Count > 0)
        {
            float deltaTime = Time.deltaTime;

            for (int i = activePieces.Count - 1; i >= 0; i--)
            {
                ConfettiPiece piece = activePieces[i];
                if (piece.Rect == null)
                {
                    activePieces.RemoveAt(i);
                    continue;
                }

                piece.Velocity.y -= gravity * deltaTime;
                piece.Velocity *= Mathf.Clamp01(1f - airDrag * deltaTime);
                piece.Rect.anchoredPosition += piece.Velocity * deltaTime;
                piece.Rect.Rotate(0f, 0f, piece.AngularVelocity * deltaTime);
                piece.Lifetime -= deltaTime;

                bool expired = piece.Lifetime <= 0f || piece.Rect.anchoredPosition.y < despawnBelowY;
                if (expired)
                {
                    DestroyTracked(piece.Image.gameObject);
                    activePieces.RemoveAt(i);
                    continue;
                }

                activePieces[i] = piece;
            }

            yield return null;
        }
    }

    private Image CreateImage(string objectName, Sprite sprite, Color color, float size)
    {
        GameObject go = new(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = gameObject.layer;
        go.transform.SetParent(container, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(size, size);
        rect.anchoredPosition = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = go.GetComponent<Image>();
        image.sprite = sprite != null ? sprite : GetFallbackSprite();
        image.type = Image.Type.Simple;
        image.color = color;
        image.raycastTarget = false;
        image.maskable = false;

        spawnedObjects.Add(go);
        return image;
    }

    private void DestroyTracked(GameObject go)
    {
        spawnedObjects.Remove(go);
        Destroy(go);
    }

    private Sprite GetShapeSprite(ConfettiShape shape)
    {
        if (shapeSprites.TryGetValue(shape, out Sprite sprite) && sprite != null)
        {
            return sprite;
        }

        return GetFallbackSprite();
    }

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite == null)
        {
            fallbackSprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
        }

        return fallbackSprite;
    }

    private void BuildShapeSprites()
    {
        shapeSprites[ConfettiShape.Square] = CreateSprite(BuildSquareTexture(32));
        shapeSprites[ConfettiShape.Circle] = CreateSprite(BuildCircleTexture(32));
        shapeSprites[ConfettiShape.Triangle] = CreateSprite(BuildTriangleTexture(32));
        shapeSprites[ConfettiShape.Diamond] = CreateSprite(BuildDiamondTexture(32));
        shapeSprites[ConfettiShape.Star] = CreateSprite(BuildStarTexture(32));
    }

    private static Sprite CreateSprite(Texture2D texture)
    {
        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);
    }

    private static Texture2D BuildSquareTexture(int size)
    {
        Texture2D texture = CreateBlankTexture(size);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                texture.SetPixel(x, y, Color.white);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildCircleTexture(int size)
    {
        Texture2D texture = CreateBlankTexture(size);
        float radius = size * 0.42f;
        Vector2 center = new(size * 0.5f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                texture.SetPixel(x, y, distance <= radius ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildTriangleTexture(int size)
    {
        Texture2D texture = CreateBlankTexture(size);
        Vector2 a = new(size * 0.5f, size * 0.86f);
        Vector2 b = new(size * 0.12f, size * 0.14f);
        Vector2 c = new(size * 0.88f, size * 0.14f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                texture.SetPixel(x, y, IsInsideTriangle(point, a, b, c) ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildDiamondTexture(int size)
    {
        Texture2D texture = CreateBlankTexture(size);
        Vector2 top = new(size * 0.5f, size * 0.9f);
        Vector2 right = new(size * 0.9f, size * 0.5f);
        Vector2 bottom = new(size * 0.5f, size * 0.1f);
        Vector2 left = new(size * 0.1f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y);
                bool inside = IsInsideTriangle(point, top, right, bottom)
                    || IsInsideTriangle(point, top, left, bottom);
                texture.SetPixel(x, y, inside ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildStarTexture(int size)
    {
        Texture2D texture = CreateBlankTexture(size);
        Vector2 center = new(size * 0.5f, size * 0.5f);
        float outerRadius = size * 0.44f;
        float innerRadius = size * 0.18f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 point = new Vector2(x, y) - center;
                float angle = Mathf.Atan2(point.y, point.x) + Mathf.PI * 0.5f;
                float radius = point.magnitude;
                float sector = Mathf.Repeat(angle / (Mathf.PI * 2f), 1f) * 10f;
                float t = sector - Mathf.Floor(sector);
                float boundaryRadius = Mathf.Lerp(innerRadius, outerRadius, t < 0.5f ? t * 2f : (1f - t) * 2f);
                texture.SetPixel(x, y, radius <= boundaryRadius ? Color.white : Color.clear);
            }
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D CreateBlankTexture(int size)
    {
        Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color32[] pixels = new Color32[size * size];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = new Color32(255, 255, 255, 0);
        }

        texture.SetPixels32(pixels);
        return texture;
    }

    private static bool IsInsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float denominator = (b.y - c.y) * (a.x - c.x) + (c.x - b.x) * (a.y - c.y);
        if (Mathf.Approximately(denominator, 0f))
        {
            return false;
        }

        float alpha = ((b.y - c.y) * (point.x - c.x) + (c.x - b.x) * (point.y - c.y)) / denominator;
        float beta = ((c.y - a.y) * (point.x - c.x) + (a.x - c.x) * (point.y - c.y)) / denominator;
        float gamma = 1f - alpha - beta;
        return alpha >= 0f && beta >= 0f && gamma >= 0f;
    }

    private static float EaseOutCubic(float t)
    {
        return 1f - Mathf.Pow(1f - t, 3f);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        projectileCount = Mathf.Max(1, projectileCount);
        piecesPerBurst = Mathf.Max(1, piecesPerBurst);
        flyDuration = Mathf.Max(0.05f, flyDuration);
        pieceLifetime = Mathf.Max(0.1f, pieceLifetime);

        if (isInitialized && overlayCanvas != null)
        {
            overlayCanvas.sortingOrder = sortingOrder;
        }
    }

    [ContextMenu("Preview Confetti")]
    private void PreviewConfetti()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        Play();
    }
#endif
}
