using UnityEngine;

public static class HandCursorBootstrap
{
    static HandCursorPresenter _presenter;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void DisableHandCursor()
    {
        DestroyPresenter();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void EnsureSystemCursorVisible()
    {
        DestroyPresenter();
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
