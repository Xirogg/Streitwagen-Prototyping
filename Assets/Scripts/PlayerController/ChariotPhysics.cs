using UnityEngine;

/// <summary>
/// Manages the chariot body physics. Sets up the ConfigurableJoint
/// to the horse pair and applies stabilization forces.
/// The chariot has NO self-propulsion - it is purely pulled by the horses.
/// </summary>
public class ChariotPhysics : MonoBehaviour
{
    [Header("Joint Configuration")]
    [SerializeField] private Rigidbody horsePairRb;
    [SerializeField] private float yawLimit = 45f;
    [SerializeField] private float pitchLimit = 10f;
    [SerializeField] private float rollLimit = 5f;
    [SerializeField] private float yawSpring = 50f;
    [SerializeField] private float yawDamper = 10f;

    [Header("Stability")]
    [SerializeField] private float downforce = 200f;
    [SerializeField] private float antiFlipTorque = 500f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;
    public float currentSpeed;
    public float driftAngle;

    private Rigidbody rb;
    private ConfigurableJoint joint;
    private float debugTimer;
    private PlayerCollisions ownerCollisions;

    /// <summary>
    /// Referenz auf das Pferde-Rigidbody dieses Spielers — wird für Ram-Checks gebraucht,
    /// damit der andere Spieler erkennen kann zu wem der getroffene Wagen gehört.
    /// </summary>
    public Rigidbody HorsePairRigidbody => horsePairRb;

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
    }

    /// <summary>
    /// Call this after both transforms are positioned to create and configure the joint.
    /// Anchors are computed from actual world positions so they always match.
    /// </summary>
    public void SetupJoint(Rigidbody horseRb)
    {
        horsePairRb = horseRb;

        // Calculate the connection point in world space: midpoint between horse rear and chariot front
        Vector3 horsePos = horsePairRb.transform.position;
        Vector3 chariotPos = transform.position;
        Vector3 connectionWorldPos = (horsePos + chariotPos) * 0.5f;
        // Use the horse's Y so the drawbar is level with the horse
        connectionWorldPos.y = Mathf.Min(horsePos.y, chariotPos.y);

        Debug.Log($"[ChariotPhysics] Horse pos: {horsePos}");
        Debug.Log($"[ChariotPhysics] Chariot pos: {chariotPos}");
        Debug.Log($"[ChariotPhysics] Connection world pos: {connectionWorldPos}");

        // Convert to local space for each body
        Vector3 chariotLocalAnchor = transform.InverseTransformPoint(connectionWorldPos);
        Vector3 horseLocalAnchor = horsePairRb.transform.InverseTransformPoint(connectionWorldPos);

        Debug.Log($"[ChariotPhysics] Chariot anchor (local): {chariotLocalAnchor}");
        Debug.Log($"[ChariotPhysics] Horse anchor (local): {horseLocalAnchor}");

        joint = gameObject.AddComponent<ConfigurableJoint>();
        joint.connectedBody = horsePairRb;
        joint.autoConfigureConnectedAnchor = false;

        // Anchors computed from actual positions - guaranteed to match
        joint.anchor = chariotLocalAnchor;
        joint.connectedAnchor = horseLocalAnchor;

        // Linear motion: locked (rigid drawbar)
        joint.xMotion = ConfigurableJointMotion.Locked;
        joint.yMotion = ConfigurableJointMotion.Locked;
        joint.zMotion = ConfigurableJointMotion.Locked;

        // Angular X (pitch): limited
        joint.angularXMotion = ConfigurableJointMotion.Limited;
        joint.lowAngularXLimit = new SoftJointLimit { limit = -pitchLimit };
        joint.highAngularXLimit = new SoftJointLimit { limit = pitchLimit };

        // Angular Y (yaw): limited - THIS IS THE DRIFT AXIS
        joint.angularYMotion = ConfigurableJointMotion.Limited;
        joint.angularYLimit = new SoftJointLimit { limit = yawLimit };

        // Angular Z (roll): limited
        joint.angularZMotion = ConfigurableJointMotion.Limited;
        joint.angularZLimit = new SoftJointLimit { limit = rollLimit };

        // Yaw drive: gentle spring pulls chariot back in line
        JointDrive yzDrive = new JointDrive
        {
            positionSpring = yawSpring,
            positionDamper = yawDamper,
            maximumForce = float.MaxValue
        };
        joint.angularYZDrive = yzDrive;
        joint.rotationDriveMode = RotationDriveMode.XYAndZ;

        // Projection - loosened to avoid fighting movement
        joint.projectionMode = JointProjectionMode.PositionAndRotation;
        joint.projectionDistance = 0.1f;
        joint.projectionAngle = 5f;

        joint.enableCollision = false;
        joint.enablePreprocessing = true;

        Debug.Log($"[ChariotPhysics] Joint setup complete! " +
                  $"Yaw limit={yawLimit}, Spring={yawSpring}, Damper={yawDamper}");
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Wagen-Kollision an die PlayerCollisions des eigenen Spielers weiterleiten,
        // damit auch ein Wagen-gegen-Wagen oder Wagen-gegen-Pferde Treffer als Ram gewertet wird.
        EnsureOwnerCollisions();
        if (ownerCollisions != null)
        {
            ownerCollisions.HandleChariotCollision(collision);
        }
    }

    private void OnCollisionStay(Collision collision)
    {
        // Während der Kontakt anhält, an PlayerCollisions weitergeben — wird für die Ram-Akkumulation
        // (Q/E ohne A/D, 2 Sekunden Kontakt) gebraucht.
        EnsureOwnerCollisions();
        if (ownerCollisions != null)
        {
            ownerCollisions.HandleChariotCollisionStay(collision);
        }
    }

    private void EnsureOwnerCollisions()
    {
        if (ownerCollisions == null && horsePairRb != null)
        {
        }
    }

    private void FixedUpdate()
    {
        if (rb == null) InitRigidbody();
        if (rb == null) return;

        ApplyDownforce();
        ApplyAntiFlip();
        UpdateTelemetry();

        if (debugLog)
        {
            debugTimer += Time.fixedDeltaTime;
            if (debugTimer >= 0.5f)
            {
                debugTimer = 0f;
                Debug.Log($"[Chariot] Speed={currentSpeed:F2} m/s | DriftAngle={driftAngle:F1} | " +
                          $"Pos={transform.position} | Vel={rb.linearVelocity} | " +
                          $"isSleeping={rb.IsSleeping()} | isKinematic={rb.isKinematic}");

                if (joint != null)
                {
                    Debug.Log($"[Chariot Joint] currentForce={joint.currentForce} | " +
                              $"currentTorque={joint.currentTorque}");
                }
            }
        }
    }

    private void ApplyDownforce()
    {
        rb.AddForce(Vector3.down * downforce, ForceMode.Force);
    }

    private void ApplyAntiFlip()
    {
        float tiltX = NormalizeAngle(transform.eulerAngles.x);
        float tiltZ = NormalizeAngle(transform.eulerAngles.z);

        Vector3 correctionTorque = Vector3.zero;
        correctionTorque += transform.right * (-tiltX * antiFlipTorque * Time.fixedDeltaTime);
        correctionTorque += transform.forward * (-tiltZ * antiFlipTorque * Time.fixedDeltaTime);
        rb.AddTorque(correctionTorque, ForceMode.Force);
    }

    private void UpdateTelemetry()
    {
        currentSpeed = rb.linearVelocity.magnitude;

        if (currentSpeed > 0.5f)
        {
            driftAngle = Vector3.Angle(rb.linearVelocity, transform.forward);
        }
        else
        {
            driftAngle = 0f;
        }
    }

    private static float NormalizeAngle(float angle)
    {
        if (angle > 180f) angle -= 360f;
        return angle;
    }
}
