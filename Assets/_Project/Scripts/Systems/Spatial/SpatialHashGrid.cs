using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Systems.Spatial
{
    /// <summary>
    /// XZ 평면 셀 기반 공간 해시. 대량 적의 이웃 질의(분리·히트)를 물리 엔진 없이
    /// 저비용으로 처리한다. 셀 리스트는 재사용해 매 프레임 재구성 시 할당을 피한다.
    /// </summary>
    public sealed class SpatialHashGrid
    {
        readonly float _cellSize;
        readonly Dictionary<long, List<int>> _cells = new();
        IReadOnlyList<Vector3> _positions;

        public SpatialHashGrid(float cellSize) => _cellSize = Mathf.Max(0.01f, cellSize);

        static long Key(int cx, int cz) => ((long)cx << 32) | (uint)cz;

        int CellCoord(float value) => Mathf.FloorToInt(value / _cellSize);

        public void Rebuild(IReadOnlyList<Vector3> positions)
        {
            _positions = positions;

            foreach (KeyValuePair<long, List<int>> cell in _cells)
            {
                cell.Value.Clear();
            }

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 p = positions[i];
                long key = Key(CellCoord(p.x), CellCoord(p.z));
                if (!_cells.TryGetValue(key, out List<int> bucket))
                {
                    bucket = new List<int>(8);
                    _cells[key] = bucket;
                }

                bucket.Add(i);
            }
        }

        /// <summary>반경 내(원형) 위치 인덱스를 results에 채운다. self 자신도 포함될 수 있다.</summary>
        public void Query(Vector3 position, float radius, List<int> results)
        {
            results.Clear();
            if (_positions == null)
            {
                return;
            }

            int minX = CellCoord(position.x - radius);
            int maxX = CellCoord(position.x + radius);
            int minZ = CellCoord(position.z - radius);
            int maxZ = CellCoord(position.z + radius);
            float r2 = radius * radius;

            for (int cx = minX; cx <= maxX; cx++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    if (!_cells.TryGetValue(Key(cx, cz), out List<int> bucket))
                    {
                        continue;
                    }

                    for (int j = 0; j < bucket.Count; j++)
                    {
                        int idx = bucket[j];
                        if ((_positions[idx] - position).sqrMagnitude <= r2)
                        {
                            results.Add(idx);
                        }
                    }
                }
            }
        }
    }
}
