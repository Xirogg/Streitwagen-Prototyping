using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// League-of-Legends style cooldown indicator: icon, radial fill overlay, and remaining-seconds text.
/// Drives its visuals from a bound GodPower.
/// </summary>
public class GUI_CooldownIndicator : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image iconImage;
    [Tooltip("Image with Image Type = Filled, Fill Method = Radial 360. Covers the icon; drains as the cooldown ticks down.")]
    [SerializeField] private Image cooldownOverlay;
    [SerializeField] private Image progressBar;
    [SerializeField] private TMP_Text cooldownText;
    [SerializeField] private TMP_Text hotkeyText;
    [SerializeField] private TMP_Text nameText;

    [Header("Display")]
    [Tooltip("Hide the seconds text and overlay when the power is ready.")]
    [SerializeField] private bool hideWhenReady = true;
    [Tooltip("Color of the icon while on cooldown.")]
    [SerializeField] private Color onCooldownTint = new Color(0.4f, 0.4f, 0.4f, 1f);
    [SerializeField] private Color readyTint = Color.white;
    [Tooltip("Format for remaining time. {0} = seconds.")]
    [SerializeField] private string textFormat = "{0:0.0}";

    private GodPower power;

    public void Bind(GodPower power)
    {
        this.power = power;
        if (power == null) return;

        if (iconImage != null) iconImage.sprite = power.Icon;
        if (nameText != null) nameText.text = power.DisplayName;
        if (hotkeyText != null) hotkeyText.text = power.ActivationKey.ToString();

        Refresh();
    }

    private void Update()
    {
        if (power == null) return;
        Refresh();
    }

    private void Refresh()
    {
        bool ready = power.IsReady;
        float remaining = power.CooldownRemaining;
        float progress = power.CooldownProgress; // 0 = just used, 1 = ready

        if (cooldownOverlay != null)
        {
            cooldownOverlay.fillAmount = ready ? 0f : (1f - progress);
            if (hideWhenReady) cooldownOverlay.enabled = !ready;
        }

        if (progressBar != null)
        {
            progressBar.fillAmount = progress;
        }

        if (cooldownText != null)
        {
            if (ready && hideWhenReady)
            {
                cooldownText.text = string.Empty;
            }
            else
            {
                cooldownText.text = string.Format(textFormat, remaining);
            }
        }

        if (iconImage != null)
        {
            iconImage.color = ready ? readyTint : onCooldownTint;
        }
    }
}
