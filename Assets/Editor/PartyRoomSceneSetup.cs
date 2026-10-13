using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// "Play with Friends" > Create/Join kurulumu. Create, mevcut Matching_Panel'e
/// (quick-match ile aynı ekran) kod gösterme alanı ekler; Join için ayrı küçük bir
/// kod giriş diyaloğu oluşturur. Sadece Editor menüsünden tetiklenir (runtime'da çalışmaz)
/// — kaba/işlevsel bir başlangıç kurar, görsel polish Inspector'dan elle yapılır.
/// </summary>
public static class PartyRoomSceneSetup
{
    const string MainMenuScenePath = "Assets/_MainMenu/Scenes/MainMenu_Scene.unity";
    static readonly Color PanelBackground = new(0f, 0f, 0f, 0.82f);
    static readonly Color ButtonColor = new(0.16f, 0.5f, 0.2f, 1f);
    static readonly Color CancelButtonColor = new(0.5f, 0.16f, 0.16f, 1f);

    [MenuItem("PennyBall/Party/Setup Create+Join Room UI On MainMenu")]
    public static void SetupOnMainMenu()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog(
                "Party Room Setup",
                "Bu işlem Play modunda çalışmaz.\n\nPlay'i durdur (Stop) ve Edit modunda tekrar çalıştır.",
                "Tamam");
            return;
        }

        Scene mainMenu = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
        Canvas canvas = Object.FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[PartyRoom] MainMenu'de Canvas bulunamadı.");
            return;
        }

        GameObject uiManagerGo = GameObject.Find("UIManager");
        UIManager uiManager = uiManagerGo != null ? uiManagerGo.GetComponent<UIManager>() : null;
        if (uiManager == null)
        {
            Debug.LogError("[PartyRoom] UIManager bulunamadı.");
            return;
        }

        MatchingPanelController matchingPanel = Object.FindFirstObjectByType<MatchingPanelController>(
            FindObjectsInactive.Include);
        if (matchingPanel == null)
        {
            Debug.LogError("[PartyRoom] Matching_Panel / MatchingPanelController bulunamadı.");
            return;
        }

        // Önceki (artık kaldırılmış) ayrı CreateRoomPanel yaklaşımından kalan obje varsa temizle.
        Transform obsoleteCreatePanel = canvas.transform.Find("CreateRoomPanel");
        if (obsoleteCreatePanel != null)
        {
            Object.DestroyImmediate(obsoleteCreatePanel.gameObject);
        }

        AddCodeDisplayToMatchingPanel(matchingPanel);
        JoinRoomPanelController joinPanel = BuildJoinPanel(canvas.transform, matchingPanel);

        AssignUIManagerFields(uiManager, matchingPanel, joinPanel);
        WireTriggerButton(canvas.transform, "Create", uiManager, nameof(UIManager.OnCreateRoomButtonPressed));
        WireTriggerButton(canvas.transform, "Join", uiManager, nameof(UIManager.OnJoinRoomButtonPressed));

        joinPanel.gameObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(mainMenu);
        EditorSceneManager.SaveScene(mainMenu);
        Debug.Log("[PartyRoom] Matching_Panel'e kod alanı eklendi, JoinRoomPanel oluşturuldu ve bağlandı.");
    }

    [MenuItem("PennyBall/Party/Setup Create+Join Room UI On MainMenu", true)]
    static bool SetupOnMainMenuValidate()
    {
        return !Application.isPlaying;
    }

    /// <summary>Quick-match ile aynı Matching_Panel'e "CodeContainer/CodeText" ekler (Create/Join'de görünür).</summary>
    static void AddCodeDisplayToMatchingPanel(MatchingPanelController matchingPanel)
    {
        Transform panelTransform = matchingPanel.transform;

        Transform existing = panelTransform.Find("CodeContainer");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject container = CreateUIObject("CodeContainer", panelTransform);
        SetAnchoredBox(container.GetComponent<RectTransform>(), new Vector2(0f, 420f), new Vector2(700f, 160f));

        CreateText(container.transform, "Label", "Oda Kodun", 36f,
            new Vector2(0f, 60f), new Vector2(700f, 60f));
        TextMeshProUGUI codeText = CreateText(container.transform, "CodeText", "------", 84f,
            new Vector2(0f, -30f), new Vector2(700f, 120f));

        container.SetActive(false);

        SerializedObject so = new(matchingPanel);
        so.FindProperty("codeContainer").objectReferenceValue = container;
        so.FindProperty("codeText").objectReferenceValue = codeText;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(matchingPanel);
    }

    static JoinRoomPanelController BuildJoinPanel(Transform canvas, MatchingPanelController matchingPanel)
    {
        Transform existing = canvas.Find("JoinRoomPanel");
        if (existing != null)
        {
            Object.DestroyImmediate(existing.gameObject);
        }

        GameObject root = CreateStretchPanel("JoinRoomPanel", canvas);

        CreateText(root.transform, "Title", "Arkadaşının Kodunu Gir", 44f,
            new Vector2(0f, 300f), new Vector2(700f, 80f));

        TMP_InputField inputField = CreateInputField(root.transform, "CodeInputField",
            new Vector2(0f, 150f), new Vector2(500f, 120f));

        Button submitButton = CreateButton(root.transform, "SubmitButton", "Katıl", ButtonColor,
            new Vector2(0f, 20f), new Vector2(360f, 110f));

        TextMeshProUGUI statusText = CreateText(root.transform, "StatusText", "", 36f,
            new Vector2(0f, -120f), new Vector2(800f, 80f));

        Button cancelButton = CreateButton(root.transform, "CancelButton", "İptal", CancelButtonColor,
            new Vector2(0f, -250f), new Vector2(360f, 110f));

        var controller = root.AddComponent<JoinRoomPanelController>();
        SerializedObject so = new(controller);
        so.FindProperty("matchingPanel").objectReferenceValue = matchingPanel;
        so.FindProperty("codeInputField").objectReferenceValue = inputField;
        so.FindProperty("statusText").objectReferenceValue = statusText;
        so.FindProperty("submitButton").objectReferenceValue = submitButton;
        so.FindProperty("cancelButton").objectReferenceValue = cancelButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        return controller;
    }

    static void AssignUIManagerFields(
        UIManager uiManager, MatchingPanelController matchingPanel, JoinRoomPanelController joinPanel)
    {
        SerializedObject so = new(uiManager);
        var matchingProp = so.FindProperty("matchingPanel");
        if (matchingProp.objectReferenceValue == null)
        {
            matchingProp.objectReferenceValue = matchingPanel;
        }

        so.FindProperty("joinRoomPanel").objectReferenceValue = joinPanel;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(uiManager);
    }

    /// <summary>Item3 altındaki Create/Join GameObject'inin içindeki "Play" butonunu bulup UIManager'a bağlar.</summary>
    static void WireTriggerButton(Transform canvasRoot, string triggerObjectName, UIManager uiManager, string methodName)
    {
        Transform trigger = FindDeepChild(canvasRoot, triggerObjectName);
        if (trigger == null)
        {
            Debug.LogWarning($"[PartyRoom] '{triggerObjectName}' objesi sahnede bulunamadı — buton bağlanamadı.");
            return;
        }

        Button button = trigger.GetComponentInChildren<Button>(true);
        if (button == null)
        {
            Debug.LogWarning($"[PartyRoom] '{triggerObjectName}' altında Button bulunamadı.");
            return;
        }

        bool isCreate = methodName == nameof(UIManager.OnCreateRoomButtonPressed);
        FixButtonLabel(button, isCreate ? "party.create_button" : "party.join_button",
            isCreate ? "Create" : "Join");

        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            UnityEventTools.RemovePersistentListener(button.onClick, i);
        }

        if (methodName == nameof(UIManager.OnCreateRoomButtonPressed))
        {
            UnityEventTools.AddPersistentListener(button.onClick, uiManager.OnCreateRoomButtonPressed);
        }
        else
        {
            UnityEventTools.AddPersistentListener(button.onClick, uiManager.OnJoinRoomButtonPressed);
        }

        EditorUtility.SetDirty(button);
    }

    /// <summary>
    /// Create/Join butonları muhtemelen orijinal "Play" butonundan kopyalanmış; etiketlerindeki
    /// LocalizedText hâlâ paylaşılan "main.play_button" anahtarına bağlıysa Play modunda "Play" yazıyordu.
    /// Doğru anahtara geçirir (varsa), yoksa TMP metnini doğrudan ayarlar.
    /// </summary>
    static void FixButtonLabel(Button button, string localizationKey, string fallbackText)
    {
        LocalizedText localizedText = button.GetComponentInChildren<LocalizedText>(true);
        if (localizedText != null)
        {
            SerializedObject so = new(localizedText);
            so.FindProperty("_key").stringValue = localizationKey;
            so.ApplyModifiedPropertiesWithoutUndo();
            localizedText.Refresh();
            EditorUtility.SetDirty(localizedText);
            return;
        }

        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (label != null)
        {
            label.text = LocalizationService.Get(localizationKey);
            if (string.IsNullOrEmpty(label.text) || label.text == localizationKey)
            {
                label.text = fallbackText;
            }

            EditorUtility.SetDirty(label);
        }
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

    static GameObject CreateStretchPanel(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        go.GetComponent<Image>().color = PanelBackground;
        return go;
    }

    static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    static void SetAnchoredBox(RectTransform rt, Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    static TextMeshProUGUI CreateText(
        Transform parent, string name, string text, float fontSize, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        SetAnchoredBox(go.GetComponent<RectTransform>(), anchoredPosition, size);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        return tmp;
    }

    static Button CreateButton(
        Transform parent, string name, string label, Color color, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        SetAnchoredBox(go.GetComponent<RectTransform>(), anchoredPosition, size);
        Image background = go.GetComponent<Image>();
        background.color = color;

        CreateText(go.transform, "Label", label, 44f, Vector2.zero, size);

        Button button = go.GetComponent<Button>();
        button.targetGraphic = background;
        return button;
    }

    static TMP_InputField CreateInputField(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        root.transform.SetParent(parent, false);
        SetAnchoredBox(root.GetComponent<RectTransform>(), anchoredPosition, size);
        root.GetComponent<Image>().color = Color.white;

        GameObject textArea = new("Text Area", typeof(RectTransform), typeof(RectMask2D));
        textArea.transform.SetParent(root.transform, false);
        RectTransform textAreaRt = textArea.GetComponent<RectTransform>();
        textAreaRt.anchorMin = Vector2.zero;
        textAreaRt.anchorMax = Vector2.one;
        textAreaRt.offsetMin = new Vector2(20f, 8f);
        textAreaRt.offsetMax = new Vector2(-20f, -8f);

        TextMeshProUGUI placeholder = CreateText(
            textArea.transform, "Placeholder", "6 haneli kod", 48f, Vector2.zero, Vector2.zero);
        placeholder.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        placeholder.GetComponent<RectTransform>().anchorMax = Vector2.one;
        placeholder.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        placeholder.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        placeholder.color = new Color(0f, 0f, 0f, 0.4f);
        placeholder.fontStyle = FontStyles.Italic;

        TextMeshProUGUI textComponent = CreateText(
            textArea.transform, "Text", "", 48f, Vector2.zero, Vector2.zero);
        textComponent.GetComponent<RectTransform>().anchorMin = Vector2.zero;
        textComponent.GetComponent<RectTransform>().anchorMax = Vector2.one;
        textComponent.GetComponent<RectTransform>().offsetMin = Vector2.zero;
        textComponent.GetComponent<RectTransform>().offsetMax = Vector2.zero;
        textComponent.color = Color.black;

        TMP_InputField inputField = root.AddComponent<TMP_InputField>();
        inputField.textViewport = textAreaRt;
        inputField.textComponent = textComponent;
        inputField.placeholder = placeholder;
        inputField.characterLimit = 6;
        inputField.contentType = TMP_InputField.ContentType.IntegerNumber;

        return inputField;
    }
}
