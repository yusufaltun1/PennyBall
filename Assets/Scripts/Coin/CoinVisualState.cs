using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CoinIdentity))]
public class CoinVisualState : MonoBehaviour
{
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] float _playerPassiveColorMultiplier = 0.55f;
    [SerializeField] Color _opponentColor = Color.black;
    [SerializeField] Color _opponentPassiveColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    MaterialPropertyBlock _propertyBlock;
    Renderer[] _renderers;
    Color[][] _baseColors;
    CoinIdentity _identity;

    void Awake()
    {
        _identity = GetComponent<CoinIdentity>();
        _propertyBlock = new MaterialPropertyBlock();
        CacheRenderers();
        CacheBaseColors();
    }

    void Start()
    {
        if (IsOpponentCoin())
        {
            ApplyColorToRenderers(_opponentColor);
        }
    }

    bool IsOpponentCoin()
    {
        return _identity != null && _identity.Team == CoinTeam.Opponent;
    }

    void CacheRenderers()
    {
        Transform coinObject = transform.Find("Coin_Object");
        if (coinObject == null)
        {
            _renderers = System.Array.Empty<Renderer>();
            return;
        }

        _renderers = coinObject.GetComponentsInChildren<Renderer>();
    }

    void CacheBaseColors()
    {
        if (_renderers == null)
        {
            return;
        }

        _baseColors = new Color[_renderers.Length][];
        for (int rendererIndex = 0; rendererIndex < _renderers.Length; rendererIndex++)
        {
            Renderer renderer = _renderers[rendererIndex];
            if (renderer == null)
            {
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            _baseColors[rendererIndex] = new Color[materials.Length];
            for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
            {
                if (IsOpponentCoin())
                {
                    _baseColors[rendererIndex][materialIndex] = _opponentColor;
                    continue;
                }

                Material material = materials[materialIndex];
                if (material != null && material.HasProperty(BaseColorId))
                {
                    _baseColors[rendererIndex][materialIndex] = material.GetColor(BaseColorId);
                }
                else
                {
                    _baseColors[rendererIndex][materialIndex] = Color.white;
                }
            }
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
            CacheBaseColors();
        }

        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        if (passive)
        {
            ApplyPassiveVisual();
            return;
        }

        ClearVisualOverride();
    }

    void ApplyColorToRenderers(Color color)
    {
        if (_propertyBlock == null || _renderers == null)
        {
            return;
        }

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
                _propertyBlock.Clear();
                _propertyBlock.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(_propertyBlock, materialIndex);
            }
        }
    }

    void ApplyPassiveVisual()
    {
        if (_propertyBlock == null || _renderers == null)
        {
            return;
        }

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
                Color baseColor = _baseColors[rendererIndex][materialIndex];
                Color passiveColor = GetPassiveColor(baseColor);

                _propertyBlock.Clear();
                _propertyBlock.SetColor(BaseColorId, passiveColor);

                renderer.SetPropertyBlock(_propertyBlock, materialIndex);
            }
        }
    }

    Color GetPassiveColor(Color baseColor)
    {
        if (IsOpponentCoin())
        {
            return _opponentPassiveColor;
        }

        return new Color(
            baseColor.r * _playerPassiveColorMultiplier,
            baseColor.g * _playerPassiveColorMultiplier,
            baseColor.b * _playerPassiveColorMultiplier,
            baseColor.a);
    }

    void ClearVisualOverride()
    {
        if (_renderers == null)
        {
            return;
        }

        if (IsOpponentCoin())
        {
            ApplyColorToRenderers(_opponentColor);
            return;
        }

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
