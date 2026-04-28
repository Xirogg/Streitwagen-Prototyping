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

    [Header("Input Bindings")]
    [Tooltip("Tastenset für diesen Spieler. Wird automatisch aus playerIndex gesetzt, kann aber im Inspector überschrieben werden.")]
    [SerializeField] private InputBindings bindings = InputBindings.Player1;

    [Tooltip("Wenn aktiv, werden die Bindings in Awake/SetPlayerIndex aus playerIndex überschrieben. Abschalten wenn man im Inspector eigene Tasten zuweisen will.")]
    [SerializeField] private bool autoBindFromPlayerIndex = true;

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
        ApplyAutoBindings();
    }

    private void Awake()
    {
        InitRigidbody();
        ApplyAutoBindings();
    }

    private void Start()
    {
        // Einmaliges Log am Start, damit man eindeutig sieht welcher Spieler welche Tasten liest.
        Debug.Log($"[HorseController] P{playerIndex + 1} bindings: " +
                  $"Forward={bindings.forward}, Backward={bindings.backward}, " +
                  $"Left={bindings.left}, Right={bindings.right}, " +
                  $"SharpLeft={bindings.sharpLeft}, SharpRight={bindings.sharpRight}");
    }

    private void ApplyAutoBindings()
    {
        if (!autoBindFromPlayerIndex) return;
        bindings = playerIndex == 0 ? InputBindings.Player1 : InputBindings.Player2;
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

        // Lesen ausschliesslich über das (per playerIndex zugewiesene) Bindings-Set.
        // Das verhindert dass z.B. P2 versehentlich Q liest — die Tasten kommen aus genau einer Quelle.
        Keyboard kb = Keyboard.current;

        float lateralInput = 0f;     // links/rechts (A/D bzw. Pfeile)
        float sharpInputDir = 0f;    // Q/E bzw. K/L, -1 links / +1 rechts

        if (IsKeyPressed(kb, bindings.forward))    vertical += 1f;
        if (IsKeyPressed(kb, bindings.backward))   vertical -= 1f;
        if (IsKeyPressed(kb, bindings.left))       lateralInput -= 1f;
        if (IsKeyPressed(kb, bindings.right))      lateralInput += 1f;
        if (IsKeyPressed(kb, bindings.sharpLeft))  sharpInputDir -= 1f;
        if (IsKeyPressed(kb, bindings.sharpRight)) sharpInputDir += 1f;

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

    /// <summary>
    /// Prüft ob die übergebene Taste aktuell gedrückt ist — bevorzugt über das neue Input System,
    /// fällt auf legacy <see cref="Input.GetKey(KeyCode)"/> zurück (manche Laptops haben mit dem
    /// neuen System Probleme bei einzelnen Tasten).
    /// </summary>
    private static bool IsKeyPressed(Keyboard kb, KeyCode key)
    {
        if (key == KeyCode.None) return false;
        if (kb != null)
        {
            Key k = ToInputSystemKey(key);
            if (k != Key.None && kb[k].isPressed) return true;
        }
        return Input.GetKey(key);
    }

    private static Key ToInputSystemKey(KeyCode keyCode)
    {
        switch (keyCode)
        {
            case KeyCode.W: return Key.W;
            case KeyCode.A: return Key.A;
            case KeyCode.S: return Key.S;
            case KeyCode.D: return Key.D;
            case KeyCode.Q: return Key.Q;
            case KeyCode.E: return Key.E;
            case KeyCode.K: return Key.K;
            case KeyCode.L: return Key.L;
            case KeyCode.UpArrow:    return Key.UpArrow;
            case KeyCode.DownArrow:  return Key.DownArrow;
            case KeyCode.LeftArrow:  return Key.LeftArrow;
            case KeyCode.RightArrow: return Key.RightArrow;
            default: return Key.None;
        }
    }

    /// <summary>
    /// Tastenset für einen Spieler. Komplett serialisiert — wer eigene Bindings setzen will,
    /// schaltet <see cref="autoBindFromPlayerIndex"/> ab und konfiguriert die Felder direkt im Inspector.
    /// </summary>
    [System.Serializable]
    public struct InputBindings
    {
        public KeyCode forward;
        public KeyCode backward;
        public KeyCode left;
        public KeyCode right;
        public KeyCode sharpLeft;
        public KeyCode sharpRight;

        /// <summary>P1: WASD + Q/E.</summary>
        public static InputBindings Player1 => new InputBindings
        {
            forward    = KeyCode.W,
            backward   = KeyCode.S,
            left       = KeyCode.A,
            right      = KeyCode.D,
            sharpLeft  = KeyCode.Q,
            sharpRight = KeyCode.E,
        };

        /// <summary>P2: Pfeiltasten + K/L.</summary>
        public static InputBindings Player2 => new InputBindings
        {
            forward    = KeyCode.UpArrow,
            backward   = KeyCode.DownArrow,
            left       = KeyCode.LeftArrow,
            right      = KeyCode.RightArrow,
            sharpLeft  = KeyCode.K,
            sharpRight = KeyCode.L,
        };
    }
}
