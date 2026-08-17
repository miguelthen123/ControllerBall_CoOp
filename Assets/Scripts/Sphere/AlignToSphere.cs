using UnityEngine;

[ExecuteAlways] // Works in Scene view as well as Play mode
public class AlignToSphere : MonoBehaviour
{
    [Header("Sphere Reference")]
    [SerializeField] private Transform globeTransform;
    [SerializeField] private float globeRadius = 5f;
    [SerializeField] private float heightOffset = 0f; // Extra height above/below surface

    [Header("Alignment Settings")]
    [SerializeField] private bool alignPosition = true;
    [SerializeField] private bool alignRotation = true;

    private void Update()
    {
        if (globeTransform == null) return;

        // 1. Calculate the surface outward direction (Up vector relative to Earth)
        Vector3 globeCenter = globeTransform.position;
        Vector3 outwardDirection = (transform.position - globeCenter).normalized;

        // Handle edge case where object is exactly at the center
        if (outwardDirection == Vector3.zero) outwardDirection = Vector3.up;

        // 2. Snap Position to sphere surface
        if (alignPosition)
        {
            Vector3 targetPosition = globeCenter + outwardDirection * (globeRadius + heightOffset);
            transform.position = targetPosition;
        }

        // 3. Align "Up" axis to point away from sphere center
        if (alignRotation)
        {
            // Keeps the object's current forward facing direction while forcing local Y to point UP away from globe
            Quaternion surfaceRotation = Quaternion.FromToRotation(transform.up, outwardDirection) * transform.rotation;
            transform.rotation = surfaceRotation;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (globeTransform != null)
        {
            // Visual green line showing the local "Up" vector relative to sphere center
            Gizmos.color = Color.green;
            Vector3 globeCenter = globeTransform.position;
            Vector3 outwardDirection = (transform.position - globeCenter).normalized;
            Gizmos.DrawRay(transform.position, outwardDirection * 2f);
        }
    }
}