using UnityEngine;

namespace LapSystem
{
    [RequireComponent(typeof(BoxCollider))]
    public class SectorCheckpoint : MonoBehaviour
    {
        [SerializeField] private int sectorIndex;

        private bool passed;

        public bool Passed => passed;
        public int SectorIndex => sectorIndex;

        private void Reset()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            //Excludes everything but Players not passed thc Checkpoint this Lap 
            if (passed) return;
            if (!other.CompareTag("Player")) return;
            if (LapManager.Instance == null) return;

            passed = true;
            LapManager.Instance.RegisterSectorPass(this);
        }

        public void ResetCheckpoint()
        {
            passed = false;
        }
    }
}
