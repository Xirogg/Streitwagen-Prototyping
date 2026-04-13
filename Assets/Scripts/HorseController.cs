using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the horse pair that pulls the chariot.
/// Reads WASD input directly from keyboard and applies forces/torque to the Rigidbody.
/// </summary>
public class HorseController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float pullForce = 3000f;
    [SerializeField] private float maxSpeed = 12f;
    [SerializeField] private float brakeForce = 2000f;

    [Header("Steering")]
    [SerializeField] private float steerTorque = 800f;
    [SerializeField] private float maxAngularVelocity = 2f;

    [Header("Stability")]
    [SerializeField] private float lateralDampingForce = 500f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    private float debugTimer;

    private Rigidbody rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.maxAngularVelocity = maxAngularVelocity;
    }

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        float horizontal = 0f;
        float vertical = 0f;

        if (kb.wKey.isPressed) vertical += 1f;
        if (kb.sKey.isPressed) vertical -= 1f;
        if (kb.aKey.isPressed) horizontal -= 1f;
        if (kb.dKey.isPressed) horizontal += 1f;

        

        moveInput = new Vector2(horizontal, vertical);
        FixedUpdate();
    }


    private void FixedUpdate()
    {
        ApplyLocomotion(moveInput.y);
        ApplySteering(moveInput.x);
        ApplyLateralDamping();

        // Debug logging every 0.5 seconds
        if (debugLog)
        {
            debugTimer += Time.fixedDeltaTime;
            if (debugTimer >= 0.5f)
            {
                debugTimer = 0f;
                float speed = rb.linearVelocity.magnitude;
                float fwdSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
                Debug.Log($"[Horse] Input=({moveInput.x:F1},{moveInput.y:F1}) | " +
                          $"Speed={speed:F2} m/s | FwdSpeed={fwdSpeed:F2} | " +
                          $"Pos={transform.position} | " +
                          $"Vel={rb.linearVelocity} | " +
                          $"isSleeping={rb.IsSleeping()} | isKinematic={rb.isKinematic}");
            }
        }
    }

    private void ApplyLocomotion(float throttle)
    {
        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (throttle > 0f)
        {
            if (currentForwardSpeed < maxSpeed)
            {
                Vector3 force = transform.forward * pullForce * throttle;
                rb.AddForce(force, ForceMode.Force);

                if (debugLog && debugTimer < Time.fixedDeltaTime)
                {
                    Debug.Log($"[Horse] APPLYING FORCE: {force} (magnitude={force.magnitude:F0}N)");
                }
            }
        }
        else if (throttle < 0f)
        {
            rb.AddForce(transform.forward * brakeForce * throttle, ForceMode.Force);
        }
    }

    private void ApplySteering(float steerInput)
    {
        print(steerInput);
        if (Mathf.Abs(steerInput) < 0.01f)
        {
            print("MathF"); return;
        }


        float forwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float speedFactor = Mathf.Clamp01(Mathf.Abs(forwardSpeed) / 2f);

        rb.AddTorque(Vector3.up * steerTorque * steerInput, ForceMode.Force);
    }

    private void ApplyLateralDamping()
    {
        Vector3 lateralVelocity = Vector3.Project(rb.linearVelocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralDampingForce, ForceMode.Force);
    }
}
