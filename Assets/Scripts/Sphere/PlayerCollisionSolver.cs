using UnityEngine;



public class PlayerCollisionSolver : MonoBehaviour

{

public enum PushAxisMode

    {

        SphericalForward, // Pushes along the forward track curved around the sphere surface

        SphericalRight,   // Pushes along the sideways track curved around the sphere surface

        SphericalCustom   // Pushes along a custom relative axis curved around the sphere surface

    }



    [Header("Globe Reference")]

    [Tooltip("The central transform of the sphere/globe the player walks on.")]

    [SerializeField] private Transform globeTransform;



    [Header("Detection Settings")]

    [SerializeField] private Collider playerCollider;

    [SerializeField] private LayerMask obstacleLayers;



    [Header("Spherical Axis Constraints")]

    [Tooltip("Choose which direction on the sphere surface the player can be pushed.")]

    [SerializeField] private PushAxisMode axisMode = PushAxisMode.SphericalForward;



    [Tooltip("Used if axisMode is SphericalCustom (e.g. Vector3.forward relative to player).")]

    [SerializeField] private Vector3 customLocalAxis = Vector3.forward;



    [Header("Overlap Buffer")]

    private readonly Collider[] overlapCache = new Collider[10];



    private void Start()

    {

        if (playerCollider == null)

        {

            playerCollider = GetComponent<Collider>();

        }



        if (globeTransform == null && transform.parent != null)

        {

            globeTransform = transform.parent;

        }

    }



    private void LateUpdate()

    {

        if (playerCollider == null || globeTransform == null) return;



        ResolveSphericalClipping();

    }



    private void ResolveSphericalClipping()

    {

        // 1. Find all overlapping obstacle colliders

        int count = Physics.OverlapBoxNonAlloc(

            playerCollider.bounds.center,

            playerCollider.bounds.extents,

            overlapCache,

            transform.rotation,

            obstacleLayers

        );



        Vector3 rawTotalCorrection = Vector3.zero;



        // 2. Compute raw 3D penetration vector (MTV)

        for (int i = 0; i < count; i++)

        {

            Collider otherCollider = overlapCache[i];

            if (otherCollider == playerCollider || otherCollider == null) continue;



            bool isPenetrating = Physics.ComputePenetration(

                playerCollider,

                playerCollider.transform.position,

                playerCollider.transform.rotation,

                otherCollider,

                otherCollider.transform.position,

                otherCollider.transform.rotation,

                out Vector3 direction,

                out float distance

            );



            if (isPenetrating)

            {

                rawTotalCorrection += direction * distance;

            }

        }



        if (rawTotalCorrection == Vector3.zero) return;



        // 3. Calculate the surface normal (upward vector from globe center to player)

        Vector3 surfaceNormal = (transform.position - globeTransform.position).normalized;



        // 4. Determine raw desired direction

        Vector3 desiredDirection = transform.forward;

        if (axisMode == PushAxisMode.SphericalRight)

        {

            desiredDirection = transform.right;

        }

        else if (axisMode == PushAxisMode.SphericalCustom)

        {

            desiredDirection = transform.TransformDirection(customLocalAxis.normalized);

        }



        // 5. Project desired direction onto the sphere's tangent plane (perpendicular to surface normal)

        Vector3 sphericalTangentAxis = Vector3.ProjectOnPlane(desiredDirection, surfaceNormal).normalized;



        // 6. Project the collision correction onto this curved tangent axis

        Vector3 constrainedCorrection = Vector3.Project(rawTotalCorrection, sphericalTangentAxis);



        // 7. Apply movement along the surface

        Vector3 newPos = transform.position + constrainedCorrection;



        // 8. Clamp position distance to preserve exact sphere radius (prevents flying or sinking)

        float currentRadius = Vector3.Distance(transform.position, globeTransform.position);

        Vector3 newDirectionFromCenter = (newPos - globeTransform.position).normalized;

       

        transform.position = globeTransform.position + (newDirectionFromCenter * currentRadius);



        // 9. Re-align player's 'Up' vector to match sphere surface normal

        transform.rotation = Quaternion.FromToRotation(transform.up, surfaceNormal) * transform.rotation;

    }

}