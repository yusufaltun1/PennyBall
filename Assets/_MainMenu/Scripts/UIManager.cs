using UnityEngine;

public class UIManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void OnPlayButtonPressed ()
    {
        Debug.Log("Oyna butonuna basıldı!");
    }

    public void OnAntremanButtonPressed ()
    {
        Debug.Log("Antreman yapılacak!");
    }
}
