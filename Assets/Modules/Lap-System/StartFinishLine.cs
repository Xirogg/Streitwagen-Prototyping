using UnityEngine;

namespace LapSystem
{
    [RequireComponent(typeof(BoxCollider))]
    public class StartFinishLine : MonoBehaviour
    {
        [Tooltip("Cooldown in seconds to prevent double-triggering when a player has multiple collider GameObjects.")]
        [SerializeField] private float retriggerCooldown = 1.5f;

        private float lastCrossTime = -999f;

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
            if (!other.CompareTag("Player")) return;
            if (LapManager.Instance == null) return;
            if (Time.time - lastCrossTime < retriggerCooldown) return;

            lastCrossTime = Time.time;
            LapManager.Instance.OnStartLineCrossed();
        }
    }
}
