using System.Collections;
using UnityEngine;
using TMPro; // Required for TextMeshPro

public class TargetOnHit : MonoBehaviour
{
    [Header("UI Display")]
    [SerializeField] private TextMeshProUGUI hitCountText;        // Drag your 3D TextMeshPro object here
    [SerializeField] private string textPrefix = "Hits: ";   // Prefix string before count

    [Header("Color Settings")]
    [SerializeField] private Color defaultColor = new Color(0.35f, 0.65f, 0.72f, 1f); // Muted Clay
    [SerializeField] private Color hitColor = new Color(0.98f, 0.40f, 0.35f, 1f);     // Coral Red
    [SerializeField] private float colorFlashDuration = 0.4f;

    [Header("Meta XR Haptics (Left Controller)")]
    [SerializeField] private float hapticFrequency = 0.5f; // Frequency (0.0 to 1.0)
    [SerializeField] private float hapticAmplitude = 0.8f; // Strength (0.0 to 1.0)
    [SerializeField] private float hapticDuration = 0.25f; // Seconds

    // --- Exposed Hit Count ---
    public int HitCount { get; private set; } = 0; // Public getter, private setter

    private MeshRenderer meshRenderer;
    private MaterialPropertyBlock propertyBlock;
    private Coroutine colorRoutine;
    private Coroutine hapticRoutine;

    private static readonly int MainColorProperty = Shader.PropertyToID("_MainColor");

    private void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
        propertyBlock = new MaterialPropertyBlock();

        // Initialize target material color and UI counter
        SetTargetColor(defaultColor);
        UpdateHitCountUI();
    }

    private void OnTriggerEnter(Collider other)
    {
        // Check if the trigger volume was entered by a Bullet
        if (other.CompareTag("Bullet") || other.GetComponent<Bullet>() != null)
        {
            // 1. Increment hit counter
            RegisterHit();

            // 2. Flash target color
            if (colorRoutine != null) StopCoroutine(colorRoutine);
            colorRoutine = StartCoroutine(FlashColorRoutine());

            // 3. Play Meta XR haptic shock on Opponent's Left Controller
            if (hapticRoutine != null) StopCoroutine(hapticRoutine);
            hapticRoutine = StartCoroutine(TriggerLeftHapticsRoutine());

            // 4. Destroy incoming bullet
            Destroy(other.gameObject);
        }
    }

    public void RegisterHit()
    {
        HitCount++;
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

    private IEnumerator FlashColorRoutine()
    {
        SetTargetColor(hitColor);

        float elapsed = 0f;
        while (elapsed < colorFlashDuration)
        {
            elapsed += Time.deltaTime;
            Color currentColor = Color.Lerp(hitColor, defaultColor, elapsed / colorFlashDuration);
            SetTargetColor(currentColor);
            yield return null;
        }

        SetTargetColor(defaultColor);
    }

    private void SetTargetColor(Color color)
    {
        if (meshRenderer == null) return;
        meshRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetColor(MainColorProperty, color);
        meshRenderer.SetPropertyBlock(propertyBlock);
    }

    private IEnumerator TriggerLeftHapticsRoutine()
    {
        OVRInput.SetControllerVibration(hapticFrequency, hapticAmplitude, OVRInput.Controller.LTouch);
        yield return new WaitForSeconds(hapticDuration);
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
    }

    private void OnDisable()
    {
        OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
    }
}