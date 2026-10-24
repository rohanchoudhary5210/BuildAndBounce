using System;
using UnityEngine;

namespace BuildAndBounce.Net
{
    /// <summary>
    /// Network transform snapshot transmitted across the wire for remote player interpolation (M6).
    /// Contains timestamp, position, velocity, and rotation for client-side interpolation and dead reckoning.
    /// </summary>
    [Serializable]
    public struct NetworkPlayerSnapshot
    {
        public int playerId;
        public float timestamp;
        public Vector3 position;
        public Vector3 velocity;
        public Quaternion rotation;

        public NetworkPlayerSnapshot(int playerId, float timestamp, Vector3 position, Vector3 velocity, Quaternion rotation)
        {
            this.playerId = playerId;
            this.timestamp = timestamp;
            this.position = position;
            this.velocity = velocity;
            this.rotation = rotation;
        }
    }
}
