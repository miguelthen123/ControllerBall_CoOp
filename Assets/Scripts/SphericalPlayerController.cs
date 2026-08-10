using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SphericalPlayerController : MonoBehaviour
{
    public enum ControllerAxis { X_Pitch, Y_Yaw, Z_Roll }
    public enum ControlMode { AxisAngle, AngularVelocity }

    [Header("Sphere Planet Target")]
    [SerializeField] private Transform planet;
    [SerializeField] private float gravityAccel = -10f;

    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Controller Input Setup")]
    [SerializeField] private OVRInput.Controller targetController = OVRInput.Controller.LTouch;
    [SerializeField] private ControllerAxis forwardBackwardAxis = ControllerAxis.X_Pitch;
    [SerializeField] private ControlMode controlType = ControlMode.AxisAngle;
    [SerializeField] private bool invertInput = false;

    [Header("Angle Mode Thresholds")]
    [Tooltip("Neutral deadzone in degrees before movement triggers.")]
    [SerializeField] private float deadzoneDegrees = 5f;
    [Tooltip("Degrees of tilt needed to reach maximum movement speed.")]
    [SerializeField] private float maxTiltAngle = 45f;

    [Header("Velocity Mode Sensitivity")]
    [SerializeField] private float velocitySensitivity = 1f;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.useGravity = false;
    }

    private void FixedUpdate()
    {
        if (planet == null) return;

        // 1. Align Player to Sphere Normal & Apply Gravity
        Vector3 gravityDir = (transform.position - planet.position).normalized;
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, gravityDir) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 50f * Time.fixedDeltaTime);
        rb.AddForce(gravityDir * gravityAccel, ForceMode.Acceleration);

        // 2. Read Controller Input
        if (OVRInput.IsControllerConnected(targetController))
        {
            float moveInput = 0f;

            if (controlType == ControlMode.AxisAngle)
            {
                moveInput = ReadAxisAngleInput();
            }
            else if (controlType == ControlMode.AngularVelocity)
            {
                moveInput = ReadAngularVelocityInput();
            }

            if (invertInput) moveInput *= -1f;

            // 3. Apply Forward/Backward Movement Along Surface
            Vector3 moveDir = transform.forward * moveInput;
            rb.MovePosition(rb.position + moveDir * moveSpeed * Time.fixedDeltaTime);
        }
    }

    private float ReadAxisAngleInput()
    {
        Quaternion ctrlRot = OVRInput.GetLocalControllerRotation(targetController);
        Vector3 euler = ctrlRot.eulerAngles;

        float rawAngle = forwardBackwardAxis switch
        {
            ControllerAxis.X_Pitch => NormalizeAngle(euler.x),
            ControllerAxis.Y_Yaw   => NormalizeAngle(euler.y),
            ControllerAxis.Z_Roll  => NormalizeAngle(euler.z),
            _ => 0f
        };

        float absAngle = Mathf.Abs(rawAngle);
        if (absAngle < deadzoneDegrees) return 0f;

        float normalizedSpeed = Mathf.InverseLerp(deadzoneDegrees, maxTiltAngle, absAngle);
        return Mathf.Sign(rawAngle) * normalizedSpeed;
    }

    private float ReadAngularVelocityInput()
    {
        Vector3 angVel = OVRInput.GetLocalControllerAngularVelocity(targetController);

        float rawVel = forwardBackwardAxis switch
        {
            ControllerAxis.X_Pitch => angVel.x,
            ControllerAxis.Y_Yaw   => angVel.y,
            ControllerAxis.Z_Roll  => angVel.z,
            _ => 0f
        };

        return Mathf.Clamp(rawVel * velocitySensitivity, -1f, 1f);
    }

    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}