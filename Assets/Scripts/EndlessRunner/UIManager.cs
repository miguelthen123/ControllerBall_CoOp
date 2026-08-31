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

    private VRTrackMover trackMover;

    private void Start()
    {
        if (trackGenerator != null)
        {
            trackMover = trackGenerator.GetComponent<VRTrackMover>();
        }
    }

    private void Update()
    {
        if (trackGenerator == null) return;

        UpdateTimerUI();
        UpdateScoreUI();
        UpdateDebugUI();
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;

        switch (trackGenerator.CurrentState)
        {
            case TrackGenerator.GameState.WaitingToStart:
                timerText.text = "PRESS 'A' TO START";
                timerText.color = Color.green;
                break;

            case TrackGenerator.GameState.Playing:
                float time = trackGenerator.TimeRemaining;
                int minutes = Mathf.FloorToInt(time / 60f);
                int seconds = Mathf.FloorToInt(time % 60f);

                timerText.text = $"{minutes:00}:{seconds:00}";
                // Change color to yellow when under 10 seconds remaining
                timerText.color = (time <= 10f) ? Color.yellow : Color.white;
                break;

            case TrackGenerator.GameState.GameOver:
                int resetCountdown = Mathf.CeilToInt(trackGenerator.RestartCountdown);
                timerText.text = $"GAME OVER\n<size=70%>Press 'A' to Restart ({resetCountdown}s)</size>";
                timerText.color = Color.red;
                break;
        }
    }

    private void UpdateScoreUI()
    {
        if (scoreText == null) return;

        if (trackGenerator.CurrentState == TrackGenerator.GameState.GameOver)
        {
            scoreText.text = $"Final Score: {trackGenerator.PassedTracksCount}";
        }
        else
        {
            scoreText.text = $"Score: {trackGenerator.PassedTracksCount}";
        }
    }

    private void UpdateDebugUI()
    {
        if (debugStatusText == null || trackMover == null) return;

        debugStatusText.text = $"Status: {trackMover.LastDebugState}\n" +
                               $"Twist: {trackMover.CurrentYawDelta:F1}° / ±{trackMover.turnRotationThreshold}°";
    }
}