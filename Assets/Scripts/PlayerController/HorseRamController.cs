using UnityEngine;

/// <summary>
/// Eigenständige Ram-Mechanik für die Pferde.
/// Liegt auf dem gleichen GameObject wie HorseController und PlayerCollisions, ist aber komplett
/// unabhängig vom Friendslop-Knockback in PlayerCollisions.
///
/// Aktiviert wenn der Spieler Q/E (P1) bzw. K/L (P2) drückt OHNE gleichzeitig A/D bzw. Pfeile zu halten.
/// In dem Fall lenkt das Pferd nicht, sondern es wird geprüft ob der Wagen mit einem anderen Spieler
/// in Kontakt steht. Bleibt der Kontakt für <see cref="ramHoldDuration"/> Sekunden ununterbrochen
/// während Q/E gehalten wird, gilt der Ram als erfolgreich:
///   * <see cref="OnRamSuccess"/> wird gefeuert (Hook für eine spätere Schadenslogik).
///   * Am Endpunkt der Kollision wird das <see cref="ramDebrisPrefab"/> instanziiert.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HorseController))]
public class HorseRamController : MonoBehaviour
{
    [Header("Ram Timing")]
    [Tooltip("Wie lange (s) der Spieler den Gegner durchgehend mit Q/E rammen muss damit der Ram \"trifft\".")]
    [SerializeField] private float ramHoldDuration = 2f;
    [Tooltip("Wie lange (s) ohne Kontakt toleriert wird bevor der Akku zurückgesetzt wird.")]
    [SerializeField] private float ramContactGrace = 0.15f;
    [Tooltip("Cooldown (s) zwischen zwei erfolgreichen Rams.")]
    [SerializeField] private float ramSuccessCooldown = 1f;

    [Header("Spawn")]
    [Tooltip("Prefab das beim erfolgreichen Ram am Endpunkt der Kollision gespawnt wird (z.B. Wagen-Debris).")]
    [SerializeField] private GameObject ramDebrisPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Rigidbody rb;
    private HorseController horseController;

    // --- Ram-State ---
    private Rigidbody ramTargetHorseRb;
    private Rigidbody ramTargetChariotRb;
    private float ramAccumTime;
    private float lastContactTime = -999f;
    private Vector3 lastContactPoint;
    private float lastSuccessTime = -999f;

    /// <summary>
    /// Wird gefeuert sobald ein Ram erfolgreich war.
    /// Hier kann später die Schadenslogik angedockt werden.
    /// Args: opfer-Pferde-Rigidbody, opfer-Wagen-Rigidbody (kann null sein), Endpunkt der Kollision.
    /// </summary>
    public event System.Action<Rigidbody, Rigidbody, Vector3> OnRamSuccess;

    /// <summary>Aktuell akkumulierte Ram-Zeit (für UI / Debug).</summary>
    public float CurrentRamProgress => ramAccumTime;

    /// <summary>Sekundenlimit für einen erfolgreichen Ram.</summary>
    public float RamHoldDuration => ramHoldDuration;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        horseController = GetComponent<HorseController>();
    }

    private void OnCollisionStay(Collision collision)
    {
        TrackContact(collision);
    }

    /// <summary>
    /// Wird von <see cref="ChariotPhysics"/> jeden FixedUpdate aufgerufen während der eigene Wagen
    /// mit einem anderen Collider in Kontakt ist.
    /// </summary>
    public void HandleChariotCollisionStay(Collision collision)
    {
        TrackContact(collision);
    }

    private void FixedUpdate()
    {
        // Spieler hält Q/E nicht (mehr) → Akku zurücksetzen.
        if (horseController == null || !horseController.IsRamAttempting)
        {
            ResetRam();
            return;
        }

        // Kein aktueller Kontakt mit einem Gegner → Akku zurücksetzen.
        if (Time.time - lastContactTime > ramContactGrace)
        {
            ResetRam();
            return;
        }

        ramAccumTime += Time.fixedDeltaTime;
        if (ramAccumTime >= ramHoldDuration)
        {
            CompleteRam();
        }
    }

    private void TrackContact(Collision collision)
    {
        if (rb == null) return;
        if (horseController == null || !horseController.IsRamAttempting) return;
        if (Time.time - lastSuccessTime < ramSuccessCooldown) return;

        Rigidbody otherHorseRb;
        Rigidbody otherChariotRb;
        FindOtherPlayerBodies(collision.collider, out otherHorseRb, out otherChariotRb);
        if (otherHorseRb == null || otherHorseRb == rb) return;

        // Wechsel des Gegners → bei dem neuen Gegner wieder bei 0 anfangen.
        if (ramTargetHorseRb != otherHorseRb)
        {
            ramTargetHorseRb = otherHorseRb;
            ramTargetChariotRb = otherChariotRb;
            ramAccumTime = 0f;
        }
        else if (otherChariotRb != null)
        {
            ramTargetChariotRb = otherChariotRb;
        }

        ContactPoint contact = collision.GetContact(0);
        lastContactPoint = contact.point;
        lastContactTime = Time.time;
    }

    private void CompleteRam()
    {
        Vector3 endpoint = lastContactPoint;

        if (ramDebrisPrefab != null)
        {
            Instantiate(ramDebrisPrefab, endpoint, Quaternion.identity);
        }

        // Schaden-Hook — die eigentliche Schadenslogik wird woanders implementiert.
        OnRamSuccess?.Invoke(ramTargetHorseRb, ramTargetChariotRb, endpoint);

        if (debugLog)
        {
            string victim = ramTargetHorseRb != null ? ramTargetHorseRb.name : "?";
            Debug.Log($"[HorseRamController {name}] RAM SUCCESS gegen {victim} bei {endpoint}");
        }

        lastSuccessTime = Time.time;
        ResetRam();
    }

    private void ResetRam()
    {
        ramTargetHorseRb = null;
        ramTargetChariotRb = null;
        ramAccumTime = 0f;
    }

    private void FindOtherPlayerBodies(Collider other, out Rigidbody horseRb, out Rigidbody chariotRb)
    {
        horseRb = null;
        chariotRb = null;

        // Fall 1: in die Pferde des anderen Spielers gefahren.
        HorseController hc = other.GetComponentInParent<HorseController>();
        if (hc != null && hc.GetComponent<Rigidbody>() != rb)
        {
            horseRb = hc.GetComponent<Rigidbody>();
            chariotRb = FindChariotRigidbodyFor(horseRb);
            return;
        }

        // Fall 2: in den Wagen des anderen Spielers gefahren.
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
