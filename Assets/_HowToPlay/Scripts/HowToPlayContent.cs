using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "HowToPlayContent", menuName = "PennyBall/How To Play Content")]
public class HowToPlayContent : ScriptableObject
{
    [Serializable]
    public class Page
    {
        [Tooltip("Lokalizasyon anahtarı, ör. how_to_play.page1.title")]
        public string titleKey;
        [Tooltip("Lokalizasyon anahtarı, ör. how_to_play.page1.body")]
        public string bodyKey;
        public Sprite image;
        [Tooltip("Doluysa Image yerine bu prefab gösterilir (animasyonlu anlatım için).")]
        public GameObject visualPrefab;
    }

    [SerializeField] List<Page> _pages = new();

    public IReadOnlyList<Page> Pages => _pages;
}
