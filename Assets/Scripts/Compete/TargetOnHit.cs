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

    [Header("Laser Damage Settings")]
    [SerializeField] private float laserHitCooldown = 0.1f; // Minimum time between consecutive laser hits

    [Header("Free Floating Trail Physics Settings")]
    [SerializeField] private float followSpeed = 6.0f;           // Speed at which trails catch up
    [SerializeField] private float trailSpacing = 0.35f;         // Distance separation between nodes
    [SerializeField] private float trailScaleMultiplier = 0.5f;  // Scale factor relative to main sphere
    [SerializeField] private float orbitWobbleSpeed = 2.0f;      // Floating motion frequency
    [SerializeField] private float orbitWobbleAmount = 0.05f;    // Floating motion intensity

    // --- Exposed Hit Count ---
    public int HitCount { get; private set; } = 0;

    private List<Transform> trailTransforms = new List<Transform>();
    private Coroutine hapticRoutine;
    
    // Per-part cooldown tracking so main sphere and trail spheres register hits independently
    private Dictionary<TargetColorHandler, float> partLaserHitTimers = new Dictionary<TargetColorHandler, float>();

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
        Vector3 baseWorldScale = transform.lossyScale;
        Vector3 currentScale = baseWorldScale;

        for (int i = 1; i <= 2; i++)
        {
            GameObject trailObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            trailObj.name = $"{gameObject.name}_FreeTrail_{i}";

            trailObj.transform.SetParent(null);
            trailObj.transform.position = transform.position - (transform.forward * (trailSpacing * i));

            currentScale *= trailScaleMultiplier;
            trailObj.transform.localScale = currentScale;

            MeshRenderer mainRenderer = GetComponent<MeshRenderer>();
            MeshRenderer trailRenderer = trailObj.GetComponent<MeshRenderer>();
            if (trailRenderer != null && mainRenderer != null)
            {
                trailRenderer.sharedMaterial = mainRenderer.sharedMaterial;
            }

            SetupCollisionAndColor(trailObj, 1);
            trailTransforms.Add(trailObj.transform);
        }
    }

    private void Update()
    {
        UpdateFreeTrailingPositions();
    }

    private void UpdateFreeTrailingPositions()
    {
        Transform leadTarget = transform;

        for (int i = 0; i < trailTransforms.Count; i++)
        {
            Transform currentTrail = trailTransforms[i];
            if (currentTrail == null) continue;

            Vector3 targetPosition = leadTarget.position - (leadTarget.forward * trailSpacing);

            float timeOffset = Time.time * orbitWobbleSpeed + (i * 1.5f);
            Vector3 wobbleOffset = new Vector3(
                Mathf.Sin(timeOffset) * orbitWobbleAmount,
                Mathf.Cos(timeOffset * 0.8f) * orbitWobbleAmount,
                Mathf.Sin(timeOffset * 1.2f) * orbitWobbleAmount
            );

            targetPosition += wobbleOffset;

            currentTrail.position = Vector3.Lerp(currentTrail.position, targetPosition, Time.deltaTime * followSpeed);
            currentTrail.rotation = Quaternion.Slerp(currentTrail.rotation, leadTarget.rotation, Time.deltaTime * followSpeed);

            leadTarget = currentTrail;
        }
    }

    private void SetupCollisionAndColor(GameObject targetObject, int scoreValue)
    {
        Collider col = targetObject.GetComponent<Collider>();
        if (col == null)
        {
            col = targetObject.AddComponent<SphereCollider>();
        }
        col.isTrigger = true;

        TargetColorHandler colorHandler = targetObject.GetComponent<TargetColorHandler>();
        if (colorHandler == null)
        {
            colorHandler = targetObject.AddComponent<TargetColorHandler>();
        }
        colorHandler.Initialize(defaultColor, hitColor, colorFlashDuration);

        TargetPartProxy proxy = targetObject.GetComponent<TargetPartProxy>();
        if (proxy == null)
        {
            proxy = targetObject.AddComponent<TargetPartProxy>();
        }
        proxy.Initialize(this, colorHandler, scoreValue);

        if (!partLaserHitTimers.ContainsKey(colorHandler))
        {
            partLaserHitTimers.Add(colorHandler, 0f);
        }
    }

    // Handles single-frame impacts (Bullets) & initial Laser entrance
    public void OnPartTriggerEnter(Collider other, TargetColorHandler hitHandler, int points)
    {
        if (IsBullet(other))
        {
            ProcessHit(hitHandler, points);
            Destroy(other.gameObject);
        }
        else if (IsLaser(other))
        {
            TryProcessLaserHit(hitHandler, points);
        }
    }

    // Handles continuous impacts over time (Lasers)
    public void OnPartTriggerStay(Collider other, TargetColorHandler hitHandler, int points)
    {
        if (IsLaser(other))
        {
            TryProcessLaserHit(hitHandler, points);
        }
    }

    private void TryProcessLaserHit(TargetColorHandler hitHandler, int points)
    {
        if (hitHandler == null) return;

        if (!partLaserHitTimers.ContainsKey(hitHandler))
        {
            partLaserHitTimers[hitHandler] = 0f;
        }

        // Check cooldown per target sphere
        if (Time.time >= partLaserHitTimers[hitHandler] + laserHitCooldown)
        {
            partLaserHitTimers[hitHandler] = Time.time;
            ProcessHit(hitHandler, points);
        }
    }

    private void ProcessHit(TargetColorHandler hitHandler, int points)
    {
        RegisterHit(points);

        if (hitHandler != null)
        {
            hitHandler.FlashColor();
        }

        if (hapticRoutine != null) StopCoroutine(hapticRoutine);
        hapticRoutine = StartCoroutine(TriggerLeftHapticsRoutine());
    }

    private bool IsBullet(Collider col)
    {
        if (col == null) return false;

        return col.CompareTag("Bullet") 
            || col.GetComponent<Bullet>() != null 
            || col.GetComponentInParent<Bullet>() != null;
    }

    private bool IsLaser(Collider col)
    {
        if (col == null) return false;

        // Thorough hierarchy check (Self, Parent, and Root)
        string colName = col.name.ToLower();
        string rootName = col.transform.root.gameObject.name.ToLower();

        return col.CompareTag("Laser") 
            || col.transform.root.CompareTag("Laser")
            || colName.Contains("laser") 
            || rootName.Contains("laser")
            || col.GetComponentInParent<AutoShooter>() != null;
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

    private static readonly int BaseColorProperty = Shader.PropertyToID("_BaseColor");
    private static readonly int MainColorProperty = Shader.PropertyToID("_MainColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

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

        propertyBlock.SetColor(BaseColorProperty, color);
        propertyBlock.SetColor(MainColorProperty, color);
        propertyBlock.SetColor(ColorProperty, color);

        meshRenderer.SetPropertyBlock(propertyBlock);
    }
}

// Helper Proxy class that delegates trigger events back to the TargetOnHit manager
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

    private void OnTriggerStay(Collider other)
    {
        if (mainTarget != null)
        {
            mainTarget.OnPartTriggerStay(other, colorHandler, pointValue);
        }
    }
}