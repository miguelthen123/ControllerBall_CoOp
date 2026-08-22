using System.Collections.Generic;
using UnityEngine;

public class VRTrackMover : MonoBehaviour
{
    public enum StopReason
    {
        NotStopped,
        StoppedAtObstacle,
        StoppedAtJunction
    }

    [Header("Meta XR Controller Configuration")]
    public OVRInput.Controller inputController = OVRInput.Controller.RTouch;

    [Header("Forward Speed (Controller Pitch)")]
    public float maxSpeed = 10f;
    public float maxTiltAngle = 45f;
    public float deadzoneAngle = 5f;

    [Header("Wrist Turn Controls")]
    [Tooltip("Degrees of controller yaw rotation over sample window required to trigger a turn")]
    public float turnRotationThreshold = 18f;
    public float yawSampleWindowSeconds = 0.25f;

    [Header("Rotation Motion")]
    public float turnRotationSpeed = 250f;

    [Header("Player Reference")]
    public Transform playerTransform;

    private float currentSpeed = 0f;
    private bool isTurning = false;
    private StopReason currentStopReason = StopReason.NotStopped;
    private float targetDegreesToRotate = 0f;
    private Vector3 activePivotPoint;

    private float turnCooldownTimer = 0f;
    private const float POST_TURN_COOLDOWN_DURATION = 0.2f;

    private bool wasTrackingLost = false;

    // Rolling sample window storing horizontal 2D direction vectors
    private struct DirectionSample
    {
        public float time;
        public Vector2 horizontalForward;
    }
    private Queue<DirectionSample> directionHistory = new Queue<DirectionSample>();

    // Live Telemetry
    public string LastDebugState { get; private set; } = "Initializing...";
    public float CurrentYawDelta { get; private set; } = 0f;

    private void Update()
    {
        // 1. Auto-Detect Lost Controller Tracking
        bool isTracked = OVRInput.IsControllerConnected(inputController) && 
                         OVRInput.GetControllerPositionTracked(inputController) && 
                         OVRInput.GetControllerOrientationTracked(inputController);

        if (!isTracked)
        {
            if (!wasTrackingLost)
            {
                wasTrackingLost = true;
                Debug.LogWarning("<color=red>[VRTrackMover] Controller tracking lost! Forcing track stop and closing all obstacles.</color>");
                OnTrackLost();
            }
            return;
        }

        // Auto-recover tracking state when connection returns
        if (wasTrackingLost)
        {
            wasTrackingLost = false;
            Debug.Log("<color=green>[VRTrackMover] Controller tracking restored.</color>");
            if (currentStopReason == StopReason.StoppedAtObstacle && LastDebugState == "STOPPED - Tracking Lost")
            {
                ResumeTrack();
            }
        }

        RecordControllerDirection();

        if (turnCooldownTimer > 0f)
        {
            turnCooldownTimer -= Time.deltaTime;
        }

        // Hard lock: If turning, halt linear translation completely
        if (isTurning)
        {
            currentSpeed = 0f;
            HandleTurnRotation();
            return;
        }

        // Hard lock: If stopped for any reason, halt linear translation
        if (currentStopReason != StopReason.NotStopped)
        {
            currentSpeed = 0f;
            return;
        }

        HandleSpeedInput();
        MoveTrackBackward();
    }

    private void RecordControllerDirection()
    {
        Quaternion controllerRot = OVRInput.GetLocalControllerRotation(inputController);
        Vector3 forward3D = controllerRot * Vector3.forward;

        Vector3 horizontalDir = Vector3.ProjectOnPlane(forward3D, Vector3.up).normalized;
        Vector2 currentDir2D = new Vector2(horizontalDir.x, horizontalDir.z);

        directionHistory.Enqueue(new DirectionSample { 
            time = Time.time, 
            horizontalForward = currentDir2D 
        });

        while (directionHistory.Count > 0 && Time.time - directionHistory.Peek().time > yawSampleWindowSeconds)
        {
            directionHistory.Dequeue();
        }
    }

    private void HandleSpeedInput()
    {
        Quaternion controllerRot = OVRInput.GetLocalControllerRotation(inputController);
        float pitchAngle = controllerRot.eulerAngles.x;
        if (pitchAngle > 180f) pitchAngle -= 360f;

        if (pitchAngle > deadzoneAngle)
        {
            float tiltNormalized = Mathf.Clamp01((pitchAngle - deadzoneAngle) / (maxTiltAngle - deadzoneAngle));
            currentSpeed = tiltNormalized * maxSpeed;
        }
        else
        {
            currentSpeed = 0f;
        }
    }

    private void MoveTrackBackward()
    {
        if (currentStopReason != StopReason.NotStopped || isTurning || currentSpeed <= 0f || playerTransform == null) return;
        transform.Translate(-playerTransform.forward * currentSpeed * Time.deltaTime, Space.World);
    }

