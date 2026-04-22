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

    [Header("Ramming")]
    [Tooltip("Impuls (Ns) pro m/s horizontaler Einschlaggeschwindigkeit.")]
    [SerializeField] private float ramImpulsePerSpeed = 640f;
    [Tooltip("Konstanter Grund-Impuls (Ns) der bei jedem gültigen Ram zusätzlich oben drauf kommt — sorgt für spürbares Minimum.")]
    [SerializeField] private float ramBaseImpulse = 1200f;
    [Tooltip("Anteil vom Ram-Impuls der zusätzlich auf den Wagen (Chariot) des Opfers geht. 0=nur Pferde, 1=gleicher Impuls auf Wagen.")]
    [SerializeField, Range(0f, 1.5f)] private float ramChariotImpulseRatio = 0.8f;
    [Tooltip("Mindest-Schlieffgeschwindigkeit (m/s) damit ein Schubs ausgelöst wird.")]
    [SerializeField] private float minRamClosingSpeed = 2f;
    [Tooltip("Multiplikator für den Ram-Impuls wenn der Angreifer gerade Q/E bzw. K/L drückt.")]
    [SerializeField] private float sharpSteerRamMultiplier = 2.6f;
    [Tooltip("Wie viel vom Schubs seitlich (zum Angreifer) statt entlang des Kontaktnormale erfolgt. 0=rein Normal, 1=rein seitlich.")]
    [SerializeField, Range(0f, 1f)] private float ramLateralBias = 0.45f;
    [Tooltip("Faktor für den zusätzlichen Dreh-Impuls auf das Opfer.")]
    [SerializeField] private float ramAngularImpulseFactor = 0.18f;
    [Tooltip("Minimaler zeitlicher Abstand zwischen zwei Ram-Impulsen gegen denselben Gegner.")]
    [SerializeField] private float ramCooldown = 0.15f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = false;
    [SerializeField] private bool debugLogRam = true;
    private float debugTimer;

    private Rigidbody rb;
    private Vector2 moveInput;
    private float speedMultiplier = 1f;
    private float lastRamTime = -999f;

    /// <summary>
    /// True solange der Spieler Q/E (P1) bzw. K/L (P2) drückt — also den scharfen Lenk-Input.
    /// Wird im Ram-Check benutzt um den Schubs deutlich zu verstärken.
    /// </summary>
    public bool IsSharpSteering { get; private set; }

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
            return;
        }

        // Try new Input System first, fall back to legacy Input.GetKey
        // (some laptop keyboards don't register all keys via the new system)
        Keyboard kb = Keyboard.current;
        bool sharpPressed = false;

        if (playerIndex == 0)
        {
            // Player 1: WASD (Q/E for sharper turns)
            if ((kb != null && kb.wKey.isPressed) || Input.GetKey(KeyCode.W)) vertical += 1f;
            if ((kb != null && kb.sKey.isPressed) || Input.GetKey(KeyCode.S)) vertical -= 1f;
            if ((kb != null && kb.aKey.isPressed) || Input.GetKey(KeyCode.A)) horizontal -= 1f;
            if ((kb != null && kb.dKey.isPressed) || Input.GetKey(KeyCode.D)) horizontal += 1f;
            if ((kb != null && kb.qKey.isPressed) || Input.GetKey(KeyCode.Q)) { horizontal -= sharpSteerMultiplier; sharpPressed = true; }
            if ((kb != null && kb.eKey.isPressed) || Input.GetKey(KeyCode.E)) { horizontal += sharpSteerMultiplier; sharpPressed = true; }
        }
        else
        {
            // Player 2: Arrow Keys (K/L for sharper turns)
            if ((kb != null && kb.upArrowKey.isPressed) || Input.GetKey(KeyCode.UpArrow)) vertical += 1f;
            if ((kb != null && kb.downArrowKey.isPressed) || Input.GetKey(KeyCode.DownArrow)) vertical -= 1f;
            if ((kb != null && kb.leftArrowKey.isPressed) || Input.GetKey(KeyCode.LeftArrow)) horizontal -= 1f;
            if ((kb != null && kb.rightArrowKey.isPressed) || Input.GetKey(KeyCode.RightArrow)) horizontal += 1f;
            if ((kb != null && kb.kKey.isPressed) || Input.GetKey(KeyCode.K)) { horizontal -= sharpSteerMultiplier; sharpPressed = true; }
            if ((kb != null && kb.lKey.isPressed) || Input.GetKey(KeyCode.L)) { horizontal += sharpSteerMultiplier; sharpPressed = true; }
        }

        moveInput = new Vector2(horizontal, vertical);
        IsSharpSteering = sharpPressed;

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

    // ==================== PLAYER vs PLAYER RAMMING ====================

    private void OnCollisionEnter(Collision collision)
    {
        TryRam(collision);
    }

    /// <summary>
    /// Wird von ChariotPhysics aufgerufen wenn der Wagen selbst in etwas reinfährt
    /// — der Wagen kann ja auch die Kollision mit dem anderen Spieler auslösen.
    /// </summary>
    public void HandleChariotCollision(Collision collision)
    {
        TryRam(collision);
    }

    private void TryRam(Collision collision)
    {
        if (rb == null) return;
        if (Time.time - lastRamTime < ramCooldown) return;

        Rigidbody otherHorseRb;
        Rigidbody otherChariotRb;
        FindOtherPlayerBodies(collision.collider, out otherHorseRb, out otherChariotRb);
        if (otherHorseRb == null) return;
        if (otherHorseRb == rb) return;

        // Kontaktgeometrie
        ContactPoint contact = collision.GetContact(0);
        Vector3 pushDirRaw = -contact.normal; // von uns weg Richtung Opfer
        pushDirRaw.y = 0f;
        if (pushDirRaw.sqrMagnitude < 0.0001f) return;
        Vector3 pushDir = pushDirRaw.normalized;

        // Nur horizontale Geschwindigkeiten zählen — vertikales Gehoppel soll nichts auslösen
        Vector3 myVelFlat = rb.linearVelocity; myVelFlat.y = 0f;
        Vector3 otherVelFlat = otherHorseRb.linearVelocity; otherVelFlat.y = 0f;

        float myImpact = Vector3.Dot(myVelFlat, pushDir);
        float otherImpact = Vector3.Dot(otherVelFlat, pushDir);
        float closingSpeed = myImpact - otherImpact;

        // Nur wir (der Angreifer) sollen schubsen — die Gegenseite sieht negative closingSpeed
        if (closingSpeed < minRamClosingSpeed) return;
        if (myImpact < 0.1f) return;

        // Mischung aus Normalen-Schubs und seitlichem Schubs (relativ zum Angreifer)
        // so fliegt das Opfer klar vom Kurs ab statt nur vorwärts gedrückt zu werden.
        Vector3 rightDir = Vector3.Cross(Vector3.up, transform.forward);
        if (rightDir.sqrMagnitude < 0.0001f) rightDir = Vector3.right;
        rightDir.Normalize();
        float sideSign = Mathf.Sign(Vector3.Dot(pushDir, rightDir));
        if (sideSign == 0f) sideSign = 1f;
        Vector3 lateralDir = rightDir * sideSign;

        Vector3 finalDir = Vector3.Lerp(pushDir, lateralDir, ramLateralBias);
        if (finalDir.sqrMagnitude < 0.0001f) finalDir = pushDir;
        finalDir.Normalize();

        float sharpMult = IsSharpSteering ? sharpSteerRamMultiplier : 1f;
        float magnitude = (ramBaseImpulse + ramImpulsePerSpeed * closingSpeed) * sharpMult;

        // Opfer-Pferde: voller Impuls + Drehimpuls
        otherHorseRb.AddForce(finalDir * magnitude, ForceMode.Impulse);
        otherHorseRb.AddTorque(Vector3.up * magnitude * ramAngularImpulseFactor * sideSign, ForceMode.Impulse);

        // Opfer-Wagen: anteiliger Impuls so fliegt auch der Wagen mit weg
        // (sonst würde der Joint nur nachziehen und der Effekt wirkt gedämpft)
        if (otherChariotRb != null && ramChariotImpulseRatio > 0f)
        {
            float chariotMag = magnitude * ramChariotImpulseRatio;
            otherChariotRb.AddForce(finalDir * chariotMag, ForceMode.Impulse);
            otherChariotRb.AddTorque(Vector3.up * chariotMag * ramAngularImpulseFactor * sideSign, ForceMode.Impulse);
        }

        lastRamTime = Time.time;

        if (debugLogRam)
        {
            Debug.Log($"[Ram P{playerIndex + 1}→] closing={closingSpeed:F2} m/s | sharp={IsSharpSteering} | " +
                      $"mult={sharpMult:F2} | impulse={magnitude:F0} Ns (horse) + " +
                      $"{magnitude * ramChariotImpulseRatio:F0} Ns (chariot) | sideSign={sideSign}");
        }
    }

    private void FindOtherPlayerBodies(Collider other, out Rigidbody horseRb, out Rigidbody chariotRb)
    {
        horseRb = null;
        chariotRb = null;

        // Fall 1: direkt in die Pferde des anderen Spielers gefahren
        HorseController hc = other.GetComponentInParent<HorseController>();
        if (hc != null && hc != this)
        {
            horseRb = hc.GetComponent<Rigidbody>();
            chariotRb = FindChariotRigidbodyFor(horseRb);
            return;
        }

        // Fall 2: in den Wagen des anderen Spielers gefahren — ChariotPhysics hält
        // die Referenz auf dessen Pferde-Rigidbody
        ChariotPhysics cp = other.GetComponentInParent<ChariotPhysics>();
        if (cp != null)
        {
            Rigidbody otherHorse = cp.HorsePairRigidbody;
            if (otherHorse != null && otherHorse != rb)
            {
                horseRb = otherHorse;
                chariotRb = cp.GetComponent<Rigidbody>();
            }
        }
    }

    private static Rigidbody FindChariotRigidbodyFor(Rigidbody horseRb)
    {
        if (horseRb == null) return null;
        ChariotPhysics[] all = Object.FindObjectsByType<ChariotPhysics>(FindObjectsSortMode.None);
        foreach (ChariotPhysics cp in all)
        {
            if (cp != null && cp.HorsePairRigidbody == horseRb)
            {
                return cp.GetComponent<Rigidbody>();
            }
        }
        return null;
    }
}
