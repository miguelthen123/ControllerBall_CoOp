using UnityEngine;
using TMPro;

public class UIManager : MonoBehaviour
{
    [Header("Generator Reference")]
    [SerializeField] private TrackGenerator trackGenerator;

    [Header("TextMeshPro UI References")]
    [SerializeField] private TextMeshProUGUI debugStatusText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI scoreText;

    private void Update()
    {
        if (trackGenerator == null) return;

        UpdateTimerUI();
        UpdateScoreUI();
    }


    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        if (trackGenerator.IsGameOver)
        {
            timerText.text = "GAME OVER";
            timerText.color = Color.red;
            return;
        }

        float time = trackGenerator.TimeRemaining;
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        timerText.color = Color.black;
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    private void UpdateScoreUI()
    {
        if (scoreText == null) return;
        scoreText.text = $"Score: {trackGenerator.PassedTracksCount}";
    }
}