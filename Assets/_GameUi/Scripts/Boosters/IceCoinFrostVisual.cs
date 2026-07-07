using UnityEngine;
using UnityEngine.Rendering;

[DisallowMultipleComponent]
public class IceCoinFrostVisual : MonoBehaviour
{
    [System.Serializable]
    public class Settings
    {
        [Header("Frost Material")]
        public Material FrostMaterial;
        public Texture2D IceOverlayTexture;
        public Color FrostColor = new(0.72f, 0.9f, 1f, 0.62f);
        public Color RimColor = new(0.92f, 0.98f, 1f, 0.95f);
        [Range(0.5f, 8f)] public float RimPower = 2.4f;
        [Range(0f, 1f)] public float CrackIntensity = 0.35f;
        [Range(0f, 1f)] public float FrostAlpha = 0.78f;
        public float ShimmerSpeed = 0.65f;
        public float ShimmerScale = 3.5f;
        [Tooltip("Buz kopyasının Coin_Object'e göre ne kadar büyük olacağı.")]
        [Range(1f, 1.35f)] public float FrostOverlayScale = 1.08f;

        [Header("Steam Particles")]
        public Texture2D SteamParticleTexture;
        public Color SteamStartColor = new(0.88f, 0.96f, 1f, 0.55f);
        public Color SteamEndColor = new(0.88f, 0.96f, 1f, 0f);
        public float SteamEmissionRate = 14f;
        public float SteamStartSpeed = 0.18f;
        public float SteamStartSize = 0.045f;
        public float SteamLifetime = 1.1f;
        public float SteamGravity = -0.04f;
        public float SteamUpwardBias = 0.22f;
        [Tooltip("Flare dokuları için açık tutun; siyah arka planı önler.")]
        public bool SteamAdditiveBlend = true;
    }

    static readonly int IceTexId = Shader.PropertyToID("_IceTex");
    static readonly int FrostColorId = Shader.PropertyToID("_FrostColor");
    static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
    static readonly int CrackIntensityId = Shader.PropertyToID("_CrackIntensity");
    static readonly int AlphaId = Shader.PropertyToID("_Alpha");
    static readonly int ShimmerSpeedId = Shader.PropertyToID("_ShimmerSpeed");
    static readonly int ShimmerScaleId = Shader.PropertyToID("_ShimmerScale");

    Transform _coinObject;
    GameObject _frostOverlay;
    MeshRenderer _frostOverlayRenderer;
    Material _runtimeMaterial;
    Material _steamParticleMaterial;
    ParticleSystem _steamParticles;

    public void Apply(Settings settings)
    {
        if (settings == null || !TryResolveCoinObject())
        {
            return;
        }

        EnsureFrostOverlay(settings);
        EnsureSteam(settings);
        SetActiveState(true);
    }

    public void Remove()
    {
        SetActiveState(false);
    }

    void OnDestroy()
    {
        DestroyFrostOverlay();

        if (_runtimeMaterial != null)
        {
            Destroy(_runtimeMaterial);
        }

        if (_steamParticleMaterial != null)
        {
            Destroy(_steamParticleMaterial);
        }
    }

    bool TryResolveCoinObject()
    {
        if (_coinObject != null)
        {
            return true;
        }

        _coinObject = transform.Find("Coin_Object");
        return _coinObject != null;
    }

    void EnsureFrostOverlay(Settings settings)
    {
        MeshFilter sourceMeshFilter = _coinObject.GetComponent<MeshFilter>();
        if (sourceMeshFilter == null || sourceMeshFilter.sharedMesh == null)
        {
            return;
        }

        if (_frostOverlay == null)
        {
            _frostOverlay = new GameObject("IceFrostOverlay");
            _frostOverlay.transform.SetParent(_coinObject, false);
            _frostOverlay.transform.localPosition = Vector3.zero;
            _frostOverlay.transform.localRotation = Quaternion.identity;

            MeshFilter overlayMeshFilter = _frostOverlay.AddComponent<MeshFilter>();
            overlayMeshFilter.sharedMesh = sourceMeshFilter.sharedMesh;

            _frostOverlayRenderer = _frostOverlay.AddComponent<MeshRenderer>();
            _frostOverlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _frostOverlayRenderer.receiveShadows = false;

            MeshRenderer sourceRenderer = _coinObject.GetComponent<MeshRenderer>();
            if (sourceRenderer != null)
            {
                _frostOverlayRenderer.lightProbeUsage = sourceRenderer.lightProbeUsage;
                _frostOverlayRenderer.reflectionProbeUsage = sourceRenderer.reflectionProbeUsage;
            }
        }

        _frostOverlay.transform.localScale = Vector3.one * settings.FrostOverlayScale;

        Material frostMaterial = GetOrCreateFrostMaterial(settings);
        if (frostMaterial == null || _frostOverlayRenderer == null)
        {
            return;
        }

        int materialSlotCount = Mathf.Max(1, _frostOverlayRenderer.sharedMaterials.Length);
        MeshRenderer sourceRendererForSlots = _coinObject.GetComponent<MeshRenderer>();
        if (sourceRendererForSlots != null && sourceRendererForSlots.sharedMaterials.Length > 0)
        {
            materialSlotCount = sourceRendererForSlots.sharedMaterials.Length;
        }

        Material[] frostSlots = new Material[materialSlotCount];
        for (int i = 0; i < frostSlots.Length; i++)
        {
            frostSlots[i] = frostMaterial;
        }

        _frostOverlayRenderer.sharedMaterials = frostSlots;
    }

