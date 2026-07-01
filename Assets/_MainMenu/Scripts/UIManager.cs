using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] MatchingPanelController matchingPanel;
    [SerializeField] Button exerciseButton;

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

        if (exerciseButton == null)
        {
            Transform root = transform.parent;
            Transform exerciseTransform = root != null
                ? FindDeepChild(root, "Btn_Exercise")
                : null;
            if (exerciseTransform != null)
            {
                exerciseButton = exerciseTransform.GetComponent<Button>();
            }
        }

        if (exerciseButton != null)
        {
            exerciseButton.onClick.AddListener(OnAntremanButtonPressed);
        }
    }

    void OnDestroy()
    {
        if (exerciseButton != null)
        {
            exerciseButton.onClick.RemoveListener(OnAntremanButtonPressed);
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
        MainMenuClickSound.Play();

        if (matchingPanel == null)
        {
            Debug.LogError("[UIManager] Matching_Panel bulunamadı.");
            return;
        }

        matchingPanel.BeginMatchFlow();
    }

    public void OnAntremanButtonPressed()
    {
        MainMenuClickSound.Play();
        Debug.Log("Antreman yapılacak!");
    }

    static Transform FindDeepChild(Transform parent, string childName)
    {
        if (parent.name == childName)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeepChild(parent.GetChild(i), childName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
