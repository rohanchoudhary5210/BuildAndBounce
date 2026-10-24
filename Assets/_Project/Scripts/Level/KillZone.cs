using System;
using BuildAndBounce.Player;
using UnityEngine;

namespace BuildAndBounce.Level
{
    /// <summary>
    /// Hazard volume placed beneath levels or inside pit obstacles.
    /// Immediately triggers player respawn upon contact.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class KillZone : MonoBehaviour
    {
        public static event Action<PlayerController> OnPlayerEnteredKillZone;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.TryGetComponent<PlayerController>(out var player))
            {
                OnPlayerEnteredKillZone?.Invoke(player);
            }
        }
    }
}
