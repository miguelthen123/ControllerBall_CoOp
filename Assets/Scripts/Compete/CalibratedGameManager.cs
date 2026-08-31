using UnityEngine;
using UnityEngine.SceneManagement; // 1. Added for Scene Loading

public class CalibratedGameManager : MonoBehaviour
{
    [Header("Tracked Hardware Nodes")]
    [SerializeField] private Transform myRightController;      // Held in hand (Shooter)
    [SerializeField] private Transform opponentLeftController; // Inside opponent casing (Target)

    [Header("Game World Objects")]
    [SerializeField] private Transform shooterGameObject;     // Virtual weapon/shooter
    [SerializeField] private Transform targetGameObject;      // Virtual basket/target
    [SerializeField] private Transform alignedWorldRoot;       // Parent transform of your game environment

    [Header("Weapon Controls")]
    [SerializeField] private AutoShooter autoShooter;          // Reference to the AutoShooter script

    [Header("Calibration Settings")]
    [SerializeField] private float targetRadius = 0.20f;        // 20cm tolerance
    [SerializeField] private float requiredHoldTime = 3.0f;     // 3 seconds dwell

    [Header("Scene Transition Settings")]
    [SerializeField] private string targetSceneName;           // Name of the scene to load in Inspector

    private float currentHoldTimer = 0.0f;
    private bool isCalibrated = false;

    // Offsets calculated at the 3-second mark
    private Vector3 worldOriginPosition;
    private Quaternion worldOriginRotation;

    private void Start()
    {
        // Ensure shooting is disabled until calibration finishes
        if (autoShooter != null)
        {
            autoShooter.SetAutoShoot(false);
        }
    }

    private void Update()
    {
        // 2. Poll for Meta XR B button input
        CheckSceneSwitchInput();

        if (!isCalibrated)
        {
            CheckCalibrationDwell();
        }
        else
        {
            UpdateGameplayTransforms();
        }
    }

    private void CheckSceneSwitchInput()
    {
        // Button.Two specifically targets the 'B' button on the Right Touch Controller
        if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch))
        {
            LoadTargetScene();
        }
    }

    private void LoadTargetScene()
    {
        if (!string.IsNullOrEmpty(targetSceneName))
        {
            SceneManager.LoadScene(targetSceneName);
        }
        else
        {
            Debug.LogWarning("Target Scene Name is empty! Please set it in the Inspector.");
        }
    }

    private void CheckCalibrationDwell()
    {
        Vector3 rayOrigin = myRightController.position;
        Vector3 rayDirection = myRightController.forward;
        Vector3 targetPos = opponentLeftController.position;

        // Vector math to check if right controller points at left controller
        Vector3 rayToTarget = targetPos - rayOrigin;
        float projection = Vector3.Dot(rayToTarget, rayDirection);

        if (projection > 0)
        {
            Vector3 closestPointOnRay = rayOrigin + (rayDirection * projection);
            float distanceToRay = Vector3.Distance(closestPointOnRay, targetPos);

            if (distanceToRay <= targetRadius)
            {
                currentHoldTimer += Time.deltaTime;

                if (currentHoldTimer >= requiredHoldTime)
                {
                    LockCalibration(rayOrigin, rayDirection);
                }
                return;
            }
        }

        // Reset if ray leaves target area
        currentHoldTimer = 0.0f;
    }

    private void LockCalibration(Vector3 originPos, Vector3 forwardDir)
    {
        // Flatten forward direction to keep horizon level (prevents pitching/rolling the arena)
        forwardDir.y = 0;
        forwardDir.Normalize();

        worldOriginPosition = originPos;
        worldOriginRotation = Quaternion.LookRotation(forwardDir);

        // Align the Game Environment Root to the physical calibration point
        if (alignedWorldRoot != null)
        {
            alignedWorldRoot.position = worldOriginPosition;
            alignedWorldRoot.rotation = worldOriginRotation;
        }

        isCalibrated = true;

        // Enable auto-shooting now that calibration is locked
        if (autoShooter != null)
        {
            autoShooter.SetAutoShoot(true);
        }

        Debug.Log("Calibration Complete! Auto-shooter activated.");
    }

    private void UpdateGameplayTransforms()
    {
        // Both objects follow their raw tracked hardware transforms directly in real-time
        if (shooterGameObject != null && myRightController != null)
        {
            shooterGameObject.position = myRightController.position;
            shooterGameObject.rotation = myRightController.rotation;
        }

        if (targetGameObject != null && opponentLeftController != null)
        {
            targetGameObject.position = opponentLeftController.position;
            targetGameObject.rotation = opponentLeftController.rotation;
        }
    }

    // Call this if you need to reset alignment during play
    public void Recalibrate()
    {
        isCalibrated = false;
        currentHoldTimer = 0.0f;

        if (autoShooter != null)
        {
            autoShooter.SetAutoShoot(false);
        }
    }
}