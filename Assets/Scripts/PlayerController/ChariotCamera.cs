using UnityEngine;

/// <summary>
/// Smooth third-person follow camera for the chariot.
/// Follows behind the chariot with look-ahead based on velocity.
/// </summary>
public class ChariotCamera : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow Settings")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 5f, -8f);
    [SerializeField] private float positionSmoothTime = 0.3f;
    [SerializeField] private float rotationSmoothSpeed = 5f;
    [SerializeField] private float lookAheadFactor = 2f;

    private Vector3 currentVelocity;
    private Rigidbody targetRb;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        targetRb = target != null ? target.GetComponent<Rigidbody>() : null;
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
    }

  
}

