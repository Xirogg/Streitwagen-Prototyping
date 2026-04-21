using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Base class for all god powers. Handles cooldown bookkeeping and input binding.
/// Subclasses implement OnActivate() for the actual effect.
/// Attach a subclass to a child GameObject of the PowerManager; it will be auto-discovered.
/// </summary>
public abstract class GodPower : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private string displayName = "Power";
    [SerializeField] private Sprite icon;

    [Header("Cooldown")]
    [SerializeField] private float cooldownDuration = 10f;
    [SerializeField] private bool startOnCooldown = false;

    [Header("Input")]
    [SerializeField] private Key activationKey = Key.B;

    [Header("UI")]
    [Tooltip("The cooldown indicator prefab spawned for this power. The PowerManager instantiates it and calls Bind(this).")]
    [SerializeField] private GUI_CooldownIndicator indicatorPrefab;

    private float cooldownRemaining;

    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public float CooldownDuration => cooldownDuration;
    public float CooldownRemaining => cooldownRemaining;
    public float CooldownProgress => cooldownDuration <= 0f ? 1f : 1f - Mathf.Clamp01(cooldownRemaining / cooldownDuration);
    public bool IsReady => cooldownRemaining <= 0f;
    public Key ActivationKey => activationKey;
    public GUI_CooldownIndicator IndicatorPrefab => indicatorPrefab;

    protected virtual void OnEnable()
    {
        if (startOnCooldown) cooldownRemaining = cooldownDuration;
    }

    protected virtual void Update()
    {
        if (cooldownRemaining > 0f)
        {
            cooldownRemaining = Mathf.Max(0f, cooldownRemaining - Time.deltaTime);
        }

        Keyboard kb = Keyboard.current;
        if (kb != null && kb[activationKey].wasPressedThisFrame)
        {
            TryActivate();
        }
    }

    public bool TryActivate()
    {
        if (!IsReady) return false;
        if (!CanActivate()) return false;

        OnActivate();
        cooldownRemaining = cooldownDuration;
        return true;
    }

    /// <summary>Override for extra gating (e.g. power already running). Default allows activation when off cooldown.</summary>
    protected virtual bool CanActivate() => true;

    /// <summary>Implement the actual effect of the power.</summary>
    protected abstract void OnActivate();
}
