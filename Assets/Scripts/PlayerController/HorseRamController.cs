using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eigenständige Ram-Mechanik für die Pferde.
/// Liegt auf dem gleichen GameObject wie HorseController und PlayerCollisions, ist aber komplett
/// unabhängig vom Friendslop-Knockback in PlayerCollisions.
///
/// Aktiviert wenn der Spieler Q/E (P1) bzw. K/L (P2) drückt OHNE gleichzeitig A/D bzw. Pfeile zu halten.
/// In dem Fall lenkt das Pferd nicht. Stattdessen wird per Tag-basiertem Distanz-Scan geprüft, ob ein
/// anderer Spieler innerhalb von <see cref="maxRamDistance"/> ist. Bleibt mindestens ein Gegner so lange
/// in Reichweite (= <see cref="ramHoldDuration"/> Sekunden während Q/E gehalten wird), gilt der Ram als
/// erfolgreich:
///   * <see cref="OnRamSuccess"/> wird gefeuert (Hook für eine spätere Schadenslogik).
///   * Am Endpunkt der "Kollision" (nächster Punkt zwischen Rammer und Opfer) wird das
///     <see cref="ramDebrisPrefab"/> instanziiert.
///
/// Wir nutzen bewusst keine Trigger-Collider — ein riesiger Trigger-Collider würde mit Wänden / Objekten
/// kollidieren und die Physik komplizieren. Stattdessen suchen wir per Tag alle Spieler-GameObjects.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(HorseController))]
public class HorseRamController : MonoBehaviour
{
    [Header("Ram Timing")]
    [Tooltip("Wie lange (s) der Spieler den Gegner durchgehend mit Q/E in Reichweite halten muss damit der Ram \"trifft\".")]
    [SerializeField] private float ramHoldDuration = 2f;
    [Tooltip("Wie lange (s) Reichweitenverlust toleriert wird bevor der Akku zurückgesetzt wird.")]
    [SerializeField] private float ramContactGrace = 0.15f;
    [Tooltip("Cooldown (s) zwischen zwei erfolgreichen Rams.")]
    [SerializeField] private float ramSuccessCooldown = 1f;

    [Header("Ram Range")]
    [Tooltip("Maximaler Abstand (m) zwischen Rammer und Opfer, ab dem der Ram-Akku zählt. Deutlich grösser als ein normaler Collider-Kontakt — die Mechanik soll auch dann zählen wenn die Wagen sich nicht direkt berühren.")]
    [SerializeField] private float maxRamDistance = 8f;
    [Tooltip("Tag der für die Spielersuche benutzt wird. Standard: \"Player\".")]
    [SerializeField] private string playerTag = "Player";
    [Tooltip("Wenn aktiv, wird der Tag-Scan gecached. Bei Bedarf (Spieler werden zur Laufzeit gespawnt) deaktivieren.")]
    [SerializeField] private bool cachePlayerList = true;
    [Tooltip("Wenn aktiv, muss der Gegner ungefähr vor dem Rammer liegen (Punktprodukt-Schwelle). 0 = überall, 1 = exakt voraus.")]
    [Range(-1f, 1f)]
    [SerializeField] private float forwardDotThreshold = -0.25f;

    [Header("Spawn")]
    [Tooltip("Prefab das beim erfolgreichen Ram am Endpunkt der \"Kollision\" gespawnt wird (z.B. Wagen-Debris).")]
    [SerializeField] private GameObject ramDebrisPrefab;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    [Tooltip("Zeichnet Reichweite und aktuelles Ziel im Editor.")]
    [SerializeField] private bool drawGizmos = true;

    private Rigidbody rb;
    private HorseController horseController;

    // Cache der Player-GameObjects (per Tag).
    private readonly List<GameObject> cachedPlayers = new List<GameObject>();
    private bool playersCached;

    // --- Ram-State ---
    private Rigidbody ramTargetHorseRb;
    private Rigidbody ramTargetChariotRb;
    private GameObject ramTargetRoot;
    private float ramAccumTime;
    private float lastInRangeTime = -999f;
    private Vector3 lastEndpoint;
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

    /// <summary>Konfigurierter Reichweitenradius. Auch zur Laufzeit anpassbar.</summary>
    public float MaxRamDistance
    {
        get => maxRamDistance;
        set => maxRamDistance = Mathf.Max(0f, value);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        horseController = GetComponent<HorseController>();
    }

    private void OnEnable()
    {
        playersCached = false;
        cachedPlayers.Clear();
    }

