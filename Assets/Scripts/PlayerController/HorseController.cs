using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Controls the horse pair that pulls the chariot.
/// Reads input directly from keyboard and applies forces/torque to the Rigidbody.
/// Player 1 (playerIndex=0): WASD
/// Player 2 (playerIndex=1): Arrow Keys
/// </summary>
public class HorseController : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private int playerIndex = 0;

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
    [SerializeField] private bool debugLog = false;
    private float debugTimer;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float speedMultiplier = 1f;

    public static bool MovementEnabled { get; set; } = true;

    public void SetSpeedMultiplier(float multiplier)
    {
        speedMultiplier = multiplier;
    }

    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
    }

    private void Awake()
    {
        InitRigidbody();
    }

    private void InitRigidbody()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }
        if (rb != null)
        {
            rb.maxAngularVelocity = maxAngularVelocity;
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        float horizontal = 0f;
        float vertical = 0f;

        if (!MovementEnabled)
        {
            moveInput = Vector2.zero;
            return;
        }

        // Try new Input System first, fall back to legacy Input.GetKey
        // (some laptop keyboards don't register all keys via the new system)
        Keyboard kb = Keyboard.current;

        if (playerIndex == 0)
        {
            // Player 1: WASD
            if ((kb != null && kb.wKey.isPressed) || Input.GetKey(KeyCode.W)) vertical += 1f;
            if ((kb != null && kb.sKey.isPressed) || Input.GetKey(KeyCode.S)) vertical -= 1f;
            if ((kb != null && kb.aKey.isPressed) || Input.GetKey(KeyCode.A)) horizontal -= 1f;
            if ((kb != null && kb.dKey.isPressed) || Input.GetKey(KeyCode.D)) horizontal += 1f;
        }
        else
        {
            // Player 2: Arrow Keys
            if ((kb != null && kb.upArrowKey.isPressed) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if ((kb != null && kb.downArrowKey.isPressed) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            if ((kb != null && kb.leftArrowKey.isPressed) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if ((kb != null && kb.rightArrowKey.isPressed) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
        }

        moveInput = new Vector2(horizontal, vertical);

        // Apply forces from Update as well to ensure responsiveness
        // (forces accumulate and are resolved in the next physics step)
        ApplyForces();
    }

    private void FixedUpdate()
    {
        ApplyForces();
        ApplyLateralDamping();

        if (debugLog)
        {
            debugTimer += Time.fixedDeltaTime;
            if (debugTimer >= 0.5f)
            {
                debugTimer = 0f;
                float speed = rb != null ? rb.linearVelocity.magnitude : -1f;
                float fwdSpeed = rb != null ? Vector3.Dot(rb.linearVelocity, transform.forward) : -1f;
                Debug.Log($"[Horse P{playerIndex + 1}] Input=({moveInput.x:F1},{moveInput.y:F1}) | " +
                          $"Speed={speed:F2} m/s | FwdSpeed={fwdSpeed:F2} | " +
                          $"Pos={transform.position} | rb={(rb != null ? "OK" : "NULL")}");
            }
        }
    }

    private void ApplyForces()
    {
        if (rb == null) InitRigidbody();
        if (rb == null) return;

        ApplyLocomotion(moveInput.y);
        ApplySteering(moveInput.x);
    }

    private void ApplyLocomotion(float throttle)
    {
        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);

        if (throttle > 0f)
        {
            if (currentForwardSpeed < maxSpeed * speedMultiplier)
            {
                Vector3 force = transform.forward * pullForce * throttle;
                rb.AddForce(force, ForceMode.Force);
            }
        }
        else if (throttle < 0f)
        {
            rb.AddForce(transform.forward * brakeForce * throttle, ForceMode.Force);
        }
    }

    private void ApplySteering(float steerInput)
    {
        if (Mathf.Abs(steerInput) < 0.01f) return;

        rb.AddTorque(Vector3.up * steerTorque * steerInput, ForceMode.Force);
    }

    private void ApplyLateralDamping()
    {
        if (rb == null) return;
        Vector3 lateralVelocity = Vector3.Project(rb.linearVelocity, transform.right);
        rb.AddForce(-lateralVelocity * lateralDampingForce, ForceMode.Force);
    }
}
