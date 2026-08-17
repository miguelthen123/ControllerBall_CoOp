using UnityEngine;

public class ObstacleAxisRotator : MonoBehaviour
{
    public enum ControllerAxis { None, X_Pitch, Y_Yaw, Z_Roll }
    public enum PivotLocation { Middle, LeftEdge, RightEdge, Custom }

    [Header("Obstacle Setup")]
    [Tooltip("The object that will spin. Leave empty to use this object.")]
    [SerializeField] private Transform targetObstacle;

    [Header("Pivot / Hinge Settings")]
    [Tooltip("Choose where the hinge pivot is placed on the obstacle.")]
    [SerializeField] private PivotLocation pivotLocation = PivotLocation.LeftEdge;

    [Tooltip("Manual pivot adjustment vector (used ONLY if Pivot Location is set to Custom).")]
    [SerializeField] private Vector3 customPivotOffset = Vector3.zero;

    [Header("Randomization")]
    [Tooltip("If TRUE, randomly chooses whether the obstacle starts OPEN or CLOSED when the scene begins.")]
    [SerializeField] private bool randomizeStateOnStart = false;

    [Header("Controller Setup")]
    [SerializeField] private OVRInput.Controller controller = OVRInput.Controller.LTouch;
    [SerializeField] private ControllerAxis inputAxis = ControllerAxis.Y_Yaw;

    [Header("Local Spin Axis")]
    [Tooltip("The axis around which the obstacle spins on itself. (e.g. Vector3.up for Y-axis door spin)")]
    [SerializeField] private Vector3 localSpinAxis = Vector3.up;

    [Header("Two-State Angles")]
    [SerializeField] private float closedAngle = 0f;
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private float rotationSpeed = 180f; // Degrees per second

    [Header("Trigger Thresholds")]
    [Tooltip("Tilt angle required on controller to trigger the toggle.")]
    [SerializeField] private float triggerThresholdDegrees = 50f;
    
    [Tooltip("Must drop back below this angle before a new gesture can trigger another toggle.")]
    [SerializeField] private float resetThresholdDegrees = 20f;

    // State variables
    private bool isOpen = false;
    private bool canTrigger = true;
    private float currentSpinAngle = 0f;
    private Vector3 calculatedPivotPoint;

    private void Start()
    {
        if (targetObstacle == null) targetObstacle = transform;

        // 1. Determine Pivot Point based on selection
        CalculatePivotPoint();

        // 2. Randomize starting state if enabled
        if (randomizeStateOnStart)
        {
            isOpen = Random.value > 0.5f; // 50% chance Open or Closed
        }

        // 3. Set initial rotation immediately without animation
        currentSpinAngle = isOpen ? openAngle : closedAngle;
        RotateAroundPivot(currentSpinAngle);
    }

    private void Update()
    {
        if (targetObstacle == null) return;

        // 1. Read controller input
        if (OVRInput.IsControllerConnected(controller))
        {
            CheckGestureTrigger();
        }

        // 2. Animate rotation around the selected pivot
        AnimateObstacleRotation();
    }

    private void CalculatePivotPoint()
    {
        float extentX = 0f;

        // Calculate extent along local X axis using MeshRenderer or LocalScale
        MeshRenderer meshRenderer = targetObstacle.GetComponent<MeshRenderer>();
        if (meshRenderer != null)
        {
            extentX = meshRenderer.bounds.extents.x;
        }
        else
        {
            extentX = targetObstacle.localScale.x * 0.5f;
        }

        switch (pivotLocation)
        {
            case PivotLocation.LeftEdge:
                // Shift pivot point to the left (-X axis) bound
                calculatedPivotPoint = targetObstacle.position - (targetObstacle.right * extentX);
                break;

            case PivotLocation.RightEdge:
                // Shift pivot point to the right (+X axis) bound
                calculatedPivotPoint = targetObstacle.position + (targetObstacle.right * extentX);
                break;

            case PivotLocation.Custom:
                calculatedPivotPoint = targetObstacle.position + targetObstacle.TransformDirection(customPivotOffset);
                break;

            case PivotLocation.Middle:
            default:
                calculatedPivotPoint = targetObstacle.position;
                break;
        }
    }

    private void CheckGestureTrigger()
    {
        float rawAngle = ReadRawAngle(inputAxis);
        float absAngle = Mathf.Abs(rawAngle);

        if (canTrigger && absAngle >= triggerThresholdDegrees)
        {
            isOpen = !isOpen; // Toggle state
            canTrigger = false; 
        }
        else if (!canTrigger && absAngle <= resetThresholdDegrees)
        {
            canTrigger = true; // Reset gesture trigger
        }
    }

    private void AnimateObstacleRotation()
    {
        float targetAngle = isOpen ? openAngle : closedAngle;

        if (Mathf.Approximately(currentSpinAngle, targetAngle)) return;

        float previousAngle = currentSpinAngle;
        currentSpinAngle = Mathf.MoveTowards(currentSpinAngle, targetAngle, rotationSpeed * Time.deltaTime);
        
        float deltaAngle = currentSpinAngle - previousAngle;

        // Apply rotation step around chosen pivot
        RotateAroundPivot(deltaAngle);
    }

    private void RotateAroundPivot(float deltaAngle)
    {
        // Re-calculate local pivot position in case globe rotated targetObstacle in world space
        CalculatePivotPoint();

        Vector3 worldSpinAxis = targetObstacle.TransformDirection(localSpinAxis.normalized);
        targetObstacle.RotateAround(calculatedPivotPoint, worldSpinAxis, deltaAngle);
    }

    private float ReadRawAngle(ControllerAxis axis)
    {
        if (axis == ControllerAxis.None) return 0f;

        Vector3 euler = OVRInput.GetLocalControllerRotation(controller).eulerAngles;

        return axis switch
        {
            ControllerAxis.X_Pitch => NormalizeAngle(euler.x),
            ControllerAxis.Y_Yaw   => NormalizeAngle(euler.y),
            ControllerAxis.Z_Roll  => NormalizeAngle(euler.z),
            _ => 0f
        };
    }

    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private void OnDrawGizmosSelected()
    {
        // Visualizes the pivot hinge position in Scene View as a red sphere
        if (targetObstacle != null)
        {
            CalculatePivotPoint();
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(calculatedPivotPoint, 0.05f);
        }
    }
}