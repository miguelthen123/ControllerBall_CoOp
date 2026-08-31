using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class TargetOnHit : MonoBehaviour
{
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI hitCountText;
    [SerializeField] private string textPrefix = "Hits: ";

    [Header("Color Settings")]
    [SerializeField] private Color defaultColor = new Color(0.35f, 0.65f, 0.72f, 1f); // Muted Clay
    [SerializeField] private Color hitColor = new Color(0.98f, 0.40f, 0.35f, 1f);     // Coral Red
    [SerializeField] private float colorFlashDuration = 0.4f;

    [Header("Meta XR Haptics (Left Controller)")]
    [SerializeField] private float hapticFrequency = 0.5f;
    [SerializeField] private float hapticAmplitude = 0.8f;
    [SerializeField] private float hapticDuration = 0.25f;

    [Header("Free Floating Trail Physics Settings")]
    [SerializeField] private float followSpeed = 6.0f;           // Speed at which trails catch up
    [SerializeField] private float trailSpacing = 0.35f;         // Distance separation between nodes
    [SerializeField] private float trailScaleMultiplier = 0.5f;  // Scale factor relative to main sphere (e.g. 0.5 = 50% size of main)
    [SerializeField] private float orbitWobbleSpeed = 2.0f;      // Floating motion frequency
    [SerializeField] private float orbitWobbleAmount = 0.05f;    // Floating motion intensity

    // --- Exposed Hit Count ---
    public int HitCount { get; private set; } = 0;

    private List<Transform> trailTransforms = new List<Transform>();
    private Coroutine hapticRoutine;

    private void Awake()
    {
        // 1. Setup Main Target MeshRenderer & Collision (3 Points)
        SetupCollisionAndColor(gameObject, 3);

        // 2. Spawn unparented floating trail spheres
        SpawnFloatingTrails();

        // 3. Set initial UI
        UpdateHitCountUI();
    }

    private void SpawnFloatingTrails()
    {
        // Get the base world scale of the main target sphere
        Vector3 baseWorldScale = transform.lossyScale;
        Vector3 currentScale = baseWorldScale;

        for (int i = 1; i <= 2; i++)
        {
            GameObject trailObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            trailObj.name = $"{gameObject.name}_FreeTrail_{i}";

            // Keep UNPARENTED so it moves freely in world space
            trailObj.transform.SetParent(null);
            
            // Set initial position behind main sphere
            trailObj.transform.position = transform.position - (transform.forward * (trailSpacing * i));

            // Apply size relative to main sphere's world scale
            currentScale *= trailScaleMultiplier;
            trailObj.transform.localScale = currentScale;

            // Copy shared material structure
            MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
            MeshRenderer trailRenderer = trailObj.GetComponent<MeshRenderer>();
            if (trailRenderer != null && mainRenderer != null)
            {
                trailRenderer.sharedMaterial = mainRenderer.sharedMaterial;
            }

            // Setup Collision Proxy & Independent Color Handler (1 Point)
            SetupCollisionAndColor(trailObj, 1);

            // Store transform reference for movement updates
            trailTransforms.Add(trailObj.transform);
        }
    }

    private void Update()
    {
        UpdateFreeTrailingPositions();
    }

    private void UpdateFreeTrailingPositions()
    {
        Transform leadTarget = transform; // First trail follows main sphere

        for (int i = 0; i < trailTransforms.Count; i++)
        {
            Transform currentTrail = trailTransforms[i];
            if (currentTrail == null) continue;

            // 1. Target position behind the leading object
            Vector3 targetPosition = leadTarget.position - (leadTarget.forward * trailSpacing);

            // 2. Add subtle free-floating wobble/orbit offset
            float timeOffset = Time.time * orbitWobbleSpeed + (i * 1.5f);
            Vector3 wobbleOffset = new Vector3(
                Mathf.Sin(timeOffset) * orbitWobbleAmount,
                Mathf.Cos(timeOffset * 0.8f) * orbitWobbleAmount,
                Mathf.Sin(timeOffset * 1.2f) * orbitWobbleAmount
            );

            targetPosition += wobbleOffset;

            // 3. Smoothly interpolate position & rotation
            currentTrail.position = Vector3.Lerp(currentTrail.position, targetPosition, Time.deltaTime * followSpeed);
            currentTrail.rotation = Quaternion.Slerp(currentTrail.rotation, leadTarget.rotation, Time.deltaTime * followSpeed);

            // Shift lead target for next trail element in chain
            leadTarget = currentTrail;
        }
    }

    private void SetupCollisionAndColor(GameObject targetObject, int scoreValue)
    {
        // Setup Collider
        Collider col = targetObject.GetComponent<Collider>();
        if (col == null)
        {
            col = targetObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;

        // Setup Independent Color Handler
        TargetColorHandler colorHandler = targetObject.GetComponent<TargetColorHandler>();
        if (colorHandler == null)
        {
            colorHandler = targetObject.AddComponent<TargetColorHandler>();
        }
        colorHandler.Initialize(defaultColor, hitColor, colorFlashDuration);

        // Setup Collision Proxy
        TargetPartProxy proxy = targetObject.GetComponent<TargetPartProxy>();
        if (proxy == null)
        {
            proxy = targetObject.AddComponent<TargetPartProxy>();
        }
        proxy.Initialize(this, colorHandler, scoreValue);
    }

    public void OnPartTriggerEnter(Collider other, TargetColorHandler hitHandler, int points)
    {
        if (other.CompareTag("Bullet") || other.GetComponent<Bullet>() != null)
        {
            // 1. Add score points
            RegisterHit(points);

            // 2. Flash ONLY the specific sphere that was hit
            if (hitHandler != null)
            {
                hitHandler.FlashColor();
            }

            // 3. Trigger controller haptics
            if (hapticRoutine != null) StopCoroutine(hapticRoutine);
            hapticRoutine = StartCoroutine(TriggerLeftHapticsRoutine());

            // 4. Destroy bullet
            Destroy(other.gameObject);
        }
    }

    public void RegisterHit(int points = 1)
    {
        HitCount += points;
        UpdateHitCountUI();
    }

    public void ResetHitCount()
    {
        HitCount = 0;
        UpdateHitCountUI();
    }

    private void UpdateHitCountUI()
    {
        if (hitCountText != null)
        {
            hitCountText.text = $"{textPrefix}{HitCount}";
        }
    }

    private IEnumerator TriggerLeftHapticsRoutine()
    {
        OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.LTouch);
        yield return new WaitForSeconds(hapticDuration);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
    }

    private void OnDestroy()
    {
        // Cleanup unparented child objects when target is destroyed
        foreach (Transform tr in trailTransforms)
        {
            if (tr != null) Destroy(tr.gameObject);
        }
    }

    private void OnDisable()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
    }
}

