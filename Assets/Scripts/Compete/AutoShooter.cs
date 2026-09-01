using UnityEngine;

public class AutoShooter : MonoBehaviour
{
    public enum ShootMode { Bullet, Laser }

    [Header("Mode Settings")]
    [SerializeField] private ShootMode currentMode = ShootMode.Bullet;

    [Header("Spawn Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private GameObject laserPrefab;
    [SerializeField] private Transform barrelTip;     // Empty child object at the tip of weapon/controller

    [Header("Laser Transform Settings")]
    [SerializeField] private Vector3 laserLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 laserLocalRotation = Vector3.zero; // Euler angles (X, Y, Z)
    [SerializeField] private Vector3 laserLocalScale = Vector3.one;

    [Header("Auto-Fire Rate")]
    [SerializeField] private float fireInterval = 0.3f; // Time in seconds between shots for bullet mode
    [SerializeField] private bool autoShootEnabled = true;

    private float timer = 0.0f;
    private GameObject currentLaserInstance;

    private void Update()
    {
        if (!autoShootEnabled)
        {
            DestroyLaser();
            return;
        }

        if (currentMode == ShootMode.Bullet)
        {
            DestroyLaser(); // Ensure continuous laser isn't lingering while in bullet mode

            timer += Time.deltaTime;
            if (timer >= fireInterval)
            {
                ShootBullet();
                timer = 0.0f;
            }
        }
        else if (currentMode == ShootMode.Laser)
        {
            MaintainLaser();
        }
    }

    private void ShootBullet()
    {
        if (bulletPrefab == null || barrelTip == null) return;

        Instantiate(bulletPrefab, barrelTip.position, barrelTip.rotation);
    }

    private void MaintainLaser()
    {
        if (laserPrefab == null || barrelTip == null) return;

        // 1. Instantiate laser as a child if it doesn't exist
        if (currentLaserInstance == null)
        {
            currentLaserInstance = Instantiate(laserPrefab, barrelTip);
        }

        // 2. Continuous update: Keep transform synchronized every frame so Inspector drag works live
        ApplyLaserTransform();
    }

    private void ApplyLaserTransform()
    {
        if (currentLaserInstance == null) return;

        currentLaserInstance.transform.localPosition = laserLocalPosition;
        currentLaserInstance.transform.localRotation = Quaternion.Euler(laserLocalRotation);
        currentLaserInstance.transform.localScale = laserLocalScale;
    }

    private void DestroyLaser()
    {
        if (currentLaserInstance != null)
        {
            Destroy(currentLaserInstance);
        }
    }

    // Called automatically in Editor when you drag values in Inspector
    private void OnValidate()
    {
        if (Application.isPlaying && currentLaserInstance != null)
        {
            ApplyLaserTransform();
        }
    }

    // Dynamic runtime configuration methods
    public void SetLaserTransform(Vector3 positionOffset, Vector3 eulerRotation, Vector3 scale)
    {
        laserLocalPosition = positionOffset;
        laserLocalRotation = eulerRotation;
        laserLocalScale = scale;

        ApplyLaserTransform();
    }

    public void SetLaserScale(Vector3 scale)
    {
        laserLocalScale = scale;
        ApplyLaserTransform();
    }

    // Public method to switch fire modes on the fly
    public void SetShootMode(ShootMode mode)
    {
        if (currentMode != mode)
        {
            currentMode = mode;
            timer = 0.0f;
            DestroyLaser();
        }
    }

    // Public method to start/stop shooting from external scripts
    public void SetAutoShoot(bool state)
    {
        autoShootEnabled = state;
        timer = 0.0f;

        if (!state)
        {
            DestroyLaser();
        }
    }

    private void OnDisable()
    {
        DestroyLaser();
    }
}