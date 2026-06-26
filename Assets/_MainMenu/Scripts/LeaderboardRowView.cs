using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowView : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI _orderText;
    [SerializeField] TextMeshProUGUI _nameText;
    [SerializeField] TextMeshProUGUI _scoreText;
    [SerializeField] GameObject _playerHighlight;

    public void Bind(int rank, string displayName, int points, bool isPlayer)
    {
        if (_orderText != null) _orderText.SetText(rank.ToString());
        if (_nameText != null)  _nameText.SetText(displayName);
        if (_scoreText != null) _scoreText.SetText(points.ToString());
        if (_playerHighlight != null) _playerHighlight.SetActive(isPlayer);
    }
}
