using UnityEngine;

public class AutoShooter : MonoBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform barrelTip;     // Empty child object at the tip of weapon/controller

    [Header("Auto-Fire Rate")]
    [SerializeField] private float fireInterval = 0.3f; // Time in seconds between shots (e.g., 0.3s)
    [SerializeField] private bool autoShootEnabled = true;

    private float timer = 0.0f;

    private void Update()
    {
        if (!autoShootEnabled) return;

        timer += Time.deltaTime;

        if (timer >= fireInterval)
        {
            Shoot();
            timer = 0.0f; // Reset timer
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null || barrelTip == null) return;

        // Instantiate bullet at barrel tip position and facing direction
        Instantiate(bulletPrefab, barrelTip.position, barrelTip.rotation);
    }

    // Public method to start/stop shooting from your Calibration script
    public void SetAutoShoot(bool state)
    {
        autoShootEnabled = state;
        timer = 0.0f;
    }
}