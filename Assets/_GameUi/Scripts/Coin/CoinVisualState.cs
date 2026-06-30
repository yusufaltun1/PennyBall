using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CoinIdentity))]
public class CoinVisualState : MonoBehaviour
{
    [Serializable]
    public struct DisabledColors
    {
        public Color outerColor;
        public Color innerColor;
    }

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Player")]
    [SerializeField] DisabledColors _playerDisabled = new()
    {
        outerColor = new Color(0.8f, 0.8f, 0.8f, 1f),
        innerColor = new Color(0.8f, 0.8f, 0.8f, 1f),
    };

    [Header("Enemy")]
    [SerializeField] DisabledColors _enemyDisabled = new()
    {
        outerColor = new Color(0.8f, 0.8f, 0.8f, 1f),
        innerColor = new Color(0.8f, 0.8f, 0.8f, 1f),
    };

    MaterialPropertyBlock _propertyBlock;
    Renderer[] _renderers;
    int[][] _materialColorPropertyIds;
    CoinIdentity _identity;

    void Awake()
    {
        _identity = GetComponent<CoinIdentity>();
        _propertyBlock = new MaterialPropertyBlock();
        CacheRenderers();
    }

    void Start()
    {
        if (_identity != null && _identity.IsPassive)
        {
            SetPassiveVisual(true);
        }
    }

    public void SetPassiveVisual(bool passive)
    {
        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        if (_renderers == null || _renderers.Length == 0)
        {
            CacheRenderers();
        }

        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        if (passive)
        {
            ApplyDisabledColors();
            return;
        }

        ClearOverride();
    }

    void CacheRenderers()
    {
        Transform coinObject = transform.Find("Coin_Object");
        if (coinObject == null)
        {
            _renderers = System.Array.Empty<Renderer>();
            _materialColorPropertyIds = System.Array.Empty<int[]>();
            return;
        }

        _renderers = coinObject.GetComponentsInChildren<Renderer>();
        _materialColorPropertyIds = new int[_renderers.Length][];

        for (int rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            Renderer renderer = _renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            _materialColorPropertyIds[rendererIndex] = new int[materials.Length];
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                _materialColorPropertyIds[rendererIndex][materialIndex] =
                    ResolveColorPropertyId(materials[materialIndex]);
            }
        }
    }

    static int ResolveColorPropertyId(Material material)
    {
        if (material == null)
        {
            return BaseColorId;
        }

        if (material.HasProperty(BaseColorId))
        {
            return BaseColorId;
        }

        if (material.HasProperty(ColorId))
        {
            return ColorId;
        }

        return BaseColorId;
    }

    void ApplyDisabledColors()
    {
        CoinTeam team = ResolveTeam();
        DisabledColors colors = team == CoinTeam.Opponent ? _enemyDisabled : _playerDisabled;

        for (int rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            Renderer renderer = _renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            int materialCount = renderer.sharedMaterials.Length;
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                int colorPropertyId = _materialColorPropertyIds[rendererIndex][materialIndex];
                Color disabledColor = materialIndex == 1 ? colors.innerColor : colors.outerColor;

                _propertyBlock.Clear();
                _propertyBlock.SetColor(colorPropertyId, disabledColor);
                renderer.SetPropertyBlock(_propertyBlock, materialIndex);
            }
        }
    }

    CoinTeam ResolveTeam()
    {
        if (_identity != null)
        {
            return _identity.Team;
        }

        return CoinTeam.Player;
    }

    void ClearOverride()
    {
        for (int rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            Renderer renderer = _renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            int materialCount = renderer.sharedMaterials.Length;
            for (int materialIndex = 0; materialIndex < materialCount; materialIndex++)
            {
                renderer.SetPropertyBlock(null, materialIndex);
            }
        }
    }
}
