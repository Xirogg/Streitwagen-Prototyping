using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Lightning: all "Player" tagged objects shrink and move slower for a duration.
/// </summary>
public class LightningPower : GodPower
{
    [Header("Lightning Settings")]
    [SerializeField] private float shrinkScale = 0.5f;
    [SerializeField] private float speedReduction = 0.5f;
    [SerializeField] private float effectDuration = 5f;

    private bool effectActive = false;

    private struct AffectedPlayer
    {
        public GameObject gameObject;
        public Vector3 originalScale;
        public HorseController horseController;
    }

    private readonly List<AffectedPlayer> affectedPlayers = new List<AffectedPlayer>();

    protected override bool CanActivate() => !effectActive;

    protected override void OnActivate()
    {
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        effectActive = true;
        affectedPlayers.Clear();

        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");

        foreach (GameObject player in players)
        {
            AffectedPlayer ap = new AffectedPlayer
            {
                gameObject = player,
                originalScale = player.transform.localScale,
                horseController = player.GetComponentInChildren<HorseController>()
            };

            if (ap.horseController == null)
                ap.horseController = player.GetComponentInParent<HorseController>();

            player.transform.localScale = ap.originalScale * shrinkScale;

            if (ap.horseController != null)
                ap.horseController.SetSpeedMultiplier(speedReduction);

            affectedPlayers.Add(ap);
        }

        Debug.Log($"[LightningPower] {affectedPlayers.Count} players affected for {effectDuration}s");

        yield return new WaitForSeconds(effectDuration);

        foreach (AffectedPlayer ap in affectedPlayers)
        {
            if (ap.gameObject != null)
                ap.gameObject.transform.localScale = ap.originalScale;

            if (ap.horseController != null)
                ap.horseController.SetSpeedMultiplier(1f);
        }

        affectedPlayers.Clear();
        effectActive = false;
    }
}
