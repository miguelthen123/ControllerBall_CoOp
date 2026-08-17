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

    private List<GameObject> activeTrackSegments = new List<GameObject>();
    // FIX 3: Keep track of ALL open exits so branches generate properly
    private List<Transform> openExits = new List<Transform>(); 
    
    private VRTrackMover trackMover;
    private int totalSpawnWeight;
    private bool lastSpawnedWasSpecial = false;

    private void Start()
    {
        if (playerTransform == null) return;

        trackMover = GetComponent<VRTrackMover>();
        CalculateTotalWeights();

        Vector3 spawnOrigin = playerTransform.TransformPoint(initialSpawnOffset);
        GameObject firstSegment = Instantiate(initialTrackPrefab, spawnOrigin, playerTransform.rotation, transform);
        activeTrackSegments.Add(firstSegment);

        TrackPoints trackData = firstSegment.GetComponentInChildren<TrackPoints>();
        if (trackData != null && trackData.EntryPoint != null)
        {
            Vector3 entryOffset = spawnOrigin - trackData.EntryPoint.position;
            firstSegment.transform.position += entryOffset;
            
            // Add initial exits
            if (trackData.ExitPoints != null && trackData.ExitPoints.Length > 0)
                openExits.AddRange(trackData.ExitPoints);
            else
                openExits.Add(firstSegment.transform);
        }

        for (int i = 0; i < totalAheadSegments - 1; i++)
        {
            SpawnNextSegment();
        }
    }

    private void Update()
    {
        if (playerTransform == null || openExits.Count == 0 || trackMover == null) return;
        if (trackMover.IsStopped) return;

        // Check if ANY of our open branch exits are getting close to the player
        bool needsSpawn = false;
        foreach (var exit in openExits)
        {
            if (exit != null)
            {
                // CHANGE FROM THIS:
                // float exitZDistance = playerTransform.InverseTransformPoint(exit.position).z;
                // if (exitZDistance <= spawnTriggerDistance)

                // TO THIS (3D distance regardless of turns/orientation):
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

    private void SpawnNextSegment()
    {

        Debug.Log($"Spawning triggered. Current open exits: {openExits.Count}");
        if (openExits.Count == 0)
        {
            Debug.LogWarning("Generation stopped: openExits list is empty!");
            return;
        }
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

            // UNIVERSAL ALIGNMENT: 
            // 1. Calculate the rotation difference needed so EntryPoint rotation matches ExitPoint rotation exactly.
            Quaternion targetRotation = exitToAttachTo.rotation * Quaternion.Inverse(trackData.EntryPoint.rotation);
            newSegment.transform.rotation = targetRotation * newSegment.transform.rotation;

            // 2. Calculate the position difference so EntryPoint position snaps to ExitPoint position exactly.
            Vector3 positionOffset = exitToAttachTo.position - trackData.EntryPoint.position;
            newSegment.transform.position += positionOffset;

            if (selectedItem.isSpecialTrack) spawnedSpecial = true;
            activeTrackSegments.Add(newSegment);

            // Collect all new exit points for the next generation cycle
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
        // FIX 2: Iterate backwards and use distance-based cleanup!
        for (int i = activeTrackSegments.Count - 1; i >= 0; i--)
        {
            GameObject segment = activeTrackSegments[i];
            if (segment == null)
            {
                activeTrackSegments.RemoveAt(i);
                continue;
            }

            // Calculate distance behind player on the Z axis
            float zDistance = playerTransform.InverseTransformPoint(segment.transform.position).z;

            if (zDistance < -cleanupDistanceBehindPlayer)
            {
                activeTrackSegments.RemoveAt(i);
                Destroy(segment); // Now it actually destroys the GameObject!
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
        if (trackMover == null) return;
        GUIStyle style = new GUIStyle();
        style.fontSize = 22;
        style.normal.textColor = Color.cyan;
        GUILayout.BeginArea(new Rect(30, 30, 600, 200));
        GUILayout.Label($"Status: {trackMover.LastDebugState}", style);
        GUILayout.Label($"Wrist Twist: {trackMover.CurrentYawDelta:F1}° (Req: ±{trackMover.turnRotationThreshold}°)", style);
        GUILayout.EndArea();
    }
}