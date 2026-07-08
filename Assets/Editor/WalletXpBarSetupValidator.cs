using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class WalletXpBarSetupValidator
{
    [MenuItem("PennyBall/Wallet/Validate XP Bar Setup")]
    public static void Validate()
    {
        WalletData data = Application.isPlaying ? WalletService.Data : WalletRepository.Load();
        int level = PlayerLevelProgression.GetLevelFromTotalXp(data.totalXp);
        float progress = PlayerLevelProgression.GetProgressInCurrentLevel(data.totalXp);

        Debug.Log($"[XP Bar] Wallet → totalXp={data.totalXp}, level={level}, progress={progress:P1}");

        WalletPresenter presenter = Object.FindAnyObjectByType<WalletPresenter>(FindObjectsInactive.Include);
        if (presenter == null)
        {
            Debug.LogError("[XP Bar] WalletPresenter bulunamadı. Canvas > Top_Bar_Indicators > Container");
            return;
        }

        Debug.Log($"[XP Bar] WalletPresenter bulundu: {GetPath(presenter.transform)}");

        Transform levelBar = presenter.transform.Find("LevelBarContainer/LevelBar");
        if (levelBar == null)
        {
            Debug.LogError("[XP Bar] LevelBar bulunamadı.");
            return;
        }

        Transform fillTrack = levelBar.Find("FillTrack");
        Transform levelStatus = fillTrack != null ? fillTrack.Find("LevelStatus") : levelBar.Find("LevelStatus");
        if (levelStatus == null)
        {
            Debug.LogError("[XP Bar] LevelStatus bulunamadı. LevelBar altında FillTrack/LevelStatus olmalı.");
            return;
        }

        LevelXpBarFill fill = levelStatus.GetComponent<LevelXpBarFill>();
        if (fill == null)
        {
            Debug.LogError("[XP Bar] LevelStatus üzerinde LevelXpBarFill yok.");
            return;
        }

        Image fillImage = levelStatus.GetComponent<Image>();
        if (fillImage == null || fillImage.sprite == null)
        {
            Debug.LogError("[XP Bar] LevelStatus Image veya sprite eksik.");
        }

        if (fillTrack != null && fillTrack.GetComponent<RectMask2D>() == null)
        {
            Debug.LogWarning("[XP Bar] FillTrack üzerinde RectMask2D yok (fill taşabilir).");
        }

        if (Application.isPlaying)
        {
            fill.Refresh();
            RectTransform fillRect = levelStatus as RectTransform;
            Debug.Log($"[XP Bar] Fill anchorMax.x={fillRect.anchorMax.x:0.###} (beklenen ~{progress:0.###})");
        }
        else
        {
            Debug.Log("[XP Bar] Play modunda fill değerini canlı görmek için oyunu başlat.");
        }

        Selection.activeGameObject = levelStatus.gameObject;
        EditorGUIUtility.PingObject(levelStatus.gameObject);
    }

    static string GetPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