    Material GetOrCreateFrostMaterial(Settings settings)
    {
        if (_runtimeMaterial == null)
        {
            Material source = settings.FrostMaterial;
            if (source == null)
            {
                Shader shader = Shader.Find("PennyBall/IceFrost");
                if (shader == null)
                {
                    return null;
                }

                _runtimeMaterial = new Material(shader);
            }
            else
            {
                _runtimeMaterial = new Material(source);
            }
        }

        ApplyShaderSettings(_runtimeMaterial, settings);
        return _runtimeMaterial;
    }

    static void ApplyShaderSettings(Material material, Settings settings)
    {
        Texture overlay = settings.IceOverlayTexture != null
            ? settings.IceOverlayTexture
            : Texture2D.whiteTexture;

        material.SetTexture(IceTexId, overlay);
        material.SetColor(FrostColorId, settings.FrostColor);
        material.SetColor(RimColorId, settings.RimColor);
        material.SetFloat(RimPowerId, settings.RimPower);
        material.SetFloat(CrackIntensityId, settings.CrackIntensity);
        material.SetFloat(AlphaId, settings.FrostAlpha);
        material.SetFloat(ShimmerSpeedId, settings.ShimmerSpeed);
        material.SetFloat(ShimmerScaleId, settings.ShimmerScale);
    }

    void EnsureSteam(Settings settings)
    {
        if (_steamParticles == null)
        {
            var steamObject = new GameObject("IceSteamParticles");
            steamObject.transform.SetParent(_coinObject, false);
            _steamParticles = steamObject.AddComponent<ParticleSystem>();
            var renderer = steamObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }

        var main = _steamParticles.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = settings.SteamLifetime;
        main.startSpeed = settings.SteamStartSpeed;
        main.startSize = settings.SteamStartSize;
        main.gravityModifier = settings.SteamGravity;
        main.startColor = new ParticleSystem.MinMaxGradient(settings.SteamStartColor, settings.SteamEndColor);

        var emission = _steamParticles.emission;
        emission.rateOverTime = settings.SteamEmissionRate;

        var shape = _steamParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = ResolveCoinRadius() * 0.75f;

        var velocity = _steamParticles.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = settings.SteamUpwardBias;

        var colorOverLifetime = _steamParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(settings.SteamStartColor, 0f),
                new GradientColorKey(settings.SteamEndColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(settings.SteamStartColor.a, 0f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = gradient;

        var sizeOverLifetime = _steamParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.2f));
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        ParticleSystemRenderer particleRenderer = _steamParticles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.material = GetOrCreateSteamParticleMaterial(
                settings.SteamParticleTexture,
                settings.SteamAdditiveBlend);
        }
    }

    void SetActiveState(bool active)
    {
        if (_frostOverlay != null)
        {
            _frostOverlay.SetActive(active);
        }

        if (_steamParticles != null)
        {
            if (active)
            {
                _steamParticles.Play();
            }
            else
            {
                _steamParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }

    void DestroyFrostOverlay()
    {
        if (_frostOverlay != null)
        {
            Destroy(_frostOverlay);
            _frostOverlay = null;
            _frostOverlayRenderer = null;
        }
    }

    float ResolveCoinRadius()
    {
        MeshFilter meshFilter = _coinObject.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 extents = meshFilter.sharedMesh.bounds.extents;
            float maxExtent = Mathf.Max(extents.x, extents.y, extents.z);
            float scale = Mathf.Max(_coinObject.lossyScale.x, _coinObject.lossyScale.y, _coinObject.lossyScale.z);
            return Mathf.Max(maxExtent * scale, 0.02f);
        }

        SphereCollider sphereCollider = GetComponentInChildren<SphereCollider>();
        if (sphereCollider != null)
        {
            float scale = sphereCollider.transform.lossyScale.x;
            return Mathf.Max(sphereCollider.radius * scale, 0.02f);
        }

        return 0.03f;
    }

    Material GetOrCreateSteamParticleMaterial(Texture2D texture, bool additiveBlend)
    {
        if (_steamParticleMaterial == null)
        {
            _steamParticleMaterial = CreateParticleMaterial(texture, additiveBlend);
            return _steamParticleMaterial;
        }

        ConfigureParticleMaterial(_steamParticleMaterial, texture, additiveBlend);
        return _steamParticleMaterial;
    }

    static Material CreateParticleMaterial(Texture2D texture, bool additiveBlend)
    {
        string[] shaderNames =
        {
            "Universal Render Pipeline/Particles/Unlit",
            "Particles/Standard Unlit",
            "Sprites/Default"
        };

        foreach (string shaderName in shaderNames)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                continue;
            }

            var material = new Material(shader);
            ConfigureParticleMaterial(material, texture, additiveBlend);
            return material;
        }

        var fallback = new Material(Shader.Find("Sprites/Default"));
        ConfigureParticleMaterial(fallback, texture, additiveBlend);
        return fallback;
    }

    static void ConfigureParticleMaterial(Material material, Texture2D texture, bool additiveBlend)
    {
        if (material == null)
        {
            return;
        }

        if (texture != null)
        {
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }
        }

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", Color.white);
        }

        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", Color.white);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
        }

        if (material.HasProperty("_Blend"))
        {
            material.SetFloat("_Blend", additiveBlend ? 2f : 0f);
        }

        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt(
            "_DstBlend",
            (int)(additiveBlend ? BlendMode.One : BlendMode.OneMinusSrcAlpha));

        if (material.HasProperty("_ZWrite"))
        {
            material.SetInt("_ZWrite", 0);
        }

        if (material.HasProperty("_AlphaClip"))
        {
            material.SetFloat("_AlphaClip", 0f);
        }

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
    }
}
