using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Manages god powers (Goetterkraefte).
/// Press B to trigger Lightning: all "Player" tagged objects shrink to 50%
/// and their max speed is halved for a duration, then everything restores.
/// </summary>
public class PowerManager : MonoBehaviour
{
    [Header("Lightning Settings")]
    [SerializeField] private float shrinkScale = 0.5f;
    [SerializeField] private float speedReduction = 0.5f;
    [SerializeField] private float effectDuration = 5f;

    private bool lightningActive = false;

    private struct AffectedPlayer
    {
        public GameObject gameObject;
        public Vector3 originalScale;
        public HorseController horseController;
    }

    private List<AffectedPlayer> affectedPlayers = new List<AffectedPlayer>();

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return;

        if (kb.bKey.wasPressedThisFrame && !lightningActive)
        {
            StartCoroutine(LightningStrike());
        }
    }

    private IEnumerator LightningStrike()
    {
        lightningActive = true;
        affectedPlayers.Clear();

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        // Shrink all players and reduce speed
        foreach (GameObject player in players)
        {
            AffectedPlayer ap = new AffectedPlayer
            {
                gameObject = player,
                originalScale = player.transform.localScale,
                horseController = player.GetComponentInChildren<HorseController>()
            };

            // If no HorseController on the object itself, check parent/siblings
            if (ap.horseController == null)
            {
                ap.horseController = player.GetComponentInParent<HorseController>();
            }

            player.transform.localScale = ap.originalScale * shrinkScale;

            if (ap.horseController != null)
            {
                ap.horseController.SetSpeedMultiplier(speedReduction);
            }

            affectedPlayers.Add(ap);
        }

        Debug.Log($"[PowerManager] Lightning! {affectedPlayers.Count} players affected for {effectDuration}s");

        yield return new WaitForSeconds(effectDuration);

        // Restore all players
        foreach (AffectedPlayer ap in affectedPlayers)
        {
            if (ap.gameObject != null)
            {
                ap.gameObject.transform.localScale = ap.originalScale;
            }

            if (ap.horseController != null)
            {
                ap.horseController.SetSpeedMultiplier(1f);
            }
        }

        affectedPlayers.Clear();
        lightningActive = false;

        Debug.Log("[PowerManager] Lightning effect ended, players restored.");
    }
}
