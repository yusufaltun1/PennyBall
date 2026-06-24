using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;

public class MatchingPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image opponentAvatarImage;
    [SerializeField] private Image loopImage;
    [SerializeField] private TextMeshProUGUI opponentNameText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private GameObject findingObject;
    [SerializeField] private TextMeshProUGUI findingText;
    [SerializeField] private GameObject sayacObject;
    [SerializeField] private TextMeshProUGUI sayacText;

    [Header("Avatars")]
    [SerializeField] private Sprite[] avatarSprites;

    [Header("Settings")]
    [SerializeField] private float rotateSpeed = 360f;
    [SerializeField] private float slideDuration = 0.5f;

    private bool isShuffling;
    private bool isRunning;
    private Coroutine matchmakingCoroutine;
    private RectTransform rectTransform;

    private readonly string[] playerNames = new string[]
    {
        "Alperen", "Batuhan", "Cem", "Doruk", "Emre", "Furkan", "Gökhan", "Hakan", "Kerem", "Mert",
        "Oğuz", "Onur", "Selin", "Zeynep", "Ece", "Buse", "Burak", "Yiğit", "Kaan", "Volkan",
        "Can", "Arda", "Serkan", "Melisa", "Seda", "Defne", "Gamze", "Tufan", "Bora", "Murat"
    };

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void OnDisable()
    {
        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
            matchmakingCoroutine = null;
        }

        isShuffling = false;
        isRunning = false;
    }

    private void Update()
    {
        if (isShuffling && loopImage != null)
        {
            loopImage.rectTransform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    public void BeginMatchFlow()
    {
        if (isRunning)
        {
            return;
        }

        isRunning = true;
        ResetUiState();

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
        }

        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        matchmakingCoroutine = StartCoroutine(DoMatchmakingSequence());
    }

    private void ResetUiState()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, 0f);
        }

        if (loopImage != null)
        {
            loopImage.gameObject.SetActive(true);
        }

        if (findingObject != null)
        {
            findingObject.SetActive(true);
        }

        if (sayacObject != null)
        {
            sayacObject.SetActive(false);
        }

        if (findingText != null)
        {
            findingText.fontSize = 64f;
            findingText.text = "Finding a match...";
        }
    }

    private IEnumerator DoMatchmakingSequence()
    {
        yield return SlidePanelUp();

        isShuffling = true;

        float shuffleElapsed = 0f;
        while (shuffleElapsed < 3.0f)
        {
            shuffleElapsed += 0.25f;

            if (avatarSprites != null && avatarSprites.Length > 0 && opponentAvatarImage != null)
            {
                int randomAvatarIndex = Random.Range(0, avatarSprites.Length);
                opponentAvatarImage.sprite = avatarSprites[randomAvatarIndex];
            }

            string randomName = playerNames[Random.Range(0, playerNames.Length)];
            if (opponentNameText != null)
            {
                opponentNameText.text = randomName;
            }

            int randomScore = Random.Range(138, 1343);
            if (scoreText != null)
            {
                scoreText.text = randomScore.ToString();
            }

            yield return new WaitForSeconds(0.25f);
        }

        isShuffling = false;

        if (loopImage != null)
        {
            loopImage.gameObject.SetActive(false);
        }

        if (findingObject != null)
        {
            findingObject.SetActive(false);
        }

        if (sayacObject != null)
        {
            sayacObject.SetActive(true);
        }

        for (int count = 5; count >= 0; count--)
        {
            if (sayacText != null)
            {
                sayacText.fontSize = 96f;
                sayacText.text = count.ToString();
            }

            yield return new WaitForSeconds(1.0f);
        }

        isRunning = false;
        matchmakingCoroutine = null;

        if (!OnboardingProgress.IsCompleted)
        {
            SceneManager.LoadScene(OnboardingSceneNames.Onboarding);
            yield break;
        }

        MatchLauncher.StartLeagueMatch();
    }

    private IEnumerator SlidePanelUp()
    {
        if (rectTransform == null)
        {
            yield break;
        }

        float canvasHeight = 2340f;
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            RectTransform canvasRt = canvas.GetComponent<RectTransform>();
            if (canvasRt != null && canvasRt.rect.height > 0f)
            {
                canvasHeight = canvasRt.rect.height;
            }
        }

        const float hiddenY = 0f;
        float targetY = canvasHeight;

        rectTransform.anchoredPosition = new Vector2(0f, hiddenY);

        float elapsed = 0f;
        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / slideDuration);
            float easedT = t * t * (3f - 2f * t);
            float currentY = Mathf.Lerp(hiddenY, targetY, easedT);
            rectTransform.anchoredPosition = new Vector2(0f, currentY);
            yield return null;
        }

        rectTransform.anchoredPosition = new Vector2(0f, targetY);
    }
}
