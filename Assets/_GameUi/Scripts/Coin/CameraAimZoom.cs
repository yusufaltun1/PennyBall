using UnityEngine;

public class CameraAimZoom : MonoBehaviour
{
    [Tooltip("Max çekilmede kameranın geri çekileceği mesafe (world units)")]
    [SerializeField, Range(0.05f, 5f)] float _maxPullBack = 0.6f;

    [Tooltip("Sağa/sola çekilince ek zoom katsayısı (dikey ekranda dar yön için)")]
    [SerializeField, Range(1f, 4f)]   float _sideZoomBoost = 2f;

    [Tooltip("Zoom in/out geçiş hızı")]
    [SerializeField, Range(1f, 20f)]     float _lerpSpeed   = 7f;

    [Tooltip("Düşük çekmede az, max çekmeye yaklaşınca hızlı artan zoom eğrisi (1 = doğrusal, 2+ = gecikmeli)")]
    [SerializeField, Range(1f, 5f)]      float _zoomCurveExponent = 2.75f;

    Vector3 _homePosition;
    float   _currentOffset;
    bool    _wasOffset;

    void Start()
    {
        _homePosition = transform.position;
        _wasOffset    = false;
    }

    void LateUpdate()
    {
        if (_currentOffset < 0.001f)
            _currentOffset = 0f;

        if (_currentOffset == 0f)
        {
            if (_wasOffset)
            {
                // Offset yeni kapandı: kamerayı temiz home'a döndür
                transform.position = _homePosition;
                _wasOffset = false;
            }
            else
            {
                // Offset yokken kamera başka bir script tarafından hareket edebilir — takip et
                _homePosition = transform.position;
            }
        }
        else
        {
            _wasOffset = true;
            transform.position = _homePosition - transform.forward * _currentOffset;
        }
    }

    /// <param name="pullRatio">Çekilme / MaxPullDistance (0–1).</param>
    /// <param name="sideRatio">Çekim ne kadar yatay (sağ/sol): 0 = düz ileri/geri, 1 = tam yatay.</param>
    public void SetDragState(float pullRatio, float sideRatio = 0f)
    {
        float easedPull = Mathf.Pow(Mathf.Clamp01(pullRatio), _zoomCurveExponent);
        float boost  = Mathf.Lerp(1f, _sideZoomBoost, sideRatio);
        float target = _maxPullBack * easedPull * boost;
        _currentOffset = Mathf.Lerp(_currentOffset, target, _lerpSpeed * Time.deltaTime);
    }
}
