using UnityEngine;

/// <summary>
/// Emits dirt particles based on the assigned chariot's speed.
/// Attach this to the Particles Prefab and assign the ChariotBody GameObject in the Inspector.
/// The ParticleSystem reference (DirtParticles) should point to the ParticleSystem on this prefab.
/// </summary>
public class ParticleScript : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("Assign the ChariotBody GameObject (e.g. ChariotBody_P1 or ChariotBody_P2)")]
    [SerializeField] private GameObject Chariot;

    [Header("Settings")]
    [Tooltip("Minimum speed before particles start emitting")]
    [SerializeField] private float speedThreshold = 3f;

    [Header("References")]
    [SerializeField] private ParticleSystem DirtParticles;

    [Header("Debug (Read-Only)")]
    [SerializeField] private float speed;

    private ChariotPhysics chariotPhysics;
    private bool isEmitting = false;

    void Start()
    {
        // Cache the ChariotPhysics reference once
        if (Chariot != null)
        {
            chariotPhysics = Chariot.GetComponent<ChariotPhysics>();
        }

        // Make sure particles are stopped at the start
        if (DirtParticles != null && DirtParticles.isPlaying)
        {
            DirtParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    void Update()
    {
        if (chariotPhysics == null)
        {
            // Try to re-acquire if chariot was assigned later
            if (Chariot != null)
            {
                chariotPhysics = Chariot.GetComponent<ChariotPhysics>();
            }
            return;
        }

        speed = chariotPhysics.currentSpeed;
        bool shouldPlay = speed > speedThreshold;

        // Only call Play/Stop on state change to avoid restarting every frame
        if (shouldPlay && !isEmitting)
        {
            // Enable looping so particles keep emitting while driving
            var main = DirtParticles.main;
            main.loop = true;
            DirtParticles.Play();
            isEmitting = true;
        }
        else if (!shouldPlay && isEmitting)
        {
            // Stop emitting but let existing particles fade out naturally
            var main = DirtParticles.main;
            main.loop = false;
            DirtParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            isEmitting = false;
        }
    }
}
