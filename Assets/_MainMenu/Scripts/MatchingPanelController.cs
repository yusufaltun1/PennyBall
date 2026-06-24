using UnityEngine;
using UnityEngine.UI;
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
    private bool isShuffling = false;
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

    private void OnEnable()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
        }

        // Set starting position: top edge at bottom of Canvas (offscreen)
        if (rectTransform != null)
        {
            rectTransform.anchoredPosition = new Vector2(0f, 0f);
        }

        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
        }
        
        // Reset states
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

        matchmakingCoroutine = StartCoroutine(DoMatchmakingSequence());
    }

    private void OnDisable()
    {
        if (matchmakingCoroutine != null)
        {
            StopCoroutine(matchmakingCoroutine);
            matchmakingCoroutine = null;
        }
        isShuffling = false;
    }

    private void Update()
    {
        if (isShuffling && loopImage != null)
        {
            // Rotate the loop image (using standard negative rotate speed to rotate clockwise)
            loopImage.rectTransform.Rotate(0f, 0f, -rotateSpeed * Time.deltaTime);
        }
    }

    private IEnumerator DoMatchmakingSequence()
    {
        // 0. Slide Up Animation (Ease-In-Out Popup Transition)
        if (rectTransform != null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            float canvasHeight = 2340f; // safe fallback
            if (canvas != null)
            {
                RectTransform canvasRt = canvas.GetComponent<RectTransform>();
                if (canvasRt != null)
                {
                    canvasHeight = canvasRt.rect.height;
                }
            }

            float elapsed = 0f;
            float duration = 0.5f; // Fast, punchy and extremely smooth

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                
                // Ease-In-Out cubic/smoothstep curve
                float easedT = t * t * (3f - 2f * t);
                
                float currentY = Mathf.Lerp(0f, canvasHeight, easedT);
                rectTransform.anchoredPosition = new Vector2(0f, currentY);
                
                yield return null;
            }
            rectTransform.anchoredPosition = new Vector2(0f, canvasHeight);
        }

        isShuffling = true;

        if (avatarSprites == null || avatarSprites.Length == 0)
        {
            Debug.LogWarning("[Matchmaking] No avatar sprites assigned!");
            isShuffling = false;
            yield break;
        }

        // 1. Shuffle avatars, names, and scores 30 times
        for (int i = 0; i < 30; i++)
        {
            // Select random avatar
            Sprite randomAvatar = avatarSprites[UnityEngine.Random.Range(0, avatarSprites.Length)];
            if (opponentAvatarImage != null)
            {
                opponentAvatarImage.sprite = randomAvatar;
            }

            // Select random name
            string randomName = playerNames[UnityEngine.Random.Range(0, playerNames.Length)];
            if (opponentNameText != null)
            {
                opponentNameText.text = randomName;
            }

            // Select random score between 138 and 1342
            int randomScore = UnityEngine.Random.Range(138, 1343); // 1342 inclusive
            if (scoreText != null)
            {
                scoreText.text = randomScore.ToString();
            }

            yield return new WaitForSeconds(0.25f);
        }

        isShuffling = false;

        // 2. Hide Loop object
        if (loopImage != null)
        {
            loopImage.gameObject.SetActive(false);
        }

        // 3. Hide Finding Object completely and Show Sayac Object completely
        if (findingObject != null)
        {
            findingObject.SetActive(false);
        }
        if (sayacObject != null)
        {
            sayacObject.SetActive(true);
        }

        // 4. Count down from 5 to 0 on sayacText
        for (int count = 5; count >= 0; count--)
        {
            if (sayacText != null)
            {
                sayacText.fontSize = 96f; // Scale up to 1.5x of 64
                sayacText.text = count.ToString();
            }
            yield return new WaitForSeconds(1.0f);
        }

        // 5. Matchmaking complete, load GameUI via MatchLauncher
        Debug.Log("[Matchmaking] Countdown finished, launching game match!");
        MatchLauncher.StartLeagueMatch();
    }
}
