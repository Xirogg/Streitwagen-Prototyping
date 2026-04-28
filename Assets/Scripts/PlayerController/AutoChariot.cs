using UnityEngine;

/// <summary>
/// Fährt den Streitwagen automatisch geradeaus mit konstanter Geschwindigkeit.
/// Gedacht für Demonstrationen / Setups in denen ein "Dummy-Wagen" einfach in einer Linie fahren soll.
///
/// Wenn <see cref="disablePlayerControl"/> aktiv ist, wird der HorseController auf
/// demselben GameObject deaktiviert — die Spielereingabe wirkt dann nur noch auf
/// die anderen Wagen, nicht auf diesen hier.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class AutoChariot : MonoBehaviour
{
    [Header("Auto Drive")]
    [Tooltip("Wenn aktiv, fährt der Wagen ab Spielstart automatisch geradeaus.")]
    [SerializeField] private bool autoDrive = true;

    [Tooltip("Konstante Vorwärtsgeschwindigkeit in m/s.")]
    [SerializeField] private float speed = 8f;

    [Header("Control Override")]
    [Tooltip("Deaktiviert den HorseController auf diesem GameObject, damit Tastatureingabe diesen Wagen nicht steuert.")]
    [SerializeField] private bool disablePlayerControl = true;

    [Header("Stability")]
    [Tooltip("Hält den Wagen auf seiner ursprünglichen Linie (kein seitliches Driften, keine Rotation).")]
    [SerializeField] private bool lockToStraightLine = true;

    private Rigidbody rb;
    private HorseController horseController;
    private Vector3 driveDirection;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        horseController = GetComponent<HorseController>();
    }

    private void Start()
    {
        driveDirection = transform.forward;

        if (disablePlayerControl && horseController != null)
        {
            horseController.enabled = false;
        }
    }

    private void FixedUpdate()
    {
        if (!autoDrive || rb == null) return;

        if (lockToStraightLine)
        {
            // Geschwindigkeit exakt auf die Startrichtung setzen — keine Seitenabweichung, keine Rotation.
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = driveDirection * speed + Vector3.up * v.y;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            // Frei: nur Vorwärtsgeschwindigkeit erzwingen, Rotation/Drift bleibt physikalisch.
            Vector3 fwd = transform.forward;
            Vector3 v = rb.linearVelocity;
            Vector3 horiz = new Vector3(v.x, 0f, v.z);
            Vector3 target = fwd * speed;
            rb.linearVelocity = new Vector3(target.x, v.y, target.z);
        }
    }

    public void SetSpeed(float newSpeed) => speed = newSpeed;
    public void SetAutoDrive(bool enabled) => autoDrive = enabled;
}
