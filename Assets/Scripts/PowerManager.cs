using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Central hub for all god powers. Auto-discovers GodPower components in children
/// and spawns a cooldown UI indicator for each one under a given UI container.
/// </summary>
public class PowerManager : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Parent transform under the Canvas where cooldown indicators are spawned. Each power provides its own prefab.")]
    [SerializeField] private RectTransform cooldownContainer;

    [Header("Discovery")]
    [Tooltip("If true, discovers GodPower components in children at Awake. Otherwise uses the list below.")]
    [SerializeField] private bool autoDiscover = true;
    [SerializeField] private List<GodPower> powers = new List<GodPower>();

    public IReadOnlyList<GodPower> Powers => powers;

    private void Awake()
    {
        if (autoDiscover)
        {
            powers.Clear();
            powers.AddRange(GetComponentsInChildren<GodPower>(true));
        }
    }

    private void Start()
    {
        SpawnIndicators();
    }

    private void SpawnIndicators()
    {
        if (cooldownContainer == null)
        {
            Debug.LogWarning("[PowerManager] No cooldown container assigned; skipping UI spawn.");
            return;
        }

        foreach (GodPower power in powers)
        {
            if (power == null) continue;
            GUI_CooldownIndicator prefab = power.IndicatorPrefab;
            if (prefab == null)
            {
                Debug.LogWarning($"[PowerManager] Power '{power.DisplayName}' has no indicator prefab assigned; skipping.");
                continue;
            }
            GUI_CooldownIndicator indicator = Instantiate(prefab, cooldownContainer);
            indicator.Bind(power);
            Debug.Log($"[PowerManager] Spawned indicator for '{power.DisplayName}' under {cooldownContainer.name}");
        }
    }
}
