using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Ana menüdeki Play'den açılan maç seçimi paneli:
/// Play Online → Matching_Panel, Play with a Friend → kod oluştur / kod gir ekranları.
/// </summary>
[DisallowMultipleComponent]
public class MatchPresetPanelController : MonoBehaviour
{
    const string PlayOnlinePath = "PanelRoot/Container_PlayOnline";
    const string PlayWithFriendPath = "PanelRoot/Container_PlaywFriend";
    const string CodeGeneratedPath = "PanelRoot/Container_CodeGenerated";
    const string EnterCodePath = "PanelRoot/Container_EnterCode";

    public event Action PlayOnlineRequested;
    public event Action<string> JoinCodeSubmitted;
    public event Action<RoomCodeResult> RoomMatchFound;

    [Header("Copy Feedback")]
    [SerializeField] Sprite copiedSprite;
    [SerializeField] Color codeColor = Color.white;
    [SerializeField] Color copiedCodeColor = new Color32(0x00, 0x96, 0x11, 0xFF);
    [SerializeField, Min(0f)] float copiedFeedbackDuration = 2f;

    GameObject _playOnline;
    GameObject _playWithFriend;
    GameObject _codeGenerated;
    GameObject _enterCode;
    TextMeshProUGUI _codeText;
    TMP_InputField _joinCodeInput;
    bool _initialized;
    bool _createRequestInFlight;
    string _activeCode;
    Image _copyButtonImage;
    Sprite _copyButtonDefaultSprite;
    Coroutine _copyFeedbackRoutine;

    RectTransform _panelRoot;
    Vector2 _panelRootOriginalAnchoredPosition;
    Canvas _rootCanvas;

    const float KeyboardShiftLerpSpeed = 12f;
    const float KeyboardShiftSnapThreshold = 0.5f;

    void Awake()
    {
        EnsureInitialized();
    }

    void Update()
    {
        UpdateKeyboardShift();
    }

    /// <summary>
    /// Kod girme ekranında native klavye açıldığında PanelRoot'u yukarı kaydırır ki input/buton
    /// klavyenin altında kalmasın. PanelRoot'u hedef alıyoruz çünkü: (1) PanelRoot'un kendi
    /// pozisyonu üst nesnede (MatchPreset, sadece full-stretch bir Canvas çocuğu, layout grubu
    /// YOK) hiçbir layout grubu tarafından yönetilmiyor — Container_EnterCode'u doğrudan kaydırmak
    /// PanelRoot'un KENDİ VerticalLayoutGroup'uyla çakışırdı (her rebuild'de eski konuma geri
    /// çekerdi); (2) karartma arka planı PanelRoot'un DIŞINDA, MatchPreset'in ayrı bir kardeşi —
    /// bu yüzden PanelRoot kaydığında arkada boşluk kalmıyor, arka plan sabit tam ekran kalıyor.
    /// TouchScreenKeyboard.area sadece gerçek cihazlarda (Editör'de genelde Rect.zero) anlamlı.
    /// </summary>
    void UpdateKeyboardShift()
    {
        if (_panelRoot == null)
        {
            return;
        }

        float targetOffset = 0f;

        if (_enterCode != null && _enterCode.activeInHierarchy
            && _joinCodeInput != null && _joinCodeInput.isFocused
            && TouchScreenKeyboard.visible)
        {
            float keyboardHeightPx = TouchScreenKeyboard.area.height;
            float scaleFactor = _rootCanvas != null && _rootCanvas.scaleFactor > 0f
                ? _rootCanvas.scaleFactor
                : 1f;
            targetOffset = keyboardHeightPx / scaleFactor;
        }

        Vector2 pos = _panelRoot.anchoredPosition;
        float desiredY = _panelRootOriginalAnchoredPosition.y + targetOffset;

        pos.y = Mathf.Abs(pos.y - desiredY) < KeyboardShiftSnapThreshold
            ? desiredY
            : Mathf.Lerp(pos.y, desiredY, Time.unscaledDeltaTime * KeyboardShiftLerpSpeed);

        _panelRoot.anchoredPosition = pos;
    }

