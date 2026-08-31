using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[System.Serializable]
public class TrackPrefabWeight
{
    public GameObject prefab;
    public int spawnWeight = 10;
    [Tooltip("Check true for Left Turns, Right Turns, or T-Junctions.")]
    public bool isSpecialTrack = false;
}

public class TrackGenerator : MonoBehaviour
{
    public enum GameState { WaitingToStart, Playing, GameOver }

    [Header("Track Prefabs Configuration")]
    public GameObject initialTrackPrefab;
    public List<TrackPrefabWeight> weightedTrackPrefabs;

    [Header("Player & Offset Config")]
    public Transform playerTransform;
    public Vector3 initialSpawnOffset = new Vector3(0f, -1.5f, 0f);

    [Header("Generation Settings")]
    public int totalAheadSegments = 5;
    public float spawnTriggerDistance = 15f;
    [Tooltip("Distance behind player before track is destroyed")]
    public float cleanupDistanceBehindPlayer = 25f; 

    [Header("Game Timer & Restart Settings")]
    [Tooltip("Duration of the game session in seconds.")]
    public float gameDurationSeconds = 60f; 
    [Tooltip("Timeout in seconds during Game Over before resetting back to main menu state.")]
    public float autoResetTimeoutSeconds = 10f;

    [Header("Scene Transition Settings")]
    [Tooltip("Name of the scene to load when pressing B on the Right Controller.")]
    public string targetSceneName;

    [Header("Game Status")]
    public GameState CurrentState { get; private set; } = GameState.WaitingToStart;
    public float TimeRemaining { get; private set; }
    public int PassedTracksCount { get; private set; }
    public float RestartCountdown { get; private set; } = 0f;

    private List<GameObject> activeTrackSegments = new List<GameObject>();
    private HashSet<GameObject> passedTrackSegments = new HashSet<GameObject>();
    private List<Transform> openExits = new List<Transform>(); 
    
    private VRTrackMover trackMover;
    private int totalSpawnWeight;
    private bool lastSpawnedWasSpecial = false;
    private Coroutine gameOverTimeoutCoroutine;

    private void Start()
    {
        trackMover = GetComponent<VRTrackMover>();
        CalculateTotalWeights();
        ResetToWaitingState();
    }

    private void Update()
    {
        // 1. Check for Scene Switch input (B Button) anytime
        CheckSceneSwitchInput();

        // 2. Handle state transitions with A Button
        HandleButtonInputs();

        // 3. Game Loop only runs during Playing state
        if (CurrentState != GameState.Playing) return;

        UpdateGameTimer();

        if (playerTransform == null || openExits.Count == 0 || trackMover == null) return;
        if (trackMover.IsStopped) return;

        CheckPassedSegments();

        bool needsSpawn = false;
        foreach (var exit in openExits)
        {
            if (exit != null)
            {
                float distanceToExit = Vector3.Distance(playerTransform.position, exit.position);
                if (distanceToExit <= spawnTriggerDistance)
                {
                    needsSpawn = true;
                    break;
                }
            }
        }

        if (needsSpawn)
        {
            SpawnNextSegment();
            CleanupBehindSegments();
        }
    }

    private void HandleButtonInputs()
    {
        // Button.One targets the 'A' button on the Right Touch Controller in Meta XR SDK
        if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
        {
            if (CurrentState == GameState.WaitingToStart)
            {
                StartGame();
            }
            else if (CurrentState == GameState.GameOver)
            {
                if (gameOverTimeoutCoroutine != null) StopCoroutine(gameOverTimeoutCoroutine);
                StartGame();
            }
        }
    }