// Handles independent color flashing on individual sphere meshes
public class TargetColorHandler : MonoBehaviour
{
    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Color defaultColor;
    private Color hitColor;
    private float flashDuration;
    private Coroutine flashRoutine;

    private static readonly int MainColorProperty = Shader.PropertyToID("_MainColor");

    public void Initialize(Color defColor, Color hColor, float duration)
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();
        defaultColor = defColor;
        hitColor = hColor;
        flashDuration = duration;

        SetColor(defaultColor);
    }

    public void FlashColor()
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        SetColor(hitColor);

        float elapsed = 0f;
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            Color currentColor = Color.Lerp(hitColor, defaultColor, elapsed / flashDuration);
            SetColor(currentColor);
            yield return null;
        }

        SetColor(defaultColor);
    }

    private void SetColor(Color color)
    {
        if (meshRenderer == null) return;
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(MainColorProperty, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}

// Helper Proxy class that delegates trigger hits back to the TargetOnHit manager
public class TargetPartProxy : MonoBehaviour
{
    private TargetOnHit mainTarget;
    private TargetColorHandler colorHandler;
    private int pointValue;

    public void Initialize(TargetOnHit target, TargetColorHandler handler, int points)
    {
        mainTarget = target;
        colorHandler = handler;
        pointValue = points;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (mainTarget != null)
        {
            mainTarget.OnPartTriggerEnter(other, colorHandler, pointValue);
        }
    }
}