using System;
using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// Serializable representation of an individual placed block instance.
    /// Stores the block catalog identifier, grid-aligned world coordinates, and rotation.
    /// </summary>
    [Serializable]
    public class PlacedBlockData
    {
        public string blockId;
        public Vector3 position;
        public Vector3 rotation;

        public PlacedBlockData() { }

        public PlacedBlockData(string blockId, Vector3 position, Vector3 rotation)
        {
            this.blockId = blockId;
            this.position = position;
            this.rotation = rotation;
        }
    }
}