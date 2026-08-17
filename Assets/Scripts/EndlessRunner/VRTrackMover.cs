using System.Collections.Generic;
using UnityEngine;

public class VRTrackMover : MonoBehaviour
{
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
    private bool isStopped = false;
    private float targetDegreesToRotate = 0f;
    private Vector3 activePivotPoint;

    private float turnCooldownTimer = 0f;
    private const float POST_TURN_COOLDOWN_DURATION = 0.2f;

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
        RecordControllerDirection();

        if (turnCooldownTimer > 0f)
        {
            turnCooldownTimer -= Time.deltaTime;
        }

        // Hard lock: If stopped or turning, halt linear translation completely
        if (isTurning)
        {
            currentSpeed = 0f;
            HandleTurnRotation();
            return;
        }

        if (isStopped)
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
        if (isStopped || isTurning || currentSpeed <= 0f || playerTransform == null) return;
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
        if (isTurning || playerTransform == null) return;

        // Perfectly align your chosen pivot point to the player's position
        Vector3 offset = playerTransform.position - junctionCenter;
        offset.y = 0; // Keep track height locked
        transform.position += offset;

        targetDegreesToRotate = angleDegrees;
        activePivotPoint = new Vector3(playerTransform.position.x, transform.position.y, playerTransform.position.z);
        
        isTurning = true;
        isStopped = false;
        LastDebugState = $"Executing Player Pivot Turn: {angleDegrees}°";
    }



    public void StopTrack()
    {
        if (isTurning || turnCooldownTimer > 0f) return;

        isStopped = true;
        currentSpeed = 0f;
        LastDebugState = "STOPPED - Waiting for Wrist Turn";
        Debug.LogWarning("<color=red>[VRTrackMover] TRACK STOPPED: Player at junction!</color>");
    }

    public void ResumeTrack()
    {
        isStopped = false;
        directionHistory.Clear();
        LastDebugState = "Running";
    }

    public bool IsStopped => isStopped;
    public bool IsTurning => isTurning;
    public bool IsInCooldown => turnCooldownTimer > 0f;
}