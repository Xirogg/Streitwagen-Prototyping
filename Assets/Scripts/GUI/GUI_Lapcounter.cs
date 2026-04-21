using System;
using LapSystem;
using TMPro;
using UnityEngine;

public class GUI_Lapcounter : MonoBehaviour
{
    [Serializable]
    public class DriverLapDisplay
    {
        [Tooltip("The HorsePair Transform of the driver this TMP belongs to.")]
        public Transform horsePair;

        [Tooltip("TMP text that will display 'Lap X / Y' for this driver.")]
        public TMP_Text lapText;
    }

    [Header("Lap Source")]
    [SerializeField] private LapManager lapManager;

    [Header("Driver Displays")]
    [SerializeField] private DriverLapDisplay[] driverDisplays;

    private void Awake()
    {
        if (lapManager == null)
            lapManager = LapManager.Instance;
    }

    private void Update()
    {
        if (lapManager == null)
        {
            lapManager = LapManager.Instance;
            if (lapManager == null) return;
        }

        int maxLaps = lapManager.MaxLaps;
        int currentLap = Mathf.Clamp(Mathf.Max(lapManager.CurrentLap, 1), 1, maxLaps);

        for (int i = 0; i < driverDisplays.Length; i++)
        {
            var entry = driverDisplays[i];
            if (entry == null || entry.lapText == null) continue;

            entry.lapText.text = $"Lap {currentLap} / {maxLaps}";
        }
    }
}
