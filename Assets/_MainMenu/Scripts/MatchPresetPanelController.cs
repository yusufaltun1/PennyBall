using System;
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

    GameObject _playOnline;
    GameObject _playWithFriend;
    GameObject _codeGenerated;
    GameObject _enterCode;
    bool _initialized;

    void Awake()
    {
        EnsureInitialized();
    }

    public void Open()
    {
        EnsureInitialized();
        ShowSelection();
        gameObject.SetActive(true);
    }

    public void Close()
    {
        gameObject.SetActive(false);
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

        Bind($"{PlayOnlinePath}/Btn_Play", OnPlayOnlinePressed);
        Bind($"{PlayWithFriendPath}/Btn_StartMatch", OnStartMatchPressed);
        Bind($"{PlayWithFriendPath}/Btn_JoinMatch", OnJoinMatchPressed);
        Bind($"{CodeGeneratedPath}/Title/GoBack", OnGoBackPressed);
        Bind($"{EnterCodePath}/Title/GoBack", OnGoBackPressed);
        Bind("Btn_Close", OnClosePressed);
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

    void OnStartMatchPressed()
    {
        MainMenuClickSound.Play();
        ShowOnly(_codeGenerated);
    }

    void OnJoinMatchPressed()
    {
        MainMenuClickSound.Play();
        ShowOnly(_enterCode);
    }

    void OnGoBackPressed()
    {
        MainMenuClickSound.Play();
        ShowSelection();
    }

    void OnClosePressed()
    {
        MainMenuClickSound.Play();
        Close();
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
