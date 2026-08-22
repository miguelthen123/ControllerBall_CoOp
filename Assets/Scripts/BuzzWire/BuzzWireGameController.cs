using System.Collections.Generic;
using UnityEngine;

public class BuzzWireGameController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BuzzWireGenerator wireGenerator;

    [Header("Meta XR Settings")]
    [SerializeField] private OVRInput.Controller targetController = OVRInput.Controller.RTouch;
    [SerializeField] private float movementSensitivity = 1.0f;

    [Header("Game State Debug")]
    [SerializeField] private bool isGameStarted = false;
    [SerializeField] private bool isGameFinished = false;

    private Vector3[] cachedPathPoints;
    private int currentPointIndex = 0;
    private Vector3 initialControllerPos;
    private Vector3 initialRingPos;
    private bool isInitialized = false;

    void Start()
    {
        InitializeGame();
    }

    public void InitializeGame()
    {
        if (wireGenerator == null)
        {
            wireGenerator = FindFirstObjectByType<BuzzWireGenerator>();
            if (wireGenerator == null)
            {
                Debug.LogError("[BUZZ WIRE] Please assign the FixedBuzzWireGenerator reference!");
                return;
            }
        }

        CacheSplinePath();

        currentPointIndex = 0;
        isGameStarted = true;
        isGameFinished = false;

        // Snap ring to exact start point
        UpdateTorusPositionAndRotation(currentPointIndex);

        // Record initial offsets for relative 2D controller motion
        initialControllerPos = OVRInput.GetLocalControllerPosition(targetController);
        initialRingPos = wireGenerator.transform.TransformPoint(cachedPathPoints[0]);
        isInitialized = true;

        Debug.Log("[BUZZ WIRE] Game Started! 2D Nearest-Point Projection Active.");
    }

    private void CacheSplinePath()
    {
        var controlPointsMethod = typeof(BuzzWireGenerator).GetMethod("GenerateRandomControlPoints", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var splinePathMethod = typeof(BuzzWireGenerator).GetMethod("GenerateSplinePath", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (controlPointsMethod != null && splinePathMethod != null)
        {
            Vector3[] controlPoints = (Vector3[])controlPointsMethod.Invoke(wireGenerator, null);
            cachedPathPoints = (Vector3[])splinePathMethod.Invoke(wireGenerator, new object[] { controlPoints, 25 });
        }
        else
        {
            Debug.LogError("[BUZZ WIRE] Could not retrieve spline path points!");
        }
    }

    void Update()
    {
        if (!isGameStarted || isGameFinished || !isInitialized || cachedPathPoints == null || cachedPathPoints.Length < 2) 
            return;

        Handle2DNearestPointTracking();
    }

    private void Handle2DNearestPointTracking()
    {
        // 1. Get raw Quest 3 controller position
        Vector3 currentControllerPos = OVRInput.GetLocalControllerPosition(targetController);
        
        // 2. Calculate 2D delta (X and Y only, Z ignored)
        Vector3 controllerDelta = (currentControllerPos - initialControllerPos) * movementSensitivity;
        
        // Target virtual 2D position in world space based on controller movement
        Vector3 targetWorld2D = initialRingPos + new Vector3(controllerDelta.x, controllerDelta.y, 0f);

        // Convert target 2D position into local wire generator space
        Vector3 targetLocal2D = wireGenerator.transform.InverseTransformPoint(targetWorld2D);
        targetLocal2D.z = 0f; // Lock strictly to XY plane

        // 3. Find the closest point index on the wire spline to the user's 2D target
        int bestIndex = currentPointIndex;
        float minDistance = float.MaxValue;

        // Search within a local window around the current index to maintain sequential progress
        int searchRange = 15;
        int minIdx = Mathf.Max(0, currentPointIndex - searchRange);
        int maxIdx = Mathf.Min(cachedPathPoints.Length - 1, currentPointIndex + searchRange);

        for (int i = minIdx; i <= maxIdx; i++)
        {
            float dist = Vector2.Distance(new Vector2(targetLocal2D.x, targetLocal2D.y), 
                                          new Vector2(cachedPathPoints[i].x, cachedPathPoints[i].y));
            if (dist < minDistance)
            {
                minDistance = dist;
                bestIndex = i;
            }
        }

        currentPointIndex = bestIndex;

        // 4. Update ring position and orientation
        UpdateTorusPositionAndRotation(currentPointIndex);

        // 5. Check Win Condition
        if (currentPointIndex >= cachedPathPoints.Length - 1 && !isGameFinished)
        {
            isGameFinished = true;
            Debug.Log("[BUZZ WIRE] VICTORY! Ring reached the END point!");
        }
    }

    private void UpdateTorusPositionAndRotation(int index)
    {
        if (cachedPathPoints == null || index < 0 || index >= cachedPathPoints.Length) return;

        // 1. Position ring directly on the spline node (Z = 0)
        Vector3 localPos = cachedPathPoints[index];
        localPos.z = 0f;
        transform.position = wireGenerator.transform.TransformPoint(localPos);

        // 2. Compute curve tangent
        Vector3 tangent = Vector3.right;
        if (index < cachedPathPoints.Length - 1)
            tangent = (cachedPathPoints[index + 1] - cachedPathPoints[index]).normalized;
        else if (index > 0)
            tangent = (cachedPathPoints[index] - cachedPathPoints[index - 1]).normalized;

        // 3. Orient the ring perpendicular to the curve on the XY plane
        if (tangent != Vector3.zero)
        {
            // Look along the tangent, using Vector3.forward (Z) as the up reference to keep the ring flat on XY
            Quaternion lookRot = Quaternion.LookRotation(tangent, Vector3.forward);
            transform.rotation = wireGenerator.transform.rotation * lookRot;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Wire"))
        {
            Debug.Log("[BUZZ WIRE DEBUG] TOUCH DETECTED! Ring hit the wire.");
            
            OVRInput.SetControllerVibration(0.5f, 0.8f, targetController);
            Invoke(nameof(StopHaptics), 0.15f);
        }
    }

    private void StopHaptics()
    {
        OVRInput.SetControllerVibration(0, 0, targetController);
    }
}