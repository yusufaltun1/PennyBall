using UnityEngine;

/// <summary>
/// GameUI kamerası iPhone 12 (19.5:9) oranına göre yerleştirildi. Daha geniş
/// (kısa boylu) ekranlarda — örn. iPhone SE 16:9 — üstteki ScoreBar dikeyde
/// orantısal olarak daha çok yer kapladığı için sahayı keser. Bu bileşen,
/// sahne açılırken ekran oranını referans oranla karşılaştırır ve fark kadar
/// kamerayı bakış ekseni boyunca geri çekerek (zoom out) sahayı küçültür.
/// MatchIntroCameraFlythrough ve CameraAimZoom "home" pozisyonu Awake/Start'ta
/// yakaladığı için bu script onlardan ÖNCE çalışmalıdır (DefaultExecutionOrder).
/// </summary>
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Camera))]
public class CameraAspectZoomFit : MonoBehaviour
{
    [Tooltip("Tasarımın baz aldığı referans çözünürlük (iPhone 12: 1170x2532)")]
    [SerializeField] Vector2 _referenceResolution = new Vector2(1170f, 2532f);

    [Tooltip("Zoom out şiddeti (1 = oran farkı kadar birebir geri çekilme)")]
    [SerializeField, Range(0.5f, 2f)] float _zoomStrength = 1f;

    void Awake()
    {
        ApplyAspectZoom();
    }

    void ApplyAspectZoom()
    {
        if (_referenceResolution.x <= 0f || _referenceResolution.y <= 0f || Screen.height <= 0)
        {
            return;
        }

        float referenceAspect = _referenceResolution.x / _referenceResolution.y;
        float currentAspect = (float)Screen.width / Screen.height;

        // Referanstan dar ekranlarda (daha uzun telefonlar) zoom in yapma;
        // sadece geniş ekranlarda oran farkı kadar geri çekil.
        float zoomFactor = 1f + Mathf.Max(0f, currentAspect / referenceAspect - 1f) * _zoomStrength;
        if (zoomFactor <= 1.0001f)
        {
            return;
        }

        Vector3 forward = transform.forward;
        if (forward.y >= -0.0001f)
        {
            // Kamera yere doğru bakmıyorsa mesafe hesaplanamaz; dokunma.
            return;
        }

        // Kameranın bakış ışınının saha düzlemine (y=0) olan mesafesi.
        // Işın boyunca geri gitmek merkez noktayı sabit tutar, görüntüyü
        // her iki eksende de zoomFactor oranında genişletir.
        float distanceToField = transform.position.y / -forward.y;
        transform.position -= forward * (distanceToField * (zoomFactor - 1f));
    }
}
