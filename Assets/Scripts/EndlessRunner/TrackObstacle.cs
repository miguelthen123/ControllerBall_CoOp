using UnityEngine;

public class TrackObstacle : MonoBehaviour
{
    public enum ControllerAxis
    {
        None,
        X_Pitch,
        Y_Yaw,
        Z_Roll
    }

    [Header("Controller Configuration")]
    public OVRInput.Controller obstacleController = OVRInput.Controller.LTouch;
    [SerializeField] private ControllerAxis inputAxis = ControllerAxis.Y_Yaw;

    [Header("Wrist Turn Controls")]
    [Tooltip("Minimum controller rotation between movement frames required to start accumulating a gesture.")]
    public float frameRotationThreshold = 1f;
    [Tooltip("Total controller rotation accumulated within the gesture window required to toggle the obstacle.")]
    public float turnRotationThreshold = 18f;
    [Tooltip("Maximum amount of time allowed for the wrist movement to accumulate enough rotation.")]
    public float yawSampleWindowSeconds = 0.25f;
    [Tooltip("After triggering, the controller must return below this amount of movement before another gesture can trigger.")]
    public float resetThresholdDegrees = 5f;

    [Header("Obstacle Parts & Rotation")]
    [Tooltip("The moving part of the obstacle.")]
    public Transform obstaclePart;
    [Tooltip("Optional pivot that the obstacle rotates around.")]
    public Transform rotationPivot;
    public float openAngle = 90f;
    public float closeAngle = 0f;
    public float rotationSpeed = 300f;

    [Header("Randomization Settings")]
    [Range(0f, 100f)] public float obstacleSpawnChance = 50f;
    [Range(0f, 100f)] public float startClosedChance = 50f;

    [Header("References")]
    public VRTrackMover trackMover;

    [Header("Debug")]
    public bool debugInput = true;

    // State
    private bool hasObstacle = true;
    private bool isOpen = false;
    private bool playerIsWaiting = false;
    private BoxCollider triggerCollider;
    private Transform activeTransform;
    private Quaternion baseLocalRotation;
    private float currentAngleY = 0f;
    private float targetAngleY = 0f;

    // Controller Gesture State
    private bool controllerInitialized = false;
    private float previousControllerAngle = 0f;
    private float accumulatedRotation = 0f;
    private float gestureStartTime = 0f;
    private bool canTrigger = true;

    private void Start()
    {
        if (trackMover == null) trackMover = FindObjectOfType<VRTrackMover>();
        triggerCollider = GetComponent<BoxCollider>();

        if (rotationPivot != null) activeTransform = rotationPivot;
        else if (obstaclePart != null) activeTransform = obstaclePart;

        if (activeTransform == null)
        {
            Debug.LogError($"[TrackObstacle] {gameObject.name}: No obstaclePart or rotationPivot assigned!");
            return;
        }

        baseLocalRotation = activeTransform.localRotation;

        if (rotationPivot != null && obstaclePart != null && !obstaclePart.IsChildOf(rotationPivot))
        {
            Debug.LogWarning($"[TrackObstacle] WARNING: '{obstaclePart.name}' is NOT a child of '{rotationPivot.name}'.");
        }

        // Randomize existence
        if (Random.Range(0f, 100f) > obstacleSpawnChance)
        {
            hasObstacle = false;
            if (obstaclePart != null) Destroy(obstaclePart.gameObject);
            if (rotationPivot != null && rotationPivot != obstaclePart) Destroy(rotationPivot.gameObject);
            if (triggerCollider != null) Destroy(triggerCollider);
            Debug.Log("<color=grey>[TrackObstacle] No obstacle spawned.</color>");
            return;
        }

        // Randomize initial state
        isOpen = Random.Range(0f, 100f) > startClosedChance;
        targetAngleY = isOpen ? openAngle : closeAngle;
        currentAngleY = targetAngleY;

        ApplyRotationImmediate(currentAngleY);
        InitializeControllerInput();

        Debug.Log($"<color=cyan>[TrackObstacle] Obstacle spawned. Initial State: {(isOpen ? "OPEN" : "CLOSED")}</color>");
    }

    private void Update()
    {
        if (!hasObstacle || activeTransform == null) return;

        if (OVRInput.IsControllerConnected(obstacleController) &&
            OVRInput.GetControllerOrientationTracked(obstacleController))
        {
            CheckFrameToFrameGesture();
        }

        SmoothRotateObstacle();
    }

    private void InitializeControllerInput()
    {
        if (!OVRInput.IsControllerConnected(obstacleController))
        {
            controllerInitialized = false;
            return;
        }

        previousControllerAngle = ReadRawAngle(inputAxis);
        accumulatedRotation = 0f;
        controllerInitialized = true;
    }

