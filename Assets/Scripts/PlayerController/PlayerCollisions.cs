using UnityEngine;

/// <summary>
/// Friendslop-Style Player-vs-Player Collisions.
/// Wird auf das Pferde-GameObject gelegt (gleiches GameObject wie HorseController).
/// Empfängt Kollisionen direkt von den Pferden (OnCollisionEnter) und vom eigenen
/// Wagen via HandleChariotCollision (von ChariotPhysics weitergereicht).
///
/// Der Treffer ist absichtlich überzogen: der Gegner soll richtig schön wegfliegen,
/// inkl. einem Hauch Vertikalimpuls, ordentlich Spin und einem dicken Mindestschubs.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerCollisions : MonoBehaviour
{
    [Header("Friendslop Impulse")]
    [Tooltip("Konstanter Grund-Impuls (Ns) bei jedem gültigen Treffer.")]
    [SerializeField] private float baseImpulse = 3500f;
    [Tooltip("Zusätzlicher Impuls (Ns) pro m/s Schliessgeschwindigkeit.")]
    [SerializeField] private float impulsePerClosingSpeed = 1100f;
    [Tooltip("Maximaler Gesamt-Impuls pro Treffer (Ns) — Sicherheitsdeckel damit es nicht ins Weltall geht.")]
    [SerializeField] private float maxImpulse = 18000f;

    [Header("Direction Mix")]
    [Tooltip("0 = rein entlang Kontakt-Normale, 1 = rein seitlich vom Angreifer weg.")]
    [Range(0f, 1f)]
    [SerializeField] private float lateralBias = 0.55f;
    [Tooltip("Vertikaler Anteil der den Wagen kurz abheben lässt (0 = kein Lift).")]
    [Range(0f, 1f)]
    [SerializeField] private float verticalLift = 0.35f;

    [Header("Spin")]
    [Tooltip("Drehimpuls auf das Opfer relativ zum linearen Impuls.")]
    [SerializeField] private float angularImpulseFactor = 0.45f;
    [Tooltip("Wagen bekommt zusätzlich einen extra wilden Spin-Faktor.")]
    [SerializeField] private float chariotExtraSpin = 1.6f;

    [Header("Chariot Coupling")]
    [Tooltip("Anteil vom Impuls der zusätzlich auf den Wagen des Opfers geht. >1 = Wagen fliegt härter als die Pferde.")]
    [SerializeField, Range(0f, 2f)] private float chariotImpulseRatio = 1.1f;

    [Header("Activation")]
    [Tooltip("Mindest-Schliessgeschwindigkeit (m/s) damit ein Schubs ausgelöst wird. Klein halten für Friendslop.")]
    [SerializeField] private float minClosingSpeed = 0.8f;
    [Tooltip("Mindest-zeitlicher Abstand zwischen zwei Treffern gegen denselben Gegner.")]
    [SerializeField] private float cooldown = 0.2f;
    [Tooltip("Multiplikator wenn der Angreifer gerade Q/E bzw. K/L hält.")]
    [SerializeField] private float sharpSteerMultiplier = 1.8f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    private Rigidbody rb;
    private HorseController horseController;
    private float lastRamTime = -999f;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        horseController = GetComponent<HorseController>();
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryRam(collision);
    }

    /// <summary>
    /// Wird von ChariotPhysics aufgerufen wenn der eigene Wagen mit etwas kollidiert.
    /// </summary>
    public void HandleChariotCollision(Collision collision)
    {
        TryRam(collision);
    }

    private void TryRam(Collision collision)
    {
        if (rb == null) return;
        if (Time.time - lastRamTime < cooldown) return;

        Rigidbody otherHorseRb;
        Rigidbody otherChariotRb;
        FindOtherPlayerBodies(collision.collider, out otherHorseRb, out otherChariotRb);
        if (otherHorseRb == null || otherHorseRb == rb) return;

        ContactPoint contact = collision.GetContact(0);
        Vector3 pushDirRaw = -contact.normal;
        pushDirRaw.y = 0f;
        if (pushDirRaw.sqrMagnitude < 0.0001f) return;
        Vector3 pushDir = pushDirRaw.normalized;

        Vector3 myVelFlat = rb.linearVelocity; myVelFlat.y = 0f;
        Vector3 otherVelFlat = otherHorseRb.linearVelocity; otherVelFlat.y = 0f;

        float myImpact = Vector3.Dot(myVelFlat, pushDir);
        float otherImpact = Vector3.Dot(otherVelFlat, pushDir);
        float closingSpeed = myImpact - otherImpact;

        // Nur der Angreifer schubst — die Gegenseite sieht negative closingSpeed
        if (closingSpeed < minClosingSpeed) return;
        if (myImpact < 0.05f) return;

        Vector3 finalDir = BuildFinalDirection(pushDir);

        bool sharp = horseController != null && horseController.IsSharpSteering;
        float sharpMult = sharp ? sharpSteerMultiplier : 1f;

        float magnitude = (baseImpulse + impulsePerClosingSpeed * closingSpeed) * sharpMult;
        magnitude = Mathf.Min(magnitude, maxImpulse);

        // Side-sign relativ zum Angreifer für konsistenten Drehsinn
        Vector3 rightDir = Vector3.Cross(Vector3.up, transform.forward);
        if (rightDir.sqrMagnitude < 0.0001f) rightDir = Vector3.right;
        rightDir.Normalize();
        float sideSign = Mathf.Sign(Vector3.Dot(pushDir, rightDir));
        if (sideSign == 0f) sideSign = 1f;

        // Opfer-Pferde
        otherHorseRb.AddForce(finalDir * magnitude, ForceMode.Impulse);
        otherHorseRb.AddTorque(Vector3.up * magnitude * angularImpulseFactor * sideSign, ForceMode.Impulse);

        // Opfer-Wagen — fliegt stärker und dreht sich wilder
        if (otherChariotRb != null && chariotImpulseRatio > 0f)
        {
            float chariotMag = magnitude * chariotImpulseRatio;
            otherChariotRb.AddForce(finalDir * chariotMag, ForceMode.Impulse);
            otherChariotRb.AddTorque(Vector3.up * chariotMag * angularImpulseFactor * chariotExtraSpin * sideSign, ForceMode.Impulse);
        }

        lastRamTime = Time.time;

        if (debugLog)
        {
            Debug.Log($"[PlayerCollisions {name}] closing={closingSpeed:F2} m/s | sharp={sharp} | " +
                      $"impulse={magnitude:F0} Ns (horse) + {magnitude * chariotImpulseRatio:F0} Ns (chariot) | " +
                      $"dir={finalDir} | sideSign={sideSign}");
        }
    }

    private Vector3 BuildFinalDirection(Vector3 pushDir)
    {
        Vector3 rightDir = Vector3.Cross(Vector3.up, transform.forward);
        if (rightDir.sqrMagnitude < 0.0001f) rightDir = Vector3.right;
        rightDir.Normalize();
        float sideSign = Mathf.Sign(Vector3.Dot(pushDir, rightDir));
        if (sideSign == 0f) sideSign = 1f;
        Vector3 lateralDir = rightDir * sideSign;

        Vector3 horizontal = Vector3.Lerp(pushDir, lateralDir, lateralBias);
        if (horizontal.sqrMagnitude < 0.0001f) horizontal = pushDir;
        horizontal.Normalize();

        // Vertikalanteil reinmischen — Wagen hebt kurz ab für den Slop-Effekt
        Vector3 mixed = horizontal * (1f - verticalLift) + Vector3.up * verticalLift;
        return mixed.normalized;
    }

    private void FindOtherPlayerBodies(Collider other, out Rigidbody horseRb, out Rigidbody chariotRb)
    {
        horseRb = null;
        chariotRb = null;

        // Fall 1: direkt in die Pferde des anderen Spielers gefahren
        HorseController hc = other.GetComponentInParent<HorseController>();
        if (hc != null && hc.GetComponent<Rigidbody>() != rb)
        {
            horseRb = hc.GetComponent<Rigidbody>();
            chariotRb = FindChariotRigidbodyFor(horseRb);
            return;
        }

        // Fall 2: in den Wagen des anderen Spielers gefahren
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
