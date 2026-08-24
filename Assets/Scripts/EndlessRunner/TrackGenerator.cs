using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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
    [Tooltip("Delay in seconds before auto-restarting after Game Over.")]
    public float restartDelaySeconds = 10f;

    [Header("Game Status")]
    public float TimeRemaining { get; private set; }
    public int PassedTracksCount { get; private set; }
    public bool IsGameOver { get; private set; } = false;
    public float RestartCountdown { get; private set; } = 0f;

    private List<GameObject> activeTrackSegments = new List<GameObject>();
    private HashSet<GameObject> passedTrackSegments = new HashSet<GameObject>();
    private List<Transform> openExits = new List<Transform>(); 
    
    private VRTrackMover trackMover;
    private int totalSpawnWeight;
    private bool lastSpawnedWasSpecial = false;
    private Coroutine restartCoroutine;

    private void Start()
    {
        trackMover = GetComponent<VRTrackMover>();
        CalculateTotalWeights();
        InitializeGame();
    }

    private void Update()
    {
        if (IsGameOver) return;

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

    private void InitializeGame()
    {
        // 1. Clear old track game objects if restarting
        foreach (GameObject segment in activeTrackSegments)
        {
            if (segment != null) Destroy(segment);
        }

        // 2. Reset tracking lists & state values
        activeTrackSegments.Clear();
        passedTrackSegments.Clear();
        openExits.Clear();

        TimeRemaining = gameDurationSeconds;
        PassedTracksCount = 0;
        IsGameOver = false;
        lastSpawnedWasSpecial = false;

        if (playerTransform == null) return;

        // 3. Spawn Initial Segment
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

        // 4. Pre-spawn ahead tracks
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
        IsGameOver = true;
        if (trackMover != null)
        {
            trackMover.StopTrack(); 
        }

        Debug.Log($"Time Up! Game Over. Total Tracks Passed: {PassedTracksCount}");

        if (restartCoroutine != null) StopCoroutine(restartCoroutine);
        restartCoroutine = StartCoroutine(RestartRoutine());
    }

    private IEnumerator RestartRoutine()
    {
        RestartCountdown = restartDelaySeconds;

        while (RestartCountdown > 0f)
        {
            yield return null;
            RestartCountdown -= Time.deltaTime;
        }

        RestartCountdown = 0f;
        InitializeGame();

        // Re-enable track movement if applicable
        if (trackMover != null)
        {
            trackMover.ResumeTrack(); // Adjust to your VRTrackMover start/reset method name if needed
        }
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

        GUILayout.BeginArea(new Rect(30, 30, 600, 300));
        
        if (trackMover != null)
        {
            GUILayout.Label($"Status: {trackMover.LastDebugState}", style);
            GUILayout.Label($"Wrist Twist: {trackMover.CurrentYawDelta:F1}° (Req: ±{trackMover.turnRotationThreshold}°)", style);
        }

        style.normal.textColor = IsGameOver ? Color.red : Color.yellow;
        GUILayout.Label($"Time Remaining: {Mathf.CeilToInt(TimeRemaining)}s", style);
        
        style.normal.textColor = Color.green;
        GUILayout.Label($"Tracks Passed: {PassedTracksCount}", style);

        if (IsGameOver)
        {
            style.fontSize = 28;
            style.normal.textColor = Color.red;
            GUILayout.Label("TIME UP! GAME OVER", style);
            
            style.fontSize = 22;
            style.normal.textColor = Color.white;
            GUILayout.Label($"Restarting in: {Mathf.CeilToInt(RestartCountdown)}s", style);
        }

        GUILayout.EndArea();
    }
}