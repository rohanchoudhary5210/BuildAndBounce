using System.Collections.Generic;
using UnityEngine;

namespace BuildAndBounce.Builder
{
    /// <summary>
    /// ScriptableObject repository of all available course blocks.
    /// Drives the level builder's selection palette.
    /// </summary>
    [CreateAssetMenu(fileName = "BlockCatalog", menuName = "Build & Bounce/Builder/Block Catalog")]
    public class BlockCatalog : ScriptableObject
    {
        [SerializeField] private List<BlockData> _blocks = new List<BlockData>();

        private readonly Dictionary<string, BlockData> _lookup = new Dictionary<string, BlockData>();

        public IReadOnlyList<BlockData> Blocks => _blocks;
        public int Count => _blocks.Count;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public void RebuildLookup()
        {
            _lookup.Clear();
            for (int i = 0; i < _blocks.Count; i++)
            {
                var block = _blocks[i];
                if (block != null && !string.IsNullOrEmpty(block.Id))
                {
                    _lookup[block.Id] = block;
                }
            }
        }

        public BlockData GetBlockById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            if (_lookup.Count == 0 && _blocks.Count > 0)
            {
                RebuildLookup();
            }

            _lookup.TryGetValue(id, out var data);
            return data;
        }

        public BlockData GetBlock(int index)
        {
            if (index >= 0 && index < _blocks.Count)
            {
                return _blocks[index];
            }
            return null;
        }

        public void AddBlock(BlockData block)
        {
            if (block == null || string.IsNullOrEmpty(block.Id)) return;
            _blocks.Add(block);
            _lookup[block.Id] = block;
        }
    }
}