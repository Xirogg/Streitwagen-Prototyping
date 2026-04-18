using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Blocks horse movement for a configurable cooldown when play starts,
/// then releases the players. Optionally displays the countdown via UI.
/// </summary>
public class StartSystem : MonoBehaviour
{
    [Header("Countdown")]
    [SerializeField] private float countdownSeconds = 5f;

    [Header("UI (optional)")]
    [SerializeField] private TMP_Text countdownTMP;
    [SerializeField] private Text countdownUGUI;
    [SerializeField] private string goText = "GO!";
    [SerializeField] private float goDisplayTime = 1f;

    private void Awake()
    {
        HorseController.MovementEnabled = false;
    }

    private void Start()
    {
        StartCoroutine(RunCountdown());
    }

    private IEnumerator RunCountdown()
    {
        float remaining = countdownSeconds;
        while (remaining > 0f)
        {
            SetCountdownText(Mathf.CeilToInt(remaining).ToString());
            yield return null;
            remaining -= Time.deltaTime;
        }

        HorseController.MovementEnabled = true;
        SetCountdownText(goText);

        if (goDisplayTime > 0f)
        {
            yield return new WaitForSeconds(goDisplayTime);
        }

        SetCountdownText(string.Empty);
    }

    private void SetCountdownText(string value)
    {
        if (countdownTMP != null) countdownTMP.text = value;
        if (countdownUGUI != null) countdownUGUI.text = value;
    }
}
