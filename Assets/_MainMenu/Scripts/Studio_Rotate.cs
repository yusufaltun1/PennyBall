using UnityEngine;

public class Studio_Saha_Donus : MonoBehaviour
{
    public float donmeHizi = 15f;

    void Update()
    {
        // Sahayı kendi ekseninde (Y ekseni) yavaşça döndürür
        transform.Rotate(Vector3.up * donmeHizi * Time.deltaTime);
    }
}