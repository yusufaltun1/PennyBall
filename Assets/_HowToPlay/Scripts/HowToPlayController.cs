using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Adımlı kural anlatımı. FirstRun: bitince Onboarding'e geçer.
/// FromMenu: ana menü üstünde overlay olarak açılır, bitince kendini kapatır.
/// </summary>
[DisallowMultipleComponent]
public class HowToPlayController : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public enum Mode
    {
        FirstRun,
        FromMenu,
    }

    const string NextKey = "how_to_play.button.next";
    const string StartKey = "how_to_play.button.start";
    const string DoneKey = "how_to_play.button.done";

    [SerializeField] Mode _mode = Mode.FirstRun;
    [SerializeField] HowToPlayContent _content;

    [Header("Page")]
    [SerializeField] TMP_Text _titleText;
    [SerializeField] TMP_Text _bodyText;
    [SerializeField] Image _pageImage;
    [SerializeField] RectTransform _visualContainer;

    [Header("Page Indicator")]
    [SerializeField] RectTransform _dotsContainer;
    [SerializeField] Image _dotTemplate;
    [SerializeField] Color _activeDotColor = Color.white;
    [SerializeField] Color _inactiveDotColor = new(1f, 1f, 1f, 0.35f);

    [Header("Buttons")]
    [SerializeField] Button _nextButton;
    [SerializeField] TMP_Text _nextButtonLabel;
    [SerializeField] Button _backButton;
    [SerializeField] Button _skipButton;
    [SerializeField] Button _closeButton;

    [Header("Swipe")]
    [SerializeField] float _swipeThresholdPixels = 80f;

    readonly List<Image> _dots = new();
    GameObject _activeVisual;
    int _pageIndex;
    bool _finished;
    Vector2 _dragStart;

    public event Action Closed;

    int PageCount => _content != null ? _content.Pages.Count : 0;

    public void Configure(Mode mode)
    {
        _mode = mode;
    }

    void Awake()
    {
        WireButton(_nextButton, OnNextPressed);
        WireButton(_backButton, OnBackPressed);
        WireButton(_skipButton, OnSkipPressed);
        WireButton(_closeButton, OnClosePressed);
    }

    void OnEnable()
    {
        LocalizationService.LanguageChanged += RefreshTexts;
    }

    void OnDisable()
    {
        LocalizationService.LanguageChanged -= RefreshTexts;
    }

    void Start()
    {
        if (PageCount == 0)
        {
            Debug.LogError("[HowToPlay] HowToPlayContent atanmamış ya da sayfa yok.", this);
            if (_mode == Mode.FirstRun)
            {
                SceneManager.LoadScene(FirstRunFlow.GetSceneAfterHowToPlay());
            }
            else
            {
                Destroy(gameObject);
            }

            return;
        }

        if (_skipButton != null)
        {
            _skipButton.gameObject.SetActive(_mode == Mode.FirstRun);
        }

        if (_closeButton != null)
        {
            _closeButton.gameObject.SetActive(_mode == Mode.FromMenu);
        }

        BuildDots();
        GameAnalytics.Track("how_to_play_open", "mode", _mode.ToString());
        ShowPage(0);
    }

    void Update()
    {
        if (WasBackKeyPressed())
        {
            HandleBackKey();
        }
    }

    static bool WasBackKeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.Escape);
