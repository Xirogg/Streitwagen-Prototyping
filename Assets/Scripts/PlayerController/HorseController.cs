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
    [SerializeField] private float sharpSteerMultiplier = 1.4f;

    [Header("Stability")]
    [SerializeField] private float lateralDampingForce = 500f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    private float debugTimer;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float speedMultiplier = 1f;

    /// <summary>
    /// True solange der Spieler A/D (bzw. Pfeile) UND zusätzlich Q/E (bzw. K/L) drückt.
    /// Das ist der "scharf einlenken"-Modus — Q/E verstärkt das Lenken nur dann.
    /// </summary>
    public bool IsSharpSteering { get; private set; }

    /// <summary>
    /// True wenn der Spieler Q/E (bzw. K/L) drückt OHNE gleichzeitig A/D (bzw. Pfeile links/rechts) zu halten.
    /// In diesem Fall wird nicht gelenkt — stattdessen versucht der Spieler einen Wagen zu rammen
    /// (siehe PlayerCollisions).
    /// </summary>
    public bool IsRamAttempting { get; private set; }

    /// <summary>
    /// Vorzeichen der Q/E (bzw. K/L) Eingabe: -1 = links (Q/K), +1 = rechts (E/L), 0 = keine.
    /// Wird von PlayerCollisions nicht direkt benötigt, kann aber für VFX/Audio nützlich sein.
    /// </summary>
    public float RamDirection { get; private set; }

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
            IsSharpSteering = false;
            IsRamAttempting = false;
            RamDirection = 0f;
            return;
        }

        // Try new Input System first, fall back to legacy Input.GetKey
        // (some laptop keyboards don't register all keys via the new system)
        Keyboard kb = Keyboard.current;

        float lateralInput = 0f;     // A/D bzw. Pfeile links/rechts
        float sharpInputDir = 0f;    // Q/E bzw. K/L, -1 links / +1 rechts

        if (playerIndex == 0)
        {
            if ((kb != null && kb.wKey.isPressed) || Input.GetKey(KeyCode.W)) vertical += 1f;
            if ((kb != null && kb.sKey.isPressed) || Input.GetKey(KeyCode.S)) vertical -= 1f;
            if ((kb != null && kb.aKey.isPressed) || Input.GetKey(KeyCode.A)) lateralInput -= 1f;
            if ((kb != null && kb.dKey.isPressed) || Input.GetKey(KeyCode.D)) lateralInput += 1f;
            if ((kb != null && kb.qKey.isPressed) || Input.GetKey(KeyCode.Q)) sharpInputDir -= 1f;
            if ((kb != null && kb.eKey.isPressed) || Input.GetKey(KeyCode.E)) sharpInputDir += 1f;
        }
        else
        {
            if ((kb != null && kb.upArrowKey.isPressed) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if ((kb != null && kb.downArrowKey.isPressed) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            if ((kb != null && kb.leftArrowKey.isPressed) || Input.GetKey(KeyCode.LeftArrow)) lateralInput -= 1f;
            if ((kb != null && kb.rightArrowKey.isPressed) || Input.GetKey(KeyCode.RightArrow)) lateralInput += 1f;
            if ((kb != null && kb.kKey.isPressed) || Input.GetKey(KeyCode.K)) sharpInputDir -= 1f;
            if ((kb != null && kb.lKey.isPressed) || Input.GetKey(KeyCode.L)) sharpInputDir += 1f;
        }

        bool sharpPressed = Mathf.Abs(sharpInputDir) > 0.01f;
        bool lateralPressed = Mathf.Abs(lateralInput) > 0.01f;

        // Q/E ohne A/D = Ram-Versuch, kein Lenken
        bool ramAttempt = sharpPressed && !lateralPressed;

        if (ramAttempt)
        {
            horizontal = 0f;
        }
        else if (sharpPressed && lateralPressed)
        {
            // beides zusammen = scharf einlenken (verstärkt in Richtung des sharp-Inputs)
            horizontal = lateralInput + sharpInputDir * sharpSteerMultiplier;
        }
        else
        {
            horizontal = lateralInput;
        }

        moveInput = new Vector2(horizontal, vertical);
        IsSharpSteering = sharpPressed && lateralPressed;
        IsRamAttempting = ramAttempt;
        RamDirection = sharpInputDir;

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
