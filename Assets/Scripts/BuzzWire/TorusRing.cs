using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(Rigidbody))]
public class TorusRing : MonoBehaviour
{
    [Header("Torus Dimensions")]
    [SerializeField] private float outerRadius = 0.05f;   // Outer diameter of the ring
    [SerializeField] private float tubeRadius = 0.008f;   // Thickness of ring tube
    [SerializeField] private int radialSegments = 16;
    [SerializeField] private int tubeSegments = 12;
    [SerializeField] private Material ringMaterial;

    [Header("Physics Settings")]
    [SerializeField] private int colliderCount = 12;      // Colliders forming the inner hole

    private Rigidbody rb;

    void Awake()
    {
        SetupRing();
    }

    [ContextMenu("Generate Perpendicular Torus")]
    public void SetupRing()
    {
        MeshFilter mf = GetComponent<MeshFilter>();
        MeshRenderer mr = GetComponent<MeshRenderer>();
        rb = GetComponent<Rigidbody>();

        // 1. Generate Mesh oriented along the X-axis (hole faces X, wire passes through)
        Mesh torusMesh = CreatePerpendicularTorusMesh(outerRadius, tubeRadius, radialSegments, tubeSegments);
        mf.sharedMesh = torusMesh;

        if (ringMaterial != null) mr.material = ringMaterial;

        // 2. Configure Rigidbody for realistic hanging physics
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.mass = 0.1f;
        rb.linearDamping = 1f;   // Smooth out excessive swinging
        rb.angularDamping = 3f;  // Prevent uncontrolled spinning
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        // 3. Build Collider Ring around the Inner Opening
        GenerateRingColliders();
    }

    private void GenerateRingColliders()
    {
        // Clean up old child colliders
        foreach (Transform child in transform)
        {
            if (child.name.StartsWith("RingCol_")) DestroyImmediate(child.gameObject);
        }

        float innerRadius = outerRadius - tubeRadius;

        for (int i = 0; i < colliderCount; i++)
        {
            float angle = (float)i / colliderCount * Mathf.PI * 2f;
            
            // Colliders arrayed along YZ circle so X is open for the wire
            Vector3 pos = new Vector3(0f, Mathf.Sin(angle) * innerRadius, Mathf.Cos(angle) * innerRadius);

            GameObject colObj = new GameObject($"RingCol_{i}");
            colObj.transform.SetParent(transform, false);
            colObj.transform.localPosition = pos;

            SphereCollider sc = colObj.AddComponent<SphereCollider>();
            sc.radius = tubeRadius * 1.1f; // Slightly larger to prevent passing through wire
            sc.isTrigger = false; // Solid physical collisions so it hangs on wire
        }
    }

    private Mesh CreatePerpendicularTorusMesh(float r1, float r2, int radSegs, int tubeSegs)
    {
        Mesh mesh = new Mesh { name = "Perpendicular_Torus_Mesh" };
        Vector3[] vertices = new Vector3[(radSegs + 1) * (tubeSegs + 1)];
        int[] triangles = new int[radSegs * tubeSegs * 6];

        int vertIndex = 0, triIndex = 0;

        // Tube hole aligned along X-axis
        for (int i = 0; i <= radSegs; i++)
        {
            float u = (float)i / radSegs * Mathf.PI * 2f;
            Vector3 center = new Vector3(0f, Mathf.Sin(u) * r1, Mathf.Cos(u) * r1);

            for (int j = 0; j <= tubeSegs; j++)
            {
                float v = (float)j / tubeSegs * Mathf.PI * 2f;
                Vector3 normal = new Vector3(Mathf.Sin(v), Mathf.Sin(u) * Mathf.Cos(v), Mathf.Cos(u) * Mathf.Cos(v));
                vertices[vertIndex] = center + normal * r2;

                if (i < radSegs && j < tubeSegs)
                {
                    int current = i * (tubeSegs + 1) + j;
                    int next = (i + 1) * (tubeSegs + 1) + j;

                    triangles[triIndex++] = current;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = current + 1;

                    triangles[triIndex++] = current + 1;
                    triangles[triIndex++] = next;
                    triangles[triIndex++] = next + 1;
                }
                vertIndex++;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wire"))
        {
            Debug.Log("[BUZZ WIRE DEBUG] PHYSICAL IMPACT DETECTED! Ring hit the wire!");
        }
    }
}