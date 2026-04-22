using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Rein visuelles Skript: dreht in scharfen Kurven (Q/E bzw. K/L) das kurveninnere Pferd
/// leicht in die Kurve rein. Beeinflusst die Fahrphysik nicht — es werden nur die
/// lokalen Rotationen der zugewiesenen Pferde-Transforms angepasst.
///
/// Bei normalem Lenken (A/D bzw. Pfeiltasten) passiert nichts.
/// </summary>
public class HorseVisuals : MonoBehaviour
{
    [Header("Player")]
    [Tooltip("0 = Spieler 1 (Q/E), 1 = Spieler 2 (K/L)")]
    [SerializeField] private int playerIndex = 0;

    [Header("Horse Transforms")]
    [Tooltip("Das linke Pferd (aus Fahrtrichtung gesehen). Wird bei Q/K in die Kurve gedreht.")]
    [SerializeField] private Transform leftHorse;
    [Tooltip("Das rechte Pferd (aus Fahrtrichtung gesehen). Wird bei E/L in die Kurve gedreht.")]
    [SerializeField] private Transform rightHorse;

    [Header("Visual Tuning")]
    [Tooltip("Maximaler Ausschlag (Grad) des inneren Pferdes in die Kurve.")]
    [SerializeField] private float maxTurnAngle = 18f;
    [Tooltip("Wie schnell das Pferd in die Kurve eindreht.")]
    [SerializeField] private float turnInSpeed = 8f;
    [Tooltip("Wie schnell das Pferd zurück in die Ausgangsposition dreht.")]
    [SerializeField] private float returnSpeed = 6f;

    private Quaternion leftOriginalLocalRot;
    private Quaternion rightOriginalLocalRot;

    private float leftCurrentAngle;
    private float rightCurrentAngle;

    public void SetPlayerIndex(int index)
    {
        playerIndex = index;
    }

    private void Awake()
    {
        if (leftHorse != null) leftOriginalLocalRot = leftHorse.localRotation;
        if (rightHorse != null) rightOriginalLocalRot = rightHorse.localRotation;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying) return;

        float leftTarget = 0f;
        float rightTarget = 0f;

        if (HorseController.MovementEnabled)
        {
            Keyboard kb = Keyboard.current;
            bool sharpLeft = false;
            bool sharpRight = false;

            if (playerIndex == 0)
            {
                sharpLeft = (kb != null && kb.qKey.isPressed) || Input.GetKey(KeyCode.Q);
                sharpRight = (kb != null && kb.eKey.isPressed) || Input.GetKey(KeyCode.E);
            }
            else
            {
                sharpLeft = (kb != null && kb.kKey.isPressed) || Input.GetKey(KeyCode.K);
                sharpRight = (kb != null && kb.lKey.isPressed) || Input.GetKey(KeyCode.L);
            }

            // Scharfe Linkskurve -> linkes (inneres) Pferd dreht nach links (-Y)
            if (sharpLeft) leftTarget = -maxTurnAngle;
            // Scharfe Rechtskurve -> rechtes (inneres) Pferd dreht nach rechts (+Y)
            if (sharpRight) rightTarget = maxTurnAngle;
        }

        float leftSpeed = Mathf.Approximately(leftTarget, 0f) ? returnSpeed : turnInSpeed;
        float rightSpeed = Mathf.Approximately(rightTarget, 0f) ? returnSpeed : turnInSpeed;

        leftCurrentAngle = Mathf.Lerp(leftCurrentAngle, leftTarget, 1f - Mathf.Exp(-leftSpeed * Time.deltaTime));
        rightCurrentAngle = Mathf.Lerp(rightCurrentAngle, rightTarget, 1f - Mathf.Exp(-rightSpeed * Time.deltaTime));

        if (leftHorse != null)
        {
            leftHorse.localRotation = leftOriginalLocalRot * Quaternion.Euler(0f, leftCurrentAngle, 0f);
        }
        if (rightHorse != null)
        {
            rightHorse.localRotation = rightOriginalLocalRot * Quaternion.Euler(0f, rightCurrentAngle, 0f);
        }
    }
}
