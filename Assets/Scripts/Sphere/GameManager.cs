using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Player Settings")]
    [SerializeField] private GameObject playerPrefab;
    [SerializeField] private GameObject activePlayer; // Assign if player already exists in scene

    [Header("World References")]
    [SerializeField] private Transform globeTransform;
    [SerializeField] private Rigidbody globeRigidbody;
    [SerializeField] private Transform startPoint;
    [SerializeField] private Transform endPoint;

    private float globeRadius;
    private Quaternion initialGlobeRotation; // Cache initial globe rotation

    private void Awake()
    {
        // Singleton setup
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (globeTransform == null)
        {
            GameObject globeObj = GameObject.FindWithTag("Globe");
            if (globeObj != null) globeTransform = globeObj.transform;
        }

        if (globeRigidbody == null && globeTransform != null)
        {
            globeRigidbody = globeTransform.GetComponent<Rigidbody>();
        }

        // Cache initial globe rotation and player sphere radius at start
        if (globeTransform != null)
        {
            initialGlobeRotation = globeTransform.rotation;

            if (startPoint != null)
            {
                globeRadius = Vector3.Distance(startPoint.position, globeTransform.position);
            }
        }

        // Spawn player if not already assigned
        if (activePlayer == null && playerPrefab != null)
        {
            SpawnPlayerAtStart();
        }
        else if (activePlayer != null)
        {
            ResetPlayerToStart();
        }
    }

    /// <summary>
    /// Instantiates a new player at the start point and resets world rotation.
    /// </summary>
    public void SpawnPlayerAtStart()
    {
        if (startPoint == null || playerPrefab == null)
        {
            Debug.LogError("GameManager: Start Point or Player Prefab is missing!");
            return;
        }

        ResetGlobeRotation();

        activePlayer = Instantiate(playerPrefab, startPoint.position, startPoint.rotation);
        
        if (globeTransform != null)
        {
            AlignPlayerToSphere(activePlayer.transform);
        }
    }

    /// <summary>
    /// Resets both the Globe and Player back to their initial starting positions & rotations.
    /// </summary>

    public void ResetPlayerToStart()
    {
        if (activePlayer == null || startPoint == null) return;

        // 1. Reset Globe back to its original starting rotation FIRST
        ResetGlobeRotation();

        // 2. Reset Player position and rotation
        activePlayer.transform.position = startPoint.position;
        activePlayer.transform.rotation = startPoint.rotation;

        if (globeTransform != null)
        {
            AlignPlayerToSphere(activePlayer.transform);
        }

        Debug.Log("Game Manager: Globe and Player reset to starting point.");
    }

    /// <summary>
    /// Resets the globe Rigidbody and Transform back to initial starting rotation.
    /// </summary>

    private void ResetGlobeRotation()
    {
        if (globeTransform == null) return;

        // Directly set transform rotation
        globeTransform.rotation = initialGlobeRotation;

        // If using Rigidbody physics rotation on globe, update it as well
        if (globeRigidbody != null)
        {
            globeRigidbody.MoveRotation(initialGlobeRotation);
            globeRigidbody.angularVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// Called when the player reaches the finish line / end point.
    /// </summary>
    public void OnPlayerReachedFinish()
    {
        Debug.Log("Player reached the end! Restarting game...");
        ResetPlayerToStart();
    }

    private void AlignPlayerToSphere(Transform playerT)
    {
        // Re-align player UP vector away from globe center
        Vector3 surfaceNormal = (playerT.position - globeTransform.position).normalized;
        playerT.rotation = Quaternion.FromToRotation(playerT.up, surfaceNormal) * playerT.rotation;

        // Ensure exact surface radius snapping
        if (globeRadius > 0f)
        {
            playerT.position = globeTransform.position + (surfaceNormal * globeRadius);
        }
    }
}