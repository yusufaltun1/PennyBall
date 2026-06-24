using UnityEngine;
using UnityEngine.SceneManagement;

public class UIManager : MonoBehaviour
{
    [SerializeField] MatchingPanelController matchingPanel;

    void Awake()
    {
        if (matchingPanel == null)
        {
            Transform panelTransform = transform.parent != null
                ? transform.parent.Find("Matching_Panel")
                : null;
            if (panelTransform != null)
            {
                matchingPanel = panelTransform.GetComponent<MatchingPanelController>();
            }
        }

        if (matchingPanel != null)
        {
            matchingPanel.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        if (!SceneManager.GetSceneByName("3d_Saha_Studio").isLoaded)
        {
            SceneManager.LoadScene("3d_Saha_Studio", LoadSceneMode.Additive);
        }
    }

    public void OnPlayButtonPressed()
    {
        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginMatchFlow();
    }

    public void OnAntremanButtonPressed()
    {
        Debug.Log("Antreman yapılacak!");
    }
}
