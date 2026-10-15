using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ResultPanel &gt; RePlay &gt; Container_PlaywFriend: arkadaşla oynanan maç bitince iki seçenek.
/// Btn_JoinMatch (üstteki) = "Quit": rematch'i reddedip normal Continue akışına
/// (ResultPanelController.OnReplayDeclined → ReturnToMainMenu) döner. Btn_StartMatch
/// (alttaki) = "Play Again": MainMenu'deki Create/Join yapısı aynen var ama host/guest ayrımı
/// yok — AYNI Photon odasına (önceki maçın PhotonRoomName'i) tekrar katılıp birbirini
/// bekliyorlar; yeni Nakama kodu üretmeye/yazmaya gerek yok, zaten kim olduklarını biliyorlar.
/// NOT: buton renkleri/konumları (üst=kırmızı Btn_JoinMatch, alt=mavi Btn_StartMatch)
/// SABİT — sadece hangi GameObject'in hangi metni/davranışı taşıdığı değişiyor.
/// </summary>
[DisallowMultipleComponent]
public class RematchPanelController : MonoBehaviour
{
    const string ContainerPath = "Container_PlaywFriend";
    const string PlayAgainLocalizationKey = "result.play_again_button";
    const string QuitLocalizationKey = "result.quit_button";

    bool _bound;
    bool _pressed;
    Button _topButton;
    Button _bottomButton;

    public static RematchPanelController FindPanel()
    {
        ResultPanelController resultPanel =
            FindFirstObjectByType<ResultPanelController>(FindObjectsInactive.Include);
        if (resultPanel == null)
        {
            return null;
        }

        Transform rePlay = resultPanel.transform.Find("RePlay");
        if (rePlay == null)
        {
            return null;
        }

        RematchPanelController controller = rePlay.GetComponent<RematchPanelController>();
        if (controller == null)
        {
            controller = rePlay.gameObject.AddComponent<RematchPanelController>();
        }

        controller.EnsureBound();
        return controller;
    }

    /// <summary>
    /// ResultPanelController.ShowReplay() panel GÖRÜNÜR olurken açıkça çağırır — sadece
    /// Awake() anındaki (ResultPanelController.Awake, maç başlamadan önce) tek seferlik
    /// RetargetLabel'a güvenmek yerine, oyuncunun paneli GERÇEKTEN gördüğü an etiketleri o anki
    /// güncel dile göre tazeler. "Key" zaten aynıysa LocalizedText.Refresh() ucuz bir no-op'tan
    /// biraz fazlası (tek bir Get() çağrısı) olduğu için her seferinde çağırmak zararsız.
    /// </summary>
    public static void RefreshLocalization()
    {
        RematchPanelController controller = FindPanel();
        controller?.ApplyLocalizedLabels();
    }

    void EnsureBound()
    {
        if (_bound)
        {
            return;
        }

        _bound = true;
        _pressed = false;

        // Üstteki (Btn_JoinMatch) = Quit, alttaki (Btn_StartMatch) = Play Again — ikisi de
        // aynı rematch'i tetikliyordu, kullanıcı bunun kafa karıştırıcı olduğunu belirtti.
        // Buton renk/konumları SABİT kalıyor, sadece davranış+metin GameObject'lere göre atandı.
        _topButton = Bind($"{ContainerPath}/Btn_JoinMatch", OnQuitPressed);
        _bottomButton = Bind($"{ContainerPath}/Btn_StartMatch", OnPlayAgainPressed);
        SetButtonsInteractable(true);

        ApplyLocalizedLabels();
    }

    void ApplyLocalizedLabels()
    {
        // Bu butonlar MainMenu'deki Create/Join'den kopyalanmış — metinleri hâlâ
        // "Create a Match!"/"Join a Match!" diyordu. Artık gerçek anlamlarına göre ayrı
        // anahtarlara yönlendiriliyorlar.
        RetargetLabel($"{ContainerPath}/Btn_JoinMatch/Container/Play", QuitLocalizationKey);
        RetargetLabel($"{ContainerPath}/Btn_StartMatch/Container/Play", PlayAgainLocalizationKey);
    }

    void RetargetLabel(string path, string localizationKey)
    {
        Transform labelTransform = transform.Find(path);
        LocalizedText label = labelTransform != null ? labelTransform.GetComponent<LocalizedText>() : null;
        if (label == null)
        {
            Debug.LogWarning($"[Rematch] Buton metni bulunamadı: {path}", this);
            return;
        }

        label.Key = localizationKey;
    }

    Button Bind(string path, UnityAction action)
    {
        Transform buttonTransform = transform.Find(path);
        Button button = buttonTransform != null ? buttonTransform.GetComponent<Button>() : null;
        if (button == null)
        {
            Debug.LogWarning($"[Rematch] Buton bulunamadı: {path}", this);
            return null;
        }

        button.onClick.RemoveListener(action);
        button.onClick.AddListener(action);
        return button;
    }

    void SetButtonsInteractable(bool interactable)
    {
        if (_topButton != null)
        {
            _topButton.interactable = interactable;
        }

        if (_bottomButton != null)
        {
            _bottomButton.interactable = interactable;
        }
    }

    void OnPlayAgainPressed()
    {
        if (_pressed)
        {
            return;
        }

        _pressed = true;
        // Bastığım butonu hemen pasif göster — karşı taraf basınca o da kendi tarafında aynı
        // şekilde pasif olur ve doğrudan Matching_Panel'e geçer (MainMenu hiç görünmez).
        SetButtonsInteractable(false);
        MainMenuClickSound.Play();

        string previousRoomName = OnlineMatchSession.PhotonRoomName;
        if (string.IsNullOrEmpty(previousRoomName))
        {
            Debug.LogWarning("[Rematch] PhotonRoomName yok — rematch başlatılamadı.");
            _pressed = false;
            SetButtonsInteractable(true);
            return;
        }

        // AYNI Photon oda adını tekrar kullanmıyoruz: Photon Cloud'un bu isim için tuttuğu
        // SessionInfo.PlayerCount, oda tam kapanmadan tekrar katılınca eski (2) değeri
        // gösterebiliyor — bu da tek taraf basınca bile "rakip zaten orada" sanılıp maça
        // yalnız girilmesine yol açıyordu. Her rematch'te daha önce hiç kullanılmamış,
        // iki tarafın da bağımsız/aynı şekilde türetebildiği yeni bir isme geçiyoruz.
        string roomName = previousRoomName + "_rm";

        PendingRematch.Set(
            roomName,
            OnlineMatchSession.OpponentUserId,
            OnlineMatchSession.OpponentDisplayName,
            OnlineMatchSession.OpponentAvatarIndex);

        SceneManager.LoadScene(GameSceneNames.MainMenu);
    }

    void OnQuitPressed()
    {
        if (_pressed)
        {
            return;
        }

        _pressed = true;
        SetButtonsInteractable(false);
        MainMenuClickSound.Play();

        ResultPanelController resultPanel = GetComponentInParent<ResultPanelController>();
        if (resultPanel == null)
        {
            Debug.LogWarning("[Rematch] ResultPanelController bulunamadı — Quit uygulanamadı.", this);
            _pressed = false;
            SetButtonsInteractable(true);
            return;
        }

        resultPanel.OnReplayDeclined();
    }
}
