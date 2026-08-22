using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class BuzzWireGenerator : MonoBehaviour
{
    [Header("Wire Layout (XY Plane)")]
    [SerializeField] private float wireWidth = 1.5f;
    [SerializeField] private float maxHeight = 0.8f;
    [SerializeField] private float minHeight = 0.1f;
    [SerializeField] private int numberOfPeaks = 6;
    [SerializeField] private int curveResolution = 20;

    [Header("Wire Mesh Settings")]
    [SerializeField] private float wireRadius = 0.012f;
    [SerializeField] private int radialSegments = 8;
    [SerializeField] private Material vrCompatibleMaterial;

    [Header("Endpoint Prefabs")]
    [SerializeField] private GameObject startSpherePrefab;
    [SerializeField] private GameObject endSpherePrefab;
    [SerializeField] private float sphereScale = 0.05f;

    public Vector3 StartPoint { get; private set; }
    public Vector3 EndPoint { get; private set; }

    private GameObject currentStartSphere;
    private GameObject currentEndSphere;
    private Transform colliderContainer;

    private MeshFilter meshFilter;
    private MeshRenderer meshRenderer;

    void Awake()
    {
        EnsureWireTagExists();
        GenerateWire();
    }

    private void EnsureWireTagExists()
    {
        #if UNITY_EDITOR
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        bool found = false;
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals("Wire")) { found = true; break; }
        }

        if (!found)
        {
            tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
            SerializedProperty n = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
            n.stringValue = "Wire";
            tagManager.ApplyModifiedProperties();
        }
        #endif

        gameObject.tag = "Wire";
    }

    [ContextMenu("Generate Fixed Wire")]
    public void GenerateWire()
    {
        meshFilter = GetComponent<MeshFilter>();
        meshRenderer = GetComponent<MeshRenderer>();

        EnsureWireTagExists();

        MeshCollider oldCollider = GetComponent<MeshCollider>();
        if (oldCollider != null) DestroyImmediate(oldCollider);

        Vector3[] controlPoints = GenerateRandomControlPoints();
        Vector3[] smoothPath = GenerateSplinePath(controlPoints, curveResolution);

        StartPoint = smoothPath[0];
        EndPoint = smoothPath[smoothPath.Length - 1];

        Mesh wireMesh = CreateTubeMesh(smoothPath, wireRadius, radialSegments);
        meshFilter.sharedMesh = wireMesh;

        if (vrCompatibleMaterial != null)
        {
            meshRenderer.material = vrCompatibleMaterial;
        }

        GenerateCapsuleColliders(smoothPath);
        SpawnOrPositionEndpoints();
    }

    private void GenerateCapsuleColliders(Vector3[] path)
    {
        if (colliderContainer != null) DestroyImmediate(colliderContainer.gameObject);
        
        colliderContainer = new GameObject("WireColliders").transform;
        colliderContainer.SetParent(transform, false);

        for (int i = 0; i < path.Length - 1; i++)
        {
            Vector3 p1 = path[i];
            Vector3 p2 = path[i + 1];
            Vector3 segment = p2 - p1;
            float distance = segment.magnitude;

            GameObject capObj = new GameObject($"Cap_{i}");
            capObj.transform.SetParent(colliderContainer, false);
            capObj.transform.localPosition = p1 + segment * 0.5f;
            capObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, segment.normalized);

            CapsuleCollider cap = capObj.AddComponent<CapsuleCollider>();
            cap.direction = 1; // Y-Axis
            cap.radius = wireRadius;
            cap.height = distance + (wireRadius * 2f);
            
            capObj.tag = "Wire";
        }
    }

    private Vector3[] GenerateRandomControlPoints()
    {
        List<Vector3> points = new List<Vector3>
        {
            new Vector3(-0.1f, 0f, 0f),
            new Vector3(0f, 0.2f, 0f)
        };

        for (int i = 1; i < numberOfPeaks; i++)
        {
            float t = (float)i / numberOfPeaks;
            float x = t * wireWidth;
            float y = (i % 2 == 1) 
                ? Random.Range(maxHeight * 0.5f, maxHeight) 
                : Random.Range(minHeight, maxHeight * 0.4f);

            float xOffset = Random.Range(-0.05f, 0.05f);
            points.Add(new Vector3(x + xOffset, y, 0f));
        }

        points.Add(new Vector3(wireWidth, 0.2f, 0f));
        points.Add(new Vector3(wireWidth + 0.1f, 0f, 0f));

        return points.ToArray();
    }

    private Vector3[] GenerateSplinePath(Vector3[] controlPoints, int resolution)
    {
        List<Vector3> pathPoints = new List<Vector3>();
        for (int i = 0; i < controlPoints.Length - 3; i++)
        {
            for (int r = 0; r < resolution; r++)
            {
                float t = (float)r / resolution;
                pathPoints.Add(GetCatmullRomPosition(t, controlPoints[i], controlPoints[i + 1], controlPoints[i + 2], controlPoints[i + 3]));
            }
        }
        pathPoints.Add(controlPoints[controlPoints.Length - 2]);
        return pathPoints.ToArray();
    }

    private Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * (t * t) +
            (-p0 + 3f * p1 - 3f * p2 + p3) * (t * t * t)
        );
    }

    private Mesh CreateTubeMesh(Vector3[] path, float radius, int sides)
    {
        Mesh mesh = new Mesh { name = "VR_RandomBuzzWire_Mesh" };
        int vertCount = path.Length * sides;
        Vector3[] vertices = new Vector3[vertCount];
        Vector2[] uvs = new Vector2[vertCount];
        int[] triangles = new int[(path.Length - 1) * sides * 6];

        int vertIndex = 0, triIndex = 0;

        for (int i = 0; i < path.Length; i++)
        {
            Vector3 forward = Vector3.right;
            if (i < path.Length - 1) forward = (path[i + 1] - path[i]).normalized;
            else if (i > 0) forward = (path[i] - path[i - 1]).normalized;

            Quaternion rotation = Quaternion.LookRotation(forward);

            for (int s = 0; s < sides; s++)
            {
                float angle = (float)s / sides * Mathf.PI * 2f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0);

                vertices[vertIndex] = path[i] + (rotation * offset);
                uvs[vertIndex] = new Vector2((float)s / sides, (float)i / path.Length);

                if (i < path.Length - 1)
                {
                    int currentRing = i * sides, nextRing = (i + 1) * sides, nextSide = (s + 1) % sides;

                    triangles[triIndex++] = currentRing + s;
                    triangles[triIndex++] = nextRing + s;
                    triangles[triIndex++] = currentRing + nextSide;

                    triangles[triIndex++] = currentRing + nextSide;
                    triangles[triIndex++] = nextRing + s;
                    triangles[triIndex++] = nextRing + nextSide;
                }
                vertIndex++;
            }
        }

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void SpawnOrPositionEndpoints()
    {
        if (startSpherePrefab != null)
        {
            if (currentStartSphere == null) currentStartSphere = Instantiate(startSpherePrefab, transform);
            currentStartSphere.transform.localPosition = StartPoint;
            currentStartSphere.transform.localScale = Vector3.one * sphereScale;
        }

        if (endSpherePrefab != null)
        {
            if (currentEndSphere == null) currentEndSphere = Instantiate(endSpherePrefab, transform);
            currentEndSphere.transform.localPosition = EndPoint;
            currentEndSphere.transform.localScale = Vector3.one * sphereScale;
        }
    }
}