using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FlexibleGlobeRotator : MonoBehaviour
{
    public enum ControllerAxis { None, X_Pitch, Y_Yaw, Z_Roll }
    public enum InputSourceType { LocalRotation, AngularVelocity }

    [Header("Target Configuration")]
    [SerializeField] private Rigidbody globeRigidbody;
    [SerializeField] private Collider playerCollider;

    [Header("Controller Settings")]
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.LTouch;
    [SerializeField] private InputSourceType inputSource = InputSourceType.LocalRotation;

    [Header("Forward / Backward World Spin")]
    [SerializeField] private ControllerAxis forwardBackwardSource = ControllerAxis.X_Pitch;
    [SerializeField] private float forwardSpeed = 50f;
    [SerializeField] private bool invertForward = false;

    [Header("Left / Right World Tilt")]
    [SerializeField] private ControllerAxis leftRightSource = ControllerAxis.Z_Roll;
    [SerializeField] private float tiltSpeed = 50f;
    [SerializeField] private bool invertTilt = false;

    [Header("Input Filtering")]
    [SerializeField] private float deadzoneDegrees = 5f;
    [SerializeField] private float maxTiltDegrees = 45f;

    [Header("Predictive Collision Settings")]
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private float lookAheadDistance = 0.15f;

    private void Start()
    {
        if (globeRigidbody == null) globeRigidbody = GetComponent<Rigidbody>();
        if (globeRigidbody != null)
        {
            globeRigidbody.isKinematic = true;
            globeRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        }

        if (playerCollider == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) playerCollider = player.GetComponent<Collider>();
        }
    }

    private void FixedUpdate()
    {
        if (globeRigidbody == null || !OVRInput.IsControllerConnected(controller)) return;

        float rawForwardInput = ReadAxisValue(forwardBackwardSource);
        float rawTiltInput = ReadAxisValue(leftRightSource);

        float forwardValue = invertForward ? -rawForwardInput : rawForwardInput;
        float tiltValue = invertTilt ? -rawTiltInput : rawTiltInput;

        float deltaPitch = forwardValue * forwardSpeed * Time.fixedDeltaTime;
        float deltaRoll = tiltValue * tiltSpeed * Time.fixedDeltaTime;

        if (Mathf.Abs(deltaPitch) < 0.001f && Mathf.Abs(deltaRoll) < 0.001f) return;

        if (playerCollider != null)
        {
            // Calculate movement directions in PLAYER space projected on sphere surface
            Vector3 surfaceNormal = (playerCollider.transform.position - transform.position).normalized;

            // Forward/Back movement relative to Player's local forward
            Vector3 playerForwardTangent = Vector3.ProjectOnPlane(playerCollider.transform.forward, surfaceNormal).normalized;
            // Left/Right movement relative to Player's local right
            Vector3 playerRightTangent = Vector3.ProjectOnPlane(playerCollider.transform.right, surfaceNormal).normalized;

            // Evaluate Pitch (Forward/Back)
            if (Mathf.Abs(deltaPitch) > 0.001f)
            {
                Vector3 moveDir = playerForwardTangent * Mathf.Sign(deltaPitch);
                if (IsDirectionBlocked(moveDir, Mathf.Abs(deltaPitch) * 0.1f + lookAheadDistance))
                {
                    deltaPitch = 0f;
                }
            }

            // Evaluate Roll (Left/Right)
            if (Mathf.Abs(deltaRoll) > 0.001f)
            {
                Vector3 moveDir = playerRightTangent * Mathf.Sign(deltaRoll);
                if (IsDirectionBlocked(moveDir, Mathf.Abs(deltaRoll) * 0.1f + lookAheadDistance))
                {
                    deltaRoll = 0f;
                }
            }
        }

        if (Mathf.Abs(deltaPitch) < 0.001f && Mathf.Abs(deltaRoll) < 0.001f) return;

        Quaternion pitchRot = Quaternion.AngleAxis(deltaPitch, Vector3.right);
        Quaternion rollRot = Quaternion.AngleAxis(deltaRoll, Vector3.forward);

        globeRigidbody.MoveRotation(pitchRot * rollRot * globeRigidbody.rotation);
    }

    private bool IsDirectionBlocked(Vector3 movementDirection, float checkDistance)
    {
        Vector3 origin = playerCollider.bounds.center;
        Vector3 halfExtents = playerCollider.bounds.extents;

        // Obstacles move opposite to planet rotation relative to player
        Vector3 castDirection = -movementDirection;

        // 1. Check direct OverlapBox with lookAhead padding
        Collider[] overlaps = Physics.OverlapBox(
            origin,
            halfExtents + (Vector3.one * lookAheadDistance),
            playerCollider.transform.rotation,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );

        foreach (Collider col in overlaps)
        {
            if (col == playerCollider || col.transform.IsChildOf(playerCollider.transform)) continue;

            Vector3 closestPoint = col.ClosestPoint(origin);
            Vector3 dirToObstacle = (closestPoint - origin).normalized;

            // If we are pushing into the obstacle
            if (Vector3.Dot(castDirection, dirToObstacle) > -0.2f)
            {
                Debug.DrawRay(origin, castDirection * checkDistance, Color.red, 0.1f);
                return true;
            }
        }

        // 2. BoxCast forward along the relative trajectory
        bool hit = Physics.BoxCast(
            origin,
            halfExtents,
            castDirection,
            out RaycastHit hitInfo,
            playerCollider.transform.rotation,
            checkDistance,
            obstacleLayer,
            QueryTriggerInteraction.Ignore
        );

        if (hit && hitInfo.collider != playerCollider && !hitInfo.collider.transform.IsChildOf(playerCollider.transform))
        {
            Debug.DrawRay(origin, castDirection * checkDistance, Color.red, 0.1f);
            return true;
        }

        Debug.DrawRay(origin, castDirection * checkDistance, Color.green, 0.1f);
        return false;
    }

    private float ReadAxisValue(ControllerAxis axis)
    {
        if (axis == ControllerAxis.None) return 0f;

        if (inputSource == InputSourceType.LocalRotation)
        {
            Vector3 euler = OVRInput.GetLocalControllerRotation(controller).eulerAngles;
            float rawAngle = axis switch
            {
                ControllerAxis.X_Pitch => NormalizeAngle(euler.x),
                ControllerAxis.Y_Yaw   => NormalizeAngle(euler.y),
                ControllerAxis.Z_Roll  => NormalizeAngle(euler.z),
                _ => 0f
            };

            float absAngle = Mathf.Abs(rawAngle);
            if (absAngle < deadzoneDegrees) return 0f;

            float normalizedProgress = Mathf.InverseLerp(deadzoneDegrees, maxTiltDegrees, absAngle);
            return Mathf.Sign(rawAngle) * normalizedProgress;
        }
        else
        {
            Vector3 angVel = OVRInput.GetLocalControllerAngularVelocity(controller);
            return axis switch
            {
                ControllerAxis.X_Pitch => angVel.x,
                ControllerAxis.Y_Yaw   => angVel.y,
                ControllerAxis.Z_Roll  => angVel.z,
                _ => 0f
            };
        }
    }

    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}