    private void CheckFrameToFrameGesture()
    {
        float currentControllerAngle = ReadRawAngle(inputAxis);

        if (!controllerInitialized)
        {
            previousControllerAngle = currentControllerAngle;
            controllerInitialized = true;
            return;
        }

        float frameDelta = Mathf.DeltaAngle(previousControllerAngle, currentControllerAngle);
        previousControllerAngle = currentControllerAngle;

        if (Mathf.Abs(frameDelta) < frameRotationThreshold)
        {
            HandleGestureReset();
            return;
        }

        if (Mathf.Abs(accumulatedRotation) < 0.001f)
        {
            gestureStartTime = Time.time;
        }

        accumulatedRotation += frameDelta;

        if (debugInput)
        {
            Debug.Log($"[TrackObstacle] Frame Delta: {frameDelta:F2}° | Accumulated: {accumulatedRotation:F2}°");
        }

        if (Time.time - gestureStartTime > yawSampleWindowSeconds)
        {
            if (debugInput) Debug.Log($"[TrackObstacle] Gesture expired. Accumulated: {accumulatedRotation:F2}°");
            accumulatedRotation = 0f;
            gestureStartTime = Time.time;
            return;
        }

        if (canTrigger && Mathf.Abs(accumulatedRotation) >= turnRotationThreshold)
        {
            ToggleObstacleState();
            canTrigger = false;
            accumulatedRotation = 0f;
        }
    }

    private void HandleGestureReset()
    {
        if (canTrigger) return;

        if (Mathf.Abs(accumulatedRotation) <= resetThresholdDegrees)
        {
            canTrigger = true;
            accumulatedRotation = 0f;
            if (debugInput) Debug.Log("[TrackObstacle] Gesture reset. Ready for next wrist turn.");
        }
    }

    private void ToggleObstacleState()
    {
        isOpen = !isOpen;
        targetAngleY = isOpen ? openAngle : closeAngle;

        Debug.Log($"<color=cyan>[TrackObstacle] OBSTACLE TOGGLED → {(isOpen ? "OPEN" : "CLOSED")} Target Angle: {targetAngleY}°</color>");

        if (isOpen && playerIsWaiting && trackMover != null)
        {
            playerIsWaiting = false;
            trackMover.ResumeTrack();
            Debug.Log("<color=green>[TrackObstacle] Obstacle opened while waiting. Resuming track.</color>");
        }
    }

    /// <summary>
    /// Forces this obstacle to close. Usually invoked by VRTrackMover on tracking loss.
    /// </summary>
    public void ForceCloseObstacle()
    {
        if (!hasObstacle) return;

        isOpen = false;
        targetAngleY = closeAngle;

        // Reset gesture accumulation state
        accumulatedRotation = 0f;
        canTrigger = true;

        Debug.LogWarning($"[TrackObstacle] {gameObject.name} forced CLOSED.");
    }

    private void SmoothRotateObstacle()
    {
        if (activeTransform == null || Mathf.Approximately(currentAngleY, targetAngleY)) return;

        currentAngleY = Mathf.MoveTowards(currentAngleY, targetAngleY, rotationSpeed * Time.deltaTime);
        ApplyRotationImmediate(currentAngleY);
    }

    private void ApplyRotationImmediate(float angleY)
    {
        if (activeTransform == null) return;
        Quaternion rotationOffset = Quaternion.Euler(0f, angleY, 0f);
        activeTransform.localRotation = baseLocalRotation * rotationOffset;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!hasObstacle || trackMover == null) return;

        bool isPlayer = other.CompareTag("Player") ||
                        other.transform == trackMover.playerTransform ||
                        (trackMover.playerTransform != null && other.transform.IsChildOf(trackMover.playerTransform));

        if (!isPlayer) return;

        if (!isOpen)
        {
            playerIsWaiting = true;
            trackMover.StopTrack();
            Debug.LogWarning("<color=orange>[TrackObstacle] Player reached CLOSED obstacle. Stopping track!</color>");
        }
    }

    private float ReadRawAngle(ControllerAxis axis)
    {
        if (axis == ControllerAxis.None) return 0f;

        Quaternion controllerRotation = OVRInput.GetLocalControllerRotation(obstacleController);
        Vector3 euler = controllerRotation.eulerAngles;

        return axis switch
        {
            ControllerAxis.X_Pitch => NormalizeAngle(euler.x),
            ControllerAxis.Y_Yaw => NormalizeAngle(euler.y),
            ControllerAxis.Z_Roll => NormalizeAngle(euler.z),
            _ => 0f
        };
    }

    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}