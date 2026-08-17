using UnityEngine;

public class TrackTurnTrigger : MonoBehaviour
{
    public enum JunctionType
    {
        LeftTurnOnly,
        RightTurnOnly,
        TJunction
    }

    [Header("Junction Setup")]
    public JunctionType junctionType = JunctionType.TJunction;

    [Header("Custom Turn Angles (Degrees)")]
    public float leftTurnAngle = -90f;
    public float rightTurnAngle = 90f;

    [Header("Stop Settings")]
    public bool stopOnEnter = true;

    [Header("Custom Pivot Override")]
    [Tooltip("Optional: Drag an empty GameObject here to serve as the exact center point for the rotation pivot. If left empty, the trigger's position will be used.")]
    public Transform turnPivot;

    private bool playerInside = false;
    private bool hasHandledTurn = false;
    private VRTrackMover trackMover;
    private Collider triggerCollider;

    private void Awake()
    {
        triggerCollider = GetComponent<Collider>();
        if (triggerCollider != null && !triggerCollider.isTrigger)
        {
            Debug.LogWarning($"[TurnTrigger] Collider on '{gameObject.name}' was not set to Is Trigger! Fixing automatically.");
            triggerCollider.isTrigger = true;
        }
    }

    private void Start()
    {
        trackMover = FindFirstObjectByType<VRTrackMover>();
    }

    private void OnEnable()
    {
        hasHandledTurn = false;
        playerInside = false;
        if (triggerCollider != null)
        {
            triggerCollider.enabled = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHandledTurn) return;

        if (IsPlayer(other))
        {
            playerInside = true;
            Debug.Log($"<color=yellow>[TurnTrigger] Player entered trigger on '{gameObject.name}'. Stopping track.</color>");

            if (stopOnEnter && trackMover != null && !trackMover.IsInCooldown)
            {
                trackMover.StopTrack();
            }
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (hasHandledTurn) return;

        if (IsPlayer(other))
        {
            playerInside = true;

            if (stopOnEnter && trackMover != null && !trackMover.IsStopped && !trackMover.IsTurning && !trackMover.IsInCooldown)
            {
                trackMover.StopTrack();
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other))
        {
            if (trackMover != null && trackMover.IsTurning) return;

            // If the player somehow exits without turning, ensure we stop so they don't slide away
            if (!hasHandledTurn && trackMover != null && !trackMover.IsStopped && !trackMover.IsInCooldown)
            {
                trackMover.StopTrack();
            }
            playerInside = false;
        }
    }

    private void Update()
    {
        if (trackMover == null || hasHandledTurn) return;

        // Process turn input if player is inside trigger OR if track is halted at this junction
        if (playerInside || trackMover.IsStopped)
        {
            int turnInput = trackMover.GetControllerTurnDirection();

            bool canTurnLeft = junctionType == JunctionType.LeftTurnOnly || junctionType == JunctionType.TJunction;
            bool canTurnRight = junctionType == JunctionType.RightTurnOnly || junctionType == JunctionType.TJunction;

            if (turnInput == -1 && canTurnLeft)
            {
                ExecuteTurn(leftTurnAngle);
            }
            else if (turnInput == 1 && canTurnRight)
            {
                ExecuteTurn(rightTurnAngle);
            }
        }
    }

    private void ExecuteTurn(float angle)
    {
        hasHandledTurn = true;
        playerInside = false;

        if (triggerCollider != null)
        {
            triggerCollider.enabled = false;
        }

        if (trackMover != null)
        {
            Vector3 pivotPosition = (turnPivot != null) ? turnPivot.position : transform.position;
            trackMover.RotateTrackAroundPlayer(angle, pivotPosition);
        }

        Debug.Log($"<color=green>[TurnTrigger] Turn ({angle}°) executed on '{gameObject.name}'.</color>");
    }

    private bool IsPlayer(Collider other)
    {
        if (trackMover != null && trackMover.playerTransform != null)
        {
            bool isTransformMatch = other.transform == trackMover.playerTransform || other.transform.IsChildOf(trackMover.playerTransform);
            bool isTagMatch = other.CompareTag("Player");
            return isTransformMatch || isTagMatch;
        }
        return other.CompareTag("Player");
    }
}