    private void FixedUpdate()
    {
        // Spieler hält Q/E nicht (mehr) → Akku zurücksetzen.
        if (horseController == null || !horseController.IsRamAttempting)
        {
            ResetRam();
            return;
        }

        if (Time.time - lastSuccessTime < ramSuccessCooldown)
        {
            return;
        }

        TryAcquireTarget();

        if (Time.time - lastInRangeTime > ramContactGrace)
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

    private void TryAcquireTarget()
    {
        EnsurePlayersCached();

        Vector3 myPos = transform.position;
        Vector3 myForward = transform.forward;

        GameObject bestRoot = null;
        float bestSqr = maxRamDistance * maxRamDistance;
        Vector3 bestEndpoint = Vector3.zero;
        Rigidbody bestHorseRb = null;
        Rigidbody bestChariotRb = null;

        for (int i = 0; i < cachedPlayers.Count; i++)
        {
            GameObject playerObj = cachedPlayers[i];
            if (playerObj == null) continue;
            if (IsSelf(playerObj)) continue;

            Vector3 otherPos = playerObj.transform.position;
            Vector3 toOther = otherPos - myPos;
            toOther.y = 0f;
            float sqr = toOther.sqrMagnitude;
            if (sqr > bestSqr) continue;

            // Optional: Sichtkegel — der Gegner muss grob vor uns sein.
            if (forwardDotThreshold > -1f && sqr > 0.0001f)
            {
                Vector3 dir = toOther / Mathf.Sqrt(sqr);
                if (Vector3.Dot(myForward, dir) < forwardDotThreshold) continue;
            }

            Rigidbody horseRb;
            Rigidbody chariotRb;
            ResolvePlayerBodies(playerObj, out horseRb, out chariotRb);
            if (horseRb == null || horseRb == rb) continue;

            bestSqr = sqr;
            bestRoot = playerObj;
            bestHorseRb = horseRb;
            bestChariotRb = chariotRb;
            bestEndpoint = ComputeEndpoint(horseRb, chariotRb, otherPos);
        }

        if (bestRoot == null) return;

        // Wechsel des Gegners → Akku auf 0 zurücksetzen.
        if (ramTargetRoot != bestRoot)
        {
            ramTargetRoot = bestRoot;
            ramTargetHorseRb = bestHorseRb;
            ramTargetChariotRb = bestChariotRb;
            ramAccumTime = 0f;
        }
        else
        {
            // Refresh chariot-Referenz falls jetzt verfügbar.
            if (ramTargetChariotRb == null && bestChariotRb != null)
            {
                ramTargetChariotRb = bestChariotRb;
            }
        }

        lastEndpoint = bestEndpoint;
        lastInRangeTime = Time.time;
    }

    private Vector3 ComputeEndpoint(Rigidbody otherHorseRb, Rigidbody otherChariotRb, Vector3 otherRootPos)
    {
        // Endpunkt = Mittelpunkt zwischen Rammer und Opfer auf Höhe des Opfer-Bodens.
        // Damit landet das Debris-Prefab dort wo eine echte Kollision stattgefunden hätte.
        Vector3 myPos = transform.position;
        Vector3 otherPos = otherHorseRb != null ? otherHorseRb.position : otherRootPos;
        Vector3 mid = (myPos + otherPos) * 0.5f;
        mid.y = Mathf.Min(myPos.y, otherPos.y);
        return mid;
    }

    private void CompleteRam()
    {
        Vector3 endpoint = lastEndpoint;

        if (ramDebrisPrefab != null)
        {
            Instantiate(ramDebrisPrefab, endpoint, Quaternion.identity);
        }

        // Schaden-Hook — die eigentliche Schadenslogik wird woanders implementiert.
        OnRamSuccess?.Invoke(ramTargetHorseRb, ramTargetChariotRb, endpoint);

        if (debugLog)
        {
            string victim = ramTargetRoot != null ? ramTargetRoot.name : "?";
            Debug.Log($"[HorseRamController {name}] RAM SUCCESS gegen {victim} bei {endpoint}");
        }

        lastSuccessTime = Time.time;
        ResetRam();
    }

    private void ResetRam()
    {
        ramTargetHorseRb = null;
        ramTargetChariotRb = null;
        ramTargetRoot = null;
        ramAccumTime = 0f;
    }

    private bool IsSelf(GameObject playerObj)
    {
        if (playerObj == gameObject) return true;
        // Auch wenn der Tag z.B. auf einem Wagen-Root sitzt: vergleiche per Rigidbody.
        Rigidbody otherRb = playerObj.GetComponentInChildren<Rigidbody>();
        return otherRb == rb;
    }

    private void EnsurePlayersCached()
    {
        if (cachePlayerList && playersCached) return;
        cachedPlayers.Clear();
        if (string.IsNullOrEmpty(playerTag)) return;
        GameObject[] found = GameObject.FindGameObjectsWithTag(playerTag);
        cachedPlayers.AddRange(found);
        playersCached = true;
    }

    /// <summary>
    /// Cache für Player-GameObjects manuell invalidieren — z.B. wenn zur Laufzeit ein Spieler dazukommt
    /// oder verschwindet.
    /// </summary>
    public void InvalidatePlayerCache()
    {
        playersCached = false;
        cachedPlayers.Clear();
    }

    private void ResolvePlayerBodies(GameObject playerObj, out Rigidbody horseRb, out Rigidbody chariotRb)
    {
        horseRb = null;
        chariotRb = null;

        // Variante A: der getaggte Knoten ist (oder enthält) den HorseController.
        HorseController hc = playerObj.GetComponentInChildren<HorseController>();
        if (hc != null)
        {
            horseRb = hc.GetComponent<Rigidbody>();
            chariotRb = FindChariotRigidbodyFor(horseRb);
            return;
        }

        // Variante B: der getaggte Knoten ist (oder enthält) den Wagen.
        ChariotPhysics cp = playerObj.GetComponentInChildren<ChariotPhysics>();
        if (cp != null)
        {
            horseRb = cp.HorsePairRigidbody;
            chariotRb = cp.GetComponent<Rigidbody>();
            return;
        }

        // Variante C: der Tag sitzt auf einem gemeinsamen Parent über Pferden + Wagen.
        HorseController hcParent = playerObj.GetComponentInParent<HorseController>();
        if (hcParent != null)
        {
            horseRb = hcParent.GetComponent<Rigidbody>();
            chariotRb = FindChariotRigidbodyFor(horseRb);
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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, maxRamDistance);
        if (ramTargetRoot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, ramTargetRoot.transform.position);
            Gizmos.DrawWireSphere(lastEndpoint, 0.3f);
        }
    }
#endif
}
