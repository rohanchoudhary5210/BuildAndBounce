using System;
using UnityEngine;

namespace BuildAndBounce.Race
{
    /// <summary>
    /// A single sampled snapshot of the player's transform at a deterministic timestamp.
    /// Used by GhostRecorder and GhostPlayback for async multiplayer racing (M4).
    /// </summary>
    [Serializable]
    public struct GhostFrame
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;

        public GhostFrame(float time, Vector3 position, Quaternion rotation)
        {
            this.time = time;
            this.position = position;
            this.rotation = rotation;
        }
    }
}
