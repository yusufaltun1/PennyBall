using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ResultPanel &gt; RePlay &gt; Container_PlaywFriend: arkadaşla oynanan maç bitince "Play Again".
/// MainMenu'deki Create/Join yapısı aynen var ama host/guest ayrımı yok — ikisi de AYNI Photon
/// odasına (önceki maçın PhotonRoomName'i) tekrar katılıp birbirini bekliyor; yeni Nakama kodu
/// üretmeye/yazmaya gerek yok, zaten kim olduklarını biliyorlar.
/// </summary>
[DisallowMultipleComponent]
public class RematchPanelController : MonoBehaviour
{
    const string ContainerPath = "Container_PlaywFriend";

    bool _bound;
    bool _pressed;
    Button _startButton;
    Button _joinButton;

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

    void EnsureBound()
    {
        if (_bound)
        {
            return;
        }

        _bound = true;
        _pressed = false;

        _startButton = Bind($"{ContainerPath}/Btn_StartMatch", OnPlayAgainPressed);
        _joinButton = Bind($"{ContainerPath}/Btn_JoinMatch", OnPlayAgainPressed);
        SetButtonsInteractable(true);

        // Bu butonlar MainMenu'deki Create/Join'den kopyalanmış — metinleri hâlâ
        // "Create a Match!"/"Join a Match!" diyordu. Rematch'te ikisi de aynı "Play Again"
        // anlamına geldiği için label'larını ortak bir anahtara çeviriyoruz.
        RetargetLabel($"{ContainerPath}/Btn_StartMatch/Container/Play");
        RetargetLabel($"{ContainerPath}/Btn_JoinMatch/Container/Play");
    }

    void RetargetLabel(string path)
    {
        Transform labelTransform = transform.Find(path);
        LocalizedText label = labelTransform != null ? labelTransform.GetComponent<LocalizedText>() : null;
        if (label == null)
        {
            Debug.LogWarning($"[Rematch] Buton metni bulunamadı: {path}", this);
            return;
        }

        label.Key = "result.play_again_button";
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
        if (_startButton != null)
        {
            _startButton.interactable = interactable;
        }

        if (_joinButton != null)
        {
            _joinButton.interactable = interactable;
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
}
