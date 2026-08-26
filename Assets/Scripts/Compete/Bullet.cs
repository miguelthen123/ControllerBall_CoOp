using UnityEngine;

public class Bullet : MonoBehaviour
{
    [SerializeField] private float speed = 15.0f;
    [SerializeField] private float lifeTime = 4.0f;
    
    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        // Push bullet forward immediately upon spawn
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }

        // Cleanup bullet after time to prevent memory leaks
        Destroy(gameObject, lifeTime);
    }
}