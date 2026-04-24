using UnityEngine;

/// <summary>
/// Smooth third-person follow camera for the chariot.
/// Follows behind the chariot with look-ahead based on velocity.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ChariotCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [SerializeField] private float positionSmoothTime = 0.3f;
    [SerializeField] private float rotationSmoothSpeed = 5f;
    [SerializeField] private float lookAheadFactor = 2f;

    [Header("Speed FOV")]
    [Tooltip("FOV when the chariot is standing still.")]
    [SerializeField] private float baseFOV = 55f;
    [Tooltip("FOV reached at or above maxSpeed - much wider for stronger speed feel.")]
    [SerializeField] private float maxFOV = 95f;
    [Tooltip("Speed (m/s) at which maxFOV is reached.")]
    [SerializeField] private float maxSpeed = 25f;
    [Tooltip("Exponent applied to the speed ratio. >1 makes FOV ramp up harder at high speed.")]
    [SerializeField] private float fovCurvePower = 1.6f;
    [Tooltip("How fast the FOV reacts to speed changes.")]
    [SerializeField] private float fovLerpSpeed = 6f;

    private Vector3 currentVelocity;
    private Rigidbody targetRb;
    private Camera cam;

    public float CurrentSpeed { get; private set; }
    public float SpeedRatio { get; private set; } // 0..1 relative to maxSpeed

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetRb = target != null ? target.GetComponent<Rigidbody>() : null;
    }

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (target != null)
        {
            targetRb = target.GetComponent<Rigidbody>();
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Calculate desired position behind the chariot
        Vector3 desiredPosition = target.position + target.rotation * offset;

        // Smooth position with SmoothDamp
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref currentVelocity,
            positionSmoothTime
        );

        // Look-ahead: aim the camera slightly ahead of the chariot based on velocity
        Vector3 lookTarget = target.position;
        if (targetRb != null && targetRb.linearVelocity.magnitude > 1f)
        {
            lookTarget += targetRb.linearVelocity * lookAheadFactor * Time.deltaTime;
        }
        lookTarget.y = target.position.y + 1f; // Look slightly above center

        // Smooth rotation toward look target
        Vector3 lookDirection = lookTarget - transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothSpeed * Time.deltaTime
            );
        }

        UpdateFOV();
    }

    private void UpdateFOV()
    {
        if (cam == null) return;

        // Use forward-velocity magnitude so reverse/sideways drift does not widen FOV as much
        CurrentSpeed = 0f;
        if (targetRb != null)
        {
            Vector3 v = targetRb.linearVelocity;
            v.y = 0f;
            CurrentSpeed = v.magnitude;
        }

        SpeedRatio = maxSpeed > 0.01f ? Mathf.Clamp01(CurrentSpeed / maxSpeed) : 0f;
        float curved = Mathf.Pow(SpeedRatio, fovCurvePower);
        float targetFOV = Mathf.Lerp(baseFOV, maxFOV, curved);

        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFOV, fovLerpSpeed * Time.deltaTime);
    }
}