    void OnDisable()
    {
        ResetCopyFeedback();
    }

#if UNITY_EDITOR
    const string DefaultCopiedSpritePath = "Assets/_MainMenu/Textures/EnterTick_btn.png";

    void OnValidate()
    {
        if (copiedSprite == null)
        {
            copiedSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(DefaultCopiedSpritePath);
        }
    }
#endif

    public void Open()
    {
        EnsureInitialized();
        ShowSelection();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);

        if (_panelRoot != null)
        {
            // Kapanırken klavye-kaydırmasından kalmış olabilecek ofseti temizle; aksi halde
            // bir dahaki Open()'da Update() birkaç frame boyunca eski konumdan geri lerp eder.
            _panelRoot.anchoredPosition = _panelRootOriginalAnchoredPosition;
        }
    }

    void EnsureInitialized()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        _playOnline = FindChild(PlayOnlinePath);
        _playWithFriend = FindChild(PlayWithFriendPath);
        _codeGenerated = FindChild(CodeGeneratedPath);
        _enterCode = FindChild(EnterCodePath);

        Transform panelRootTransform = transform.Find("PanelRoot");
        _panelRoot = panelRootTransform as RectTransform;
        if (_panelRoot != null)
        {
            _panelRootOriginalAnchoredPosition = _panelRoot.anchoredPosition;
        }

        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null)
        {
            _rootCanvas = _rootCanvas.rootCanvas;
        }

        Transform codeTransform = transform.Find($"{CodeGeneratedPath}/CodeHolder/Code");
        _codeText = codeTransform != null ? codeTransform.GetComponent<TextMeshProUGUI>() : null;
        if (_codeText != null)
        {
            _codeText.color = codeColor;
        }

        Transform copyButtonTransform = transform.Find($"{CodeGeneratedPath}/CodeHolder/Btn_Copy");
        _copyButtonImage = copyButtonTransform != null ? copyButtonTransform.GetComponent<Image>() : null;
        _copyButtonDefaultSprite = _copyButtonImage != null ? _copyButtonImage.sprite : null;

        EnsureJoinCodeInput();

        Bind($"{PlayOnlinePath}/Btn_Play", OnPlayOnlinePressed);
        Bind($"{PlayWithFriendPath}/Btn_StartMatch", OnStartMatchPressed);
        Bind($"{PlayWithFriendPath}/Btn_JoinMatch", OnJoinMatchPressed);
        Bind($"{CodeGeneratedPath}/CodeHolder/Btn_Copy", OnCopyCodePressed);
        Bind($"{EnterCodePath}/CodeHolder/Btn_Copy", OnJoinSubmitPressed);
        Bind($"{CodeGeneratedPath}/Title/GoBack", OnGoBackPressed);
        Bind($"{EnterCodePath}/Title/GoBack", OnGoBackPressed);
        Bind("Btn_Close", OnClosePressed);
    }

    /// <summary>
    /// Container_EnterCode/CodeHolder sadece statik TMP metni olarak kurulmuştu (Container_CodeGenerated'ten
    /// kopyalanmış) — burada gerçek bir TMP_InputField'a çeviriyoruz. Code'un kendi rect'i hem metin hem
    /// viewport olarak kullanılıyor (CodeHolder'daki layout grubunu bozmamak için yeni sibling eklemiyoruz).
    /// </summary>
    void EnsureJoinCodeInput()
    {
        Transform codeHolder = transform.Find($"{EnterCodePath}/CodeHolder");
        if (codeHolder == null)
        {
            return;
        }

        // CodeHolder'ın kendi ContentSizeFitter'ı (Horizontal=PreferredSize) içeriğe göre
        // genişlemeye/daralmaya çalışıyor, AMA üst Container_EnterCode'un VerticalLayoutGroup'u
        // (ChildControlWidth=1 + ChildForceExpandWidth=1) CodeHolder'ı ZATEN tam genişliğe
        // zorluyor — ikisi aynı anda CodeHolder'ın genişliğini belirlemeye çalışınca, hangisinin
        // kazandığı o anki rebuild sırasına göre değişiyordu (özellikle metin 6 haneye ulaşıp TMP
        // tercih genişliğini yeniden hesapladığı anda) — "6 hanede buton/kod kayıyor" bug'ının
        // GERÇEK kök sebebi buydu (Code'un kendi LayoutElement'ini dondurmak bunu çözmüyordu,
        // çünkü CodeHolder'ın ChildControlWidth=0 olduğu için Code'un genişliği zaten üst satırın
        // boyutunu etkilemiyordu — asıl çakışma CodeHolder'ın KENDİ ContentSizeFitter'ıyla üst
        // layout grubu arasındaydı). Üst grup zaten genişliği sabitlediği için CSF gereksiz —
        // kapatıyoruz.
        ContentSizeFitter codeHolderFitter = codeHolder.GetComponent<ContentSizeFitter>();
        if (codeHolderFitter != null)
        {
            codeHolderFitter.enabled = false;
        }

        Transform codeLabelTransform = codeHolder.Find("Code");
        TextMeshProUGUI codeLabel = codeLabelTransform != null
            ? codeLabelTransform.GetComponent<TextMeshProUGUI>()
            : null;
        if (codeLabel == null)
        {
            return;
        }

        _joinCodeInput = codeHolder.GetComponent<TMP_InputField>();
        if (_joinCodeInput == null)
        {
            _joinCodeInput = codeHolder.gameObject.AddComponent<TMP_InputField>();
        }

        // CodeHolder'daki Code/Btn_Copy bir layout grubuyla diziliyor — Code'un kendi rect'i
        // DIŞINDA yeni bir sibling (ör. "Text Area") eklemek o diziliş bozup Btn_Copy'nin ikonuyla
        // çakışmasına yol açıyordu. Bu yüzden viewport olarak Code'un kendi rect'i kullanılıyor,
        // placeholder da CodeHolder'a değil doğrudan Code'un ALTINA ekleniyor (layout'u etkilemez).
        RectTransform codeRect = codeLabelTransform as RectTransform;

        // Code'un genişliği tasarımdaki statik metne göre (layout grubu + muhtemelen bir
        // ContentSizeFitter ile) hesaplanmıştı. Metni boşaltıp canlı yazıyla değiştirince içerik
        // genişliği her tuş vuruşunda değişip CodeHolder/Btn_Copy'yi kaydırıyordu (0 karakterken
        // geniş, 6 karakter dolunca dar). Orijinal boyutu dondurup layout'un içerikten bağımsız
        // sabit kalmasını sağlıyoruz.
        Vector2 originalSize = codeRect.rect.size;
        ContentSizeFitter sizeFitter = codeLabelTransform.GetComponent<ContentSizeFitter>();
        if (sizeFitter != null)
        {
            sizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            sizeFitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
        }

        LayoutElement layoutElement = codeLabelTransform.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = codeLabelTransform.gameObject.AddComponent<LayoutElement>();
        }

        layoutElement.preferredWidth = originalSize.x;
        layoutElement.preferredHeight = originalSize.y;
        layoutElement.minWidth = originalSize.x;
        layoutElement.flexibleWidth = 0f;

        TextMeshProUGUI placeholder = EnsureCodeInputPlaceholder(codeRect, codeLabel);

        codeLabel.raycastTarget = false;
        codeLabel.textWrappingMode = TextWrappingModes.NoWrap;
        codeLabel.text = string.Empty;

        _joinCodeInput.targetGraphic = codeHolder.GetComponent<Image>();
        _joinCodeInput.textViewport = codeRect;
        _joinCodeInput.textComponent = codeLabel;
        _joinCodeInput.placeholder = placeholder;
        _joinCodeInput.lineType = TMP_InputField.LineType.SingleLine;
        _joinCodeInput.characterLimit = 6;
        _joinCodeInput.contentType = TMP_InputField.ContentType.IntegerNumber;
        _joinCodeInput.richText = false;
        _joinCodeInput.shouldHideMobileInput = false;
        _joinCodeInput.transition = Selectable.Transition.None;
        _joinCodeInput.interactable = true;

        // İmleç (caret) görünmesin istendi — yazma hâlâ normal çalışır, sadece yanıp sönen
        // çizgi çizilmiyor. caretWidth=0 tek başına bazı TMP sürümlerinde yetmiyor (min 1px'e
        // yuvarlanabiliyor) — customCaretColor ile tamamen saydam yapmak daha güvenilir.
        _joinCodeInput.caretWidth = 0;
        _joinCodeInput.customCaretColor = true;
        _joinCodeInput.caretColor = new Color(0f, 0f, 0f, 0f);
    }

    static TextMeshProUGUI EnsureCodeInputPlaceholder(RectTransform codeRect, TextMeshProUGUI codeLabel)
    {
        Transform existing = codeRect.Find("Placeholder");
        TextMeshProUGUI placeholder = existing != null ? existing.GetComponent<TextMeshProUGUI>() : null;
        if (placeholder == null)
        {
            var placeholderObject = new GameObject("Placeholder", typeof(RectTransform));
            RectTransform placeholderRect = placeholderObject.GetComponent<RectTransform>();
            placeholderRect.SetParent(codeRect, false);
            StretchToParent(placeholderRect);

            placeholder = placeholderObject.AddComponent<TextMeshProUGUI>();
        }

        placeholder.font = codeLabel.font;
        placeholder.fontSize = codeLabel.fontSize;
        placeholder.fontStyle = codeLabel.fontStyle;
        placeholder.alignment = codeLabel.alignment;
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.raycastTarget = false;
        Color color = codeLabel.color;
        color.a *= 0.4f;
        placeholder.color = color;
        placeholder.text = "______";

        return placeholder;
    }

    static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }

    void ShowSelection()
    {
        SetActive(_playOnline, true);
        SetActive(_playWithFriend, true);
        SetActive(_codeGenerated, false);
        SetActive(_enterCode, false);
    }

    void ShowOnly(GameObject container)
    {
        SetActive(_playOnline, false);
        SetActive(_playWithFriend, false);
        SetActive(_codeGenerated, container == _codeGenerated);
        SetActive(_enterCode, container == _enterCode);
    }

    void OnPlayOnlinePressed()
    {
        MainMenuClickSound.Play();
        Close();
        PlayOnlineRequested?.Invoke();
    }

    async void OnStartMatchPressed()
    {
        MainMenuClickSound.Play();
        if (_createRequestInFlight)
        {
            return;
        }

        _createRequestInFlight = true;
        _activeCode = null;
        if (_codeText != null)
        {
            _codeText.text = "......";
        }

        ShowOnly(_codeGenerated);

        RoomCodeResult created = await PartyRoomService.RequestCreateCodeAsync();

        // Kullanıcı RPC dönene kadar geri/kapat yapmış olabilir — ekranda değilsek sonucu
        // uygulamadan odayı hemen iptal et (sunucuda askıda kalmasın).
        if (_codeGenerated == null || !_codeGenerated.activeSelf)
        {
            _createRequestInFlight = false;
            if (created.Success)
            {
                _ = PartyRoomService.CancelRoomAsync(created.Code);
            }

            return;
        }

        if (!created.Success)
        {
            _createRequestInFlight = false;
            Debug.LogWarning($"[MatchPreset] Oda kodu oluşturulamadı: {created.Reason}");
            if (_codeText != null)
            {
                _codeText.text = "------";
            }

            return;
        }

        _activeCode = created.Code;
        if (_codeText != null)
        {
            _codeText.text = created.Code;
        }

        // Kod ekrandayken host arka planda Photon'a bağlanır ve bu ekranda kalarak
        // arkadaşını bekler — panel burada değişmez, bulununca Matching_Panel'e devredilir.
        RoomCodeResult connected = await PartyRoomService.ConnectAndWaitForOpponentAsync(created);
        _createRequestInFlight = false;

        bool stillWaitingHere = _codeGenerated != null && _codeGenerated.activeSelf && _activeCode == created.Code;
        if (!stillWaitingHere)
        {
            // Kullanıcı beklerken geri/kapat yaptı (kod zaten o sırada iptal edildi) —
            // Photon bağlantısı kurulduysa onu da bırakalım.
            connected.Channel?.Dispose();
            return;
        }

        if (!connected.Success)
        {
            Debug.LogWarning($"[MatchPreset] Arkadaş bulunamadı: {connected.Reason}");
            if (_codeText != null)
            {
                _codeText.text = "------";
            }

            return;
        }

        _activeCode = null;
        Close();
        RoomMatchFound?.Invoke(connected);
    }

    void OnJoinMatchPressed()
    {
        MainMenuClickSound.Play();
        if (_joinCodeInput != null)
        {
            _joinCodeInput.SetTextWithoutNotify(string.Empty);
        }

        ShowOnly(_enterCode);

        if (_joinCodeInput != null)
        {
            _joinCodeInput.Select();
            _joinCodeInput.ActivateInputField();
        }
    }

    void OnJoinSubmitPressed()
    {
        MainMenuClickSound.Play();

        string code = _joinCodeInput != null ? (_joinCodeInput.text ?? string.Empty).Trim() : string.Empty;
        if (code.Length != 6)
        {
            Debug.LogWarning("[MatchPreset] Geçersiz oda kodu.");
            return;
        }

        Close();
        JoinCodeSubmitted?.Invoke(code);
    }

    void OnCopyCodePressed()
    {
        MainMenuClickSound.Play();
        if (string.IsNullOrEmpty(_activeCode))
        {
            return;
        }

        GUIUtility.systemCopyBuffer = LocalizationService.Get("party.invite_share_text", _activeCode);
        ShowCopyFeedback();
    }

    void ShowCopyFeedback()
    {
        if (copiedSprite == null)
        {
            Debug.LogWarning("[MatchPreset] Copied Sprite atanmamış; kopyalama geri bildirimi gösterilemedi.", this);
            return;
        }

        if (_copyButtonImage == null || !isActiveAndEnabled)
        {
            return;
        }

        if (_copyFeedbackRoutine != null)
        {
            StopCoroutine(_copyFeedbackRoutine);
        }

        _copyFeedbackRoutine = StartCoroutine(CopyFeedbackRoutine());
    }

    IEnumerator CopyFeedbackRoutine()
    {
        _copyButtonImage.sprite = copiedSprite;
        if (_codeText != null)
        {
            _codeText.color = copiedCodeColor;
        }

        yield return new WaitForSecondsRealtime(copiedFeedbackDuration);
        _copyFeedbackRoutine = null;
        _copyButtonImage.sprite = _copyButtonDefaultSprite;
        if (_codeText != null)
        {
            _codeText.color = codeColor;
        }
    }

    void ResetCopyFeedback()
    {
        if (_copyFeedbackRoutine != null)
        {
            StopCoroutine(_copyFeedbackRoutine);
            _copyFeedbackRoutine = null;
        }

        if (_copyButtonImage != null)
        {
            _copyButtonImage.sprite = _copyButtonDefaultSprite;
        }

        if (_codeText != null)
        {
            _codeText.color = codeColor;
        }
    }

    void OnGoBackPressed()
    {
        MainMenuClickSound.Play();
        CancelActiveCodeIfAny();
        ShowSelection();
    }

    void OnClosePressed()
    {
        MainMenuClickSound.Play();
        CancelActiveCodeIfAny();
        Close();
    }

    void CancelActiveCodeIfAny()
    {
        if (string.IsNullOrEmpty(_activeCode))
        {
            return;
        }

        _ = PartyRoomService.CancelRoomAsync(_activeCode);
        _activeCode = null;
    }

    void Bind(string path, UnityAction action)
    {
        Transform buttonTransform = transform.Find(path);
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button == null)
        {
            Debug.LogWarning($"[MatchPreset] Buton bulunamadı: {path}", this);
            return;
        }

        // Sahnede kopyalanan butonlardan kalan Inspector OnClick bağlantıları bu akışla çakışmasın.
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
        {
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
    }

    GameObject FindChild(string path)
    {
        Transform child = transform.Find(path);
        if (child == null)
        {
            Debug.LogWarning($"[MatchPreset] Obje bulunamadı: {path}", this);
        }

        return child != null ? child.gameObject : null;
    }

    static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }
}