    private void HandleTurnRotation()
    {
        if (!isTurning) return;

        float step = Mathf.Sign(targetDegreesToRotate) * Mathf.Min(Mathf.Abs(targetDegreesToRotate), turnRotationSpeed * Time.deltaTime);
        
        transform.RotateAround(activePivotPoint, Vector3.up, step);
        targetDegreesToRotate -= step;

        if (Mathf.Abs(targetDegreesToRotate) < 0.1f)
        {
            isTurning = false;
            targetDegreesToRotate = 0f;
            turnCooldownTimer = POST_TURN_COOLDOWN_DURATION;
            ResumeTrack();
            Debug.Log("<color=green>[VRTrackMover] Turn Completed! Track Resumed.</color>");
        }
    }

    public int GetControllerTurnDirection()
    {
        // DO NOT allow junction turn gestures if stopped at an obstacle!
        if (currentStopReason == StopReason.StoppedAtObstacle)
        {
            directionHistory.Clear();
            return 0;
        }

        if (directionHistory.Count < 2) return 0;

        Vector2 oldestDir = directionHistory.Peek().horizontalForward;
        
        Quaternion currentRot = OVRInput.GetLocalControllerRotation(inputController);
        Vector3 forward3D = currentRot * Vector3.forward;
        Vector3 currentHorizontal = Vector3.ProjectOnPlane(forward3D, Vector3.up).normalized;
        Vector2 currentDir = new Vector2(currentHorizontal.x, currentHorizontal.z);

        CurrentYawDelta = Vector2.SignedAngle(oldestDir, currentDir);

        if (CurrentYawDelta < -turnRotationThreshold)
        {
            directionHistory.Clear();
            return -1;
        }
        if (CurrentYawDelta > turnRotationThreshold)
        {
            directionHistory.Clear();
            return 1;
        }

        return 0;
    }

    public void RotateTrackAroundPlayer(float angleDegrees, Vector3 junctionCenter)
    {
        // Block turns if already turning OR if stopped at an obstacle
        if (isTurning || currentStopReason == StopReason.StoppedAtObstacle || playerTransform == null)
        {
            Debug.LogWarning("[VRTrackMover] Turn rejected: Player is stopped at an obstacle or already turning.");
            return;
        }

        // Perfectly align chosen pivot point to the player's position
        Vector3 offset = playerTransform.position - junctionCenter;
        offset.y = 0; // Keep track height locked
        transform.position += offset;

        targetDegreesToRotate = angleDegrees;
        activePivotPoint = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        
        isTurning = true;
        currentStopReason = StopReason.NotStopped;
        LastDebugState = $"Executing Player Pivot Turn: {angleDegrees}°";
    }

    public void OnTrackLost()
    {
        // 1. Force close all active track obstacles in the scene
        TrackObstacle[] obstacles = FindObjectsOfType<TrackObstacle>();
        foreach (TrackObstacle obstacle in obstacles)
        {
            obstacle.ForceCloseObstacle();
        }

        // 2. Override turn states and hard stop track motion
        isTurning = false;
        targetDegreesToRotate = 0f;
        currentStopReason = StopReason.StoppedAtObstacle;
        currentSpeed = 0f;
        directionHistory.Clear();
        LastDebugState = "STOPPED - Tracking Lost";

        Debug.LogWarning("<color=red>[VRTrackMover] OnTrackLost executed: All obstacles forced closed, movement halted.</color>");
    }

    /// <summary>
    /// Default stop method used by obstacles when player hits a closed barrier.
    /// </summary>
    public void StopTrack()
    {
        StopTrack(StopReason.StoppedAtObstacle);
    }

    public void StopTrack(StopReason reason)
    {
        // Do NOT return if we are trying to stop at an obstacle! Always allow obstacle stops.
        if (isTurning && reason != StopReason.StoppedAtObstacle) return;

        currentStopReason = reason;
        currentSpeed = 0f;
        directionHistory.Clear();
        LastDebugState = $"STOPPED - {reason}";
        Debug.LogWarning($"<color=orange>[VRTrackMover] TRACK STOPPED: {reason}</color>");
    }

    public void ResumeTrack()
    {
        currentStopReason = StopReason.NotStopped;
        isTurning = false;
        targetDegreesToRotate = 0f;
        directionHistory.Clear();
        LastDebugState = "Running";
        Debug.Log("<color=green>[VRTrackMover] Track Resumed Successfully!</color>");
    }

    public bool IsStopped => currentStopReason != StopReason.NotStopped;
    public bool IsStoppedAtObstacle => currentStopReason == StopReason.StoppedAtObstacle;
    public bool IsTurning => isTurning;
    public bool IsInCooldown => turnCooldownTimer > 0f;
    public StopReason CurrentStopReason => currentStopReason;
}