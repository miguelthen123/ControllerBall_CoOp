using UnityEngine;

public class PlayerObstacleSensor : MonoBehaviour
{
    public bool IsTouchingObstacle { get; private set; }
    public Vector3 ObstacleNormal { get; private set; }

    [Tooltip("Layer mask for obstacles that should block movement.")]
    [SerializeField] private LayerMask obstacleLayer;

    private void OnTriggerStay(Collider other)
    {
        if (IsInObstacleLayer(other.gameObject))
        {
            IsTouchingObstacle = true;

            // Calculate direction pointing away from obstacle center toward player
            Vector3 closestPoint = other.ClosestPoint(transform.position);
            Vector3 dirAwayFromObstacle = (transform.position - closestPoint).normalized;

            // Maintain stable normal vector
            if (dirAwayFromObstacle != Vector3.zero)
            {
                ObstacleNormal = dirAwayFromObstacle;
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsInObstacleLayer(other.gameObject))
        {
            ResetState();
        }
    }

    public void ResetState()
    {
        IsTouchingObstacle = false;
        ObstacleNormal = Vector3.zero;
    }

    private bool IsInObstacleLayer(GameObject obj)
    {
        return (obstacleLayer.value & (1 << obj.layer)) != 0;
    }
}