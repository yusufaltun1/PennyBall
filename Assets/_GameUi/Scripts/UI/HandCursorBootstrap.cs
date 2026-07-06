using UnityEngine;
using UnityEngine.SceneManagement;

public static class HandCursorBootstrap
{
    const string ConfigResourcePath = "HandCursorConfig";
    const string DefaultHandResourcePath = "Hand_Default";
    const string PressedHandResourcePath = "Hand_Pressed";

    static HandCursorPresenter _presenter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureForInitialScene()
    {
        OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!scene.IsValid())
        {
            return;
        }

        if (!ShouldUseHandCursor(scene.name))
        {
            DestroyPresenter();
            return;
        }

        if (_presenter != null)
        {
            _presenter.SetActiveState(true);
            return;
        }

        if (!TryLoadSprites(out Sprite defaultHand, out Sprite pressedHand, out float screenSize))
        {
            Debug.LogWarning("[HandCursor] Hand sprites could not be loaded. Hand cursor disabled.");
            return;
        }

        var presenterObject = new GameObject("HandCursor");
        _presenter = presenterObject.AddComponent<HandCursorPresenter>();
        _presenter.Configure(defaultHand, pressedHand, screenSize);
        Object.DontDestroyOnLoad(presenterObject);
    }

    static bool TryLoadSprites(out Sprite defaultHand, out Sprite pressedHand, out float screenSize)
    {
        defaultHand = null;
        pressedHand = null;
        screenSize = 220f;

        HandCursorConfig config = Resources.Load<HandCursorConfig>(ConfigResourcePath);
        if (config != null)
        {
            defaultHand = config.DefaultHand;
            pressedHand = config.PressedHand;
            screenSize = config.ScreenSize;
        }

        if (defaultHand == null)
        {
            defaultHand = Resources.Load<Sprite>(DefaultHandResourcePath);
        }

        if (pressedHand == null)
        {
            pressedHand = Resources.Load<Sprite>(PressedHandResourcePath);
        }

        return defaultHand != null && pressedHand != null;
    }

    static bool ShouldUseHandCursor(string sceneName)
    {
        return sceneName == GameSceneNames.Game
               || sceneName == GameSceneNames.Exercise
               || sceneName == "Onboarding";
    }

    static void DestroyPresenter()
    {
        if (_presenter == null)
        {
            Cursor.visible = true;
            return;
        }

        Object.Destroy(_presenter.gameObject);
        _presenter = null;
        Cursor.visible = true;
    }
}
