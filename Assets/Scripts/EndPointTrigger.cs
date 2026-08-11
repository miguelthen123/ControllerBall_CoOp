using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EndPointTrigger : MonoBehaviour
{
    [SerializeField] private string playerTag = "Player";

    private void Awake()
    {
        // Ensure collider is set as a Trigger so player passes through it smoothly
        Collider col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag) || other.transform.IsChildOf(other.transform.root))
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerReachedFinish();
            }
        }
    }
}