#endif
    }

    void HandleBackKey()
    {
        if (_pageIndex > 0)
        {
            ShowPage(_pageIndex - 1);
        }
        else if (_mode == Mode.FromMenu)
        {
            Finish("close");
        }
    }

    void OnNextPressed()
    {
        MainMenuClickSound.Play();

        if (_pageIndex >= PageCount - 1)
        {
            Finish("complete");
            return;
        }

        ShowPage(_pageIndex + 1);
    }

    void OnBackPressed()
    {
        MainMenuClickSound.Play();
        ShowPage(_pageIndex - 1);
    }

    void OnSkipPressed()
    {
        MainMenuClickSound.Play();
        Finish("skip");
    }

    void OnClosePressed()
    {
        MainMenuClickSound.Play();
        Finish("close");
    }

    void ShowPage(int index)
    {
        if (PageCount == 0)
        {
            return;
        }

        _pageIndex = Mathf.Clamp(index, 0, PageCount - 1);
        HowToPlayContent.Page page = _content.Pages[_pageIndex];

        RefreshTexts();
        ShowVisual(page);
        RefreshDots();

        if (_backButton != null)
        {
            _backButton.gameObject.SetActive(_pageIndex > 0);
        }

        GameAnalytics.Track("how_to_play_page_view", new Dictionary<string, string>
        {
            { "mode", _mode.ToString() },
            { "page", (_pageIndex + 1).ToString() },
        });
    }

    void RefreshTexts()
    {
        if (PageCount == 0)
        {
            return;
        }

        HowToPlayContent.Page page = _content.Pages[_pageIndex];

        if (_titleText != null)
        {
            _titleText.text = LocalizationService.Get(page.titleKey);
        }

        if (_bodyText != null)
        {
            _bodyText.text = LocalizationService.Get(page.bodyKey);
        }

        if (_nextButtonLabel != null)
        {
            bool lastPage = _pageIndex >= PageCount - 1;
            string key = !lastPage ? NextKey : _mode == Mode.FirstRun ? StartKey : DoneKey;
            _nextButtonLabel.text = LocalizationService.Get(key);
        }
    }

    void ShowVisual(HowToPlayContent.Page page)
    {
        if (_activeVisual != null)
        {
            Destroy(_activeVisual);
            _activeVisual = null;
        }

        bool usePrefab = page.visualPrefab != null && _visualContainer != null;
        if (usePrefab)
        {
            _activeVisual = Instantiate(page.visualPrefab, _visualContainer, false);
        }

        if (_pageImage != null)
        {
            _pageImage.sprite = page.image;
            _pageImage.preserveAspect = true;
            _pageImage.gameObject.SetActive(!usePrefab && page.image != null);
        }
    }

    void BuildDots()
    {
        if (_dotsContainer == null || _dotTemplate == null)
        {
            return;
        }

        _dotTemplate.gameObject.SetActive(false);

        for (int i = 0; i < PageCount; i++)
        {
            Image dot = Instantiate(_dotTemplate, _dotsContainer, false);
            dot.gameObject.SetActive(true);
            _dots.Add(dot);
        }
    }

    void RefreshDots()
    {
        for (int i = 0; i < _dots.Count; i++)
        {
            _dots[i].color = i == _pageIndex ? _activeDotColor : _inactiveDotColor;
        }
    }

    void Finish(string reason)
    {
        if (_finished)
        {
            return;
        }

        _finished = true;

        GameAnalytics.Track("how_to_play_finish", new Dictionary<string, string>
        {
            { "mode", _mode.ToString() },
            { "reason", reason },
            { "page", (_pageIndex + 1).ToString() },
        });

        if (_mode == Mode.FirstRun)
        {
            HowToPlayProgress.MarkSeen();
            SceneManager.LoadScene(FirstRunFlow.GetSceneAfterHowToPlay());
            return;
        }

        Closed?.Invoke();
        Destroy(gameObject);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _dragStart = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        float deltaX = eventData.position.x - _dragStart.x;
        if (Mathf.Abs(deltaX) < _swipeThresholdPixels)
        {
            return;
        }

        // Son sayfada kaydırma bitirmez; yanlışlıkla geçilmesin diye buton gerekir.
        if (deltaX < 0f && _pageIndex < PageCount - 1)
        {
            ShowPage(_pageIndex + 1);
        }
        else if (deltaX > 0f && _pageIndex > 0)
        {
            ShowPage(_pageIndex - 1);
        }
    }

    static void WireButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }
}
