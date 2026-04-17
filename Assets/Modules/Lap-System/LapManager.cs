using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace LapSystem
{
    public class LapManager : MonoBehaviour
    {
        public static LapManager Instance { get; private set; }

        [Header("Settings")]
        [SerializeField] private int maxLaps = 3;

        [Header("Events")]
        public UnityEvent onRaceStart;
        public UnityEvent<int> onLapCompleted;
        public UnityEvent onRaceFinished;
        public UnityEvent onInvalidLap;

        private readonly HashSet<SectorCheckpoint> allSectors = new();
        private readonly HashSet<SectorCheckpoint> passedSectors = new();

        private int currentLap;
        private bool raceActive;
        private bool raceFinished;

        public int CurrentLap => currentLap;
        public int MaxLaps => maxLaps;
        public bool RaceActive => raceActive;
        public bool RaceFinished => raceFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            StartRace();
        }

        public void StartRace()
        {
            allSectors.Clear();
            foreach (var sector in FindObjectsByType<SectorCheckpoint>(FindObjectsSortMode.None))
            {
                allSectors.Add(sector);
                sector.ResetCheckpoint();
            }

            passedSectors.Clear();
            currentLap = 0;
            raceActive = true;
            raceFinished = false;

            onRaceStart?.Invoke();
        }

        public void RegisterSectorPass(SectorCheckpoint sector)
        {
            if (!raceActive) return;
            passedSectors.Add(sector);
        }

        public void OnStartLineCrossed()
        {
            if (!raceActive || raceFinished) return;

            if (currentLap == 0)
            {
                currentLap = 1;
                ResetSectors();
                return;
            }

            if (passedSectors.Count >= allSectors.Count)
            {
                currentLap++;
                onLapCompleted?.Invoke(currentLap - 1);

                if (currentLap > maxLaps)
                {
                    raceActive = false;
                    raceFinished = true;
                    onRaceFinished?.Invoke();
                    return;
                }

                ResetSectors();
            }
            else
            {
                onInvalidLap?.Invoke();
            }
        }

        private void ResetSectors()
        {
            passedSectors.Clear();
            foreach (var sector in allSectors)
            {
                sector.ResetCheckpoint();
            }
        }
    }
}