    private void CheckSceneSwitchInput()
    {
        // Button.Two targets the 'B' button on the Right Touch Controller in Meta XR SDK
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            LoadTargetScene();
        }
    }

    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("Target Scene Name is empty! Assign it in the TrackGenerator Inspector.");
        }
    }

    private void ResetToWaitingState()
    {
        CurrentState = GameState.WaitingToStart;
        RebuildTrack();
        
        TimeRemaining = gameDurationSeconds;
        PassedTracksCount = 0;

        if (trackMover != null)
        {
            trackMover.StopTrack();
        }
    }

    private void StartGame()
    {
        CurrentState = GameState.Playing;
        RebuildTrack();

        TimeRemaining = gameDurationSeconds;
        PassedTracksCount = 0;

        if (trackMover != null)
        {
            trackMover.ResumeTrack();
        }
    }

    private void RebuildTrack()
    {
        // Clear existing segments
        foreach (GameObject segment in activeTrackSegments)
        {
            if (segment != null) Destroy(segment);
        }

        activeTrackSegments.Clear();
        passedTrackSegments.Clear();
        openExits.Clear();
        lastSpawnedWasSpecial = false;

        if (playerTransform == null) return;

        // Spawn Initial Segment
        Vector3 spawnOrigin = playerTransform.TransformPoint(initialSpawnOffset);
        GameObject firstSegment = Instantiate(initialTrackPrefab, spawnOrigin, playerTransform.rotation, transform);
        activeTrackSegments.Add(firstSegment);

        TrackPoints trackData = firstSegment.GetComponentInChildren<TrackPoints>();
        if (trackData != null && trackData.EntryPoint != null)
        {
            Vector3 entryOffset = spawnOrigin - trackData.EntryPoint.position;
            firstSegment.transform.position += entryOffset;
            
            if (trackData.ExitPoints != null && trackData.ExitPoints.Length > 0)
                openExits.AddRange(trackData.ExitPoints);
            else
                openExits.Add(firstSegment.transform);
        }

        // Pre-spawn initial set of tracks ahead
        for (int i = 0; i < totalAheadSegments - 1; i++)
        {
            SpawnNextSegment();
        }
    }

    private void UpdateGameTimer()
    {
        if (TimeRemaining > 0f)
        {
            TimeRemaining -= Time.deltaTime;
            if (TimeRemaining <= 0f)
            {
                TimeRemaining = 0f;
                TriggerGameOver();
            }
        }
    }

    private void TriggerGameOver()
    {
        CurrentState = GameState.GameOver;

        if (trackMover != null)
        {
            trackMover.StopTrack(); 
        }

        Debug.Log($"Time Up! Game Over. Total Tracks Passed: {PassedTracksCount}");

        if (gameOverTimeoutCoroutine != null) StopCoroutine(gameOverTimeoutCoroutine);
        gameOverTimeoutCoroutine = StartCoroutine(GameOverTimeoutRoutine());
    }

    private IEnumerator GameOverTimeoutRoutine()
    {
        RestartCountdown = autoResetTimeoutSeconds;

        while (RestartCountdown > 0f)
        {
            yield return null;
            RestartCountdown -= Time.deltaTime;
        }

        RestartCountdown = 0f;
        
        // If user pressed nothing during countdown, revert back to waiting start state
        ResetToWaitingState();
    }

    private void CheckPassedSegments()
    {
        foreach (GameObject segment in activeTrackSegments)
        {
            if (segment == null || passedTrackSegments.Contains(segment)) continue;

            float zDistance = playerTransform.InverseTransformPoint(segment.transform.position).z;

            if (zDistance < 0f)
            {
                passedTrackSegments.Add(segment);
                PassedTracksCount++;
            }
        }
    }

    private void SpawnNextSegment()
    {
        if (openExits.Count == 0) return;

        List<Transform> newExits = new List<Transform>();
        bool spawnedSpecial = false;

        foreach (Transform exitToAttachTo in openExits)
        {
            if (exitToAttachTo == null) continue;

            TrackPrefabWeight selectedItem = GetValidPrefab();
            if (selectedItem == null || selectedItem.prefab == null) continue;

            GameObject newSegment = Instantiate(selectedItem.prefab, transform);
            TrackPoints trackData = newSegment.GetComponentInChildren<TrackPoints>();
            
            if (trackData == null || trackData.EntryPoint == null)
            {
                Destroy(newSegment);
                continue;
            }

            Quaternion targetRotation = exitToAttachTo.rotation * Quaternion.Inverse(trackData.EntryPoint.rotation);
            newSegment.transform.rotation = targetRotation * newSegment.transform.rotation;

            Vector3 positionOffset = exitToAttachTo.position - trackData.EntryPoint.position;
            newSegment.transform.position += positionOffset;

            if (selectedItem.isSpecialTrack) spawnedSpecial = true;
            activeTrackSegments.Add(newSegment);

            if (trackData.ExitPoints != null && trackData.ExitPoints.Length > 0)
            {
                newExits.AddRange(trackData.ExitPoints);
            }
            else
            {
                newExits.Add(newSegment.transform);
            }
        }

        lastSpawnedWasSpecial = spawnedSpecial;
        openExits = newExits;
    }

    private void CleanupBehindSegments()
    {
        for (int i = activeTrackSegments.Count - 1; i >= 0; i--)
        {
            GameObject segment = activeTrackSegments[i];
            if (segment == null)
            {
                activeTrackSegments.RemoveAt(i);
                continue;
            }

            float zDistance = playerTransform.InverseTransformPoint(segment.transform.position).z;

            if (zDistance < -cleanupDistanceBehindPlayer)
            {
                passedTrackSegments.Remove(segment);
                activeTrackSegments.RemoveAt(i);
                Destroy(segment);
            }
        }
    }

    private TrackPrefabWeight GetValidPrefab()
    {
        if (weightedTrackPrefabs == null || weightedTrackPrefabs.Count == 0) return null;

        if (lastSpawnedWasSpecial)
        {
            TrackPrefabWeight straightItem = weightedTrackPrefabs.Find(x => !x.isSpecialTrack);
            if (straightItem != null) return straightItem;
        }

        int randomValue = Random.Range(0, totalSpawnWeight);
        int currentWeightSum = 0;

        foreach (var item in weightedTrackPrefabs)
        {
            currentWeightSum += item.spawnWeight;
            if (randomValue < currentWeightSum) return item;
        }
        return weightedTrackPrefabs[0];
    }

    private void CalculateTotalWeights()
    {
        totalSpawnWeight = 0;
        foreach (var item in weightedTrackPrefabs) totalSpawnWeight += item.spawnWeight;
    }

    private void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.normal.textColor = Color.cyan;

        GUILayout.BeginArea(new Rect(30, 30, 600, 350));
        
        if (trackMover != null)
        {
            GUILayout.Label($"Status: {trackMover.LastDebugState}", style);
            GUILayout.Label($"Wrist Twist: {trackMover.CurrentYawDelta:F1}° (Req: ±{trackMover.turnRotationThreshold}°)", style);
        }

        if (CurrentState == GameState.WaitingToStart)
        {
            style.fontSize = 26;
            style.normal.textColor = Color.green;
            GUILayout.Label("PRESS 'A' TO START GAME", style);
        }
        else if (CurrentState == GameState.Playing)
        {
            style.normal.textColor = Color.yellow;
            GUILayout.Label($"Time Remaining: {Mathf.CeilToInt(TimeRemaining)}s", style);
            
            style.normal.textColor = Color.green;
            GUILayout.Label($"Tracks Passed: {PassedTracksCount}", style);
        }
        else if (CurrentState == GameState.GameOver)
        {
            style.fontSize = 28;
            style.normal.textColor = Color.red;
            GUILayout.Label("TIME UP! GAME OVER", style);
            
            style.fontSize = 22;
            style.normal.textColor = Color.yellow;
            GUILayout.Label($"Final Score: {PassedTracksCount} Tracks", style);

            style.normal.textColor = Color.green;
            GUILayout.Label("PRESS 'A' TO RESTART", style);

            style.normal.textColor = Color.white;
            GUILayout.Label($"Returning to start in: {Mathf.CeilToInt(RestartCountdown)}s", style);
        }

        GUILayout.EndArea();
    }
}