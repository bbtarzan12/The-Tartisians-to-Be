using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Systems.Navigation
{
    /// <summary>
    /// XZ 평면 그리드 위의 Flow Field(흐름장). 목표(플레이어)까지의 통합 비용장을
    /// BFS로 계산하고, 각 셀에 목표로 향하는 방향(8방향 경사 하강)을 저장한다.
    /// 대량 적이 NavMesh/A* 없이 셀 방향만 샘플(O(1))해 장애물을 우회한다.
    /// 순수 로직이라 EditMode에서 단위 테스트한다.
    /// </summary>
    public sealed class FlowField
    {
        const ushort Unreachable = ushort.MaxValue;

        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }
        public Vector3 Origin { get; } // 그리드 최소 코너(월드 XZ). y는 무시.

        readonly bool[] _blocked;
        readonly ushort[] _cost;
        readonly Vector2[] _flow;
        readonly int[] _obstacleDist; // 가장 가까운 장애물까지의 거리(셀 단위)
        readonly Queue<int> _queue;

        static readonly int[] Dx4 = { 1, -1, 0, 0 };
        static readonly int[] Dy4 = { 0, 0, 1, -1 };

        public FlowField(Vector3 origin, float cellSize, int width, int height)
        {
            Origin = origin;
            CellSize = Mathf.Max(0.01f, cellSize);
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);

            int n = Width * Height;
            _blocked = new bool[n];
            _cost = new ushort[n];
            _flow = new Vector2[n];
            _obstacleDist = new int[n];
            _queue = new Queue<int>(n);
        }

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

        int Index(int x, int y) => y * Width + x;

        public void ClearBlocked() => System.Array.Clear(_blocked, 0, _blocked.Length);

        public void SetBlocked(int x, int y, bool blocked)
        {
            if (InBounds(x, y))
            {
                _blocked[Index(x, y)] = blocked;
            }
        }

        public bool IsBlocked(int x, int y) => InBounds(x, y) && _blocked[Index(x, y)];

        public ushort GetCost(int x, int y) => InBounds(x, y) ? _cost[Index(x, y)] : Unreachable;

        public void WorldToCell(Vector3 world, out int x, out int y)
        {
            x = Mathf.FloorToInt((world.x - Origin.x) / CellSize);
            y = Mathf.FloorToInt((world.z - Origin.z) / CellSize);
        }

        /// <summary>목표 위치 기준으로 통합 비용장과 흐름 방향을 다시 계산한다.</summary>
        public void Compute(Vector3 goalWorld)
        {
            for (int i = 0; i < _cost.Length; i++)
            {
                _cost[i] = Unreachable;
            }

            WorldToCell(goalWorld, out int gx, out int gy);
            gx = Mathf.Clamp(gx, 0, Width - 1);
            gy = Mathf.Clamp(gy, 0, Height - 1);

            _queue.Clear();
            int goal = Index(gx, gy);
            _cost[goal] = 0;
            _queue.Enqueue(goal);

            // BFS 통합 비용장(4방향, 장애물 통과 금지)
            while (_queue.Count > 0)
            {
                int current = _queue.Dequeue();
                int cx = current % Width;
                int cy = current / Width;
                ushort next = (ushort)(_cost[current] + 1);

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx4[d];
                    int ny = cy + Dy4[d];
                    if (!InBounds(nx, ny))
                    {
                        continue;
                    }

                    int ni = Index(nx, ny);
                    if (_blocked[ni] || _cost[ni] <= next)
                    {
                        continue;
                    }

                    _cost[ni] = next;
                    _queue.Enqueue(ni);
                }
            }

            BuildFlow();
            ComputeDistanceField();
        }

        /// <summary>각 셀에서 가장 가까운 장애물까지의 거리(셀)를 멀티소스 BFS로 계산한다.</summary>
        void ComputeDistanceField()
        {
            int big = Width + Height;
            _queue.Clear();
            for (int i = 0; i < _obstacleDist.Length; i++)
            {
                if (_blocked[i])
                {
                    _obstacleDist[i] = 0;
                    _queue.Enqueue(i);
                }
                else
                {
                    _obstacleDist[i] = big;
                }
            }

            while (_queue.Count > 0)
            {
                int current = _queue.Dequeue();
                int cx = current % Width;
                int cy = current / Width;
                int nd = _obstacleDist[current] + 1;

                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx4[d];
                    int ny = cy + Dy4[d];
                    if (!InBounds(nx, ny))
                    {
                        continue;
                    }

                    int ni = Index(nx, ny);
                    if (_obstacleDist[ni] > nd)
                    {
                        _obstacleDist[ni] = nd;
                        _queue.Enqueue(ni);
                    }
                }
            }
        }

        int CellDistClamped(int x, int y)
        {
            x = Mathf.Clamp(x, 0, Width - 1);
            y = Mathf.Clamp(y, 0, Height - 1);
            return _obstacleDist[Index(x, y)];
        }

        /// <summary>월드 위치에서 가장 가까운 장애물까지의 거리(월드 단위 근사).</summary>
        public float DistanceToObstacle(Vector3 world)
        {
            WorldToCell(world, out int x, out int y);
            if (!InBounds(x, y))
            {
                return (Width + Height) * CellSize;
            }

            return _obstacleDist[Index(x, y)] * CellSize;
        }

        /// <summary>장애물에서 멀어지는 방향(거리장 그래디언트, XZ 단위벡터). 평지면 zero.</summary>
        public Vector3 ObstacleGradient(Vector3 world)
        {
            WorldToCell(world, out int x, out int y);
            if (!InBounds(x, y))
            {
                return Vector3.zero;
            }

            // 중앙 차분: 거리가 증가하는 방향 = 벽 반대 방향
            float gx = CellDistClamped(x + 1, y) - CellDistClamped(x - 1, y);
            float gz = CellDistClamped(x, y + 1) - CellDistClamped(x, y - 1);
            Vector3 g = new Vector3(gx, 0f, gz);
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector3.zero;
        }

        void BuildFlow()
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    int i = Index(x, y);
                    if (_blocked[i] || _cost[i] == Unreachable)
                    {
                        _flow[i] = Vector2.zero;
                        continue;
                    }

                    int bestX = x;
                    int bestY = y;
                    ushort best = _cost[i];

                    // 8방향 중 비용이 가장 낮은 이웃으로 향한다.
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            if (dx == 0 && dy == 0)
                            {
                                continue;
                            }

                            int nx = x + dx;
                            int ny = y + dy;
                            if (!InBounds(nx, ny))
                            {
                                continue;
                            }

                            int ni = Index(nx, ny);
                            if (_blocked[ni])
                            {
                                continue;
                            }

                            // 대각 이동 시 모서리 끼임 방지: 인접 직교 셀이 둘 다 막혔으면 스킵
                            if (dx != 0 && dy != 0 && _blocked[Index(x + dx, y)] && _blocked[Index(x, y + dy)])
                            {
                                continue;
                            }

                            if (_cost[ni] < best)
                            {
                                best = _cost[ni];
                                bestX = nx;
                                bestY = ny;
                            }
                        }
                    }

                    Vector2 dir = new Vector2(bestX - x, bestY - y);
                    _flow[i] = dir.sqrMagnitude > 0f ? dir.normalized : Vector2.zero;
                }
            }
        }

        /// <summary>월드 위치에서의 흐름 방향(XZ 단위벡터). 범위 밖/도달 불가면 zero.</summary>
        public Vector3 SampleDirection(Vector3 world)
        {
            WorldToCell(world, out int x, out int y);
            if (!InBounds(x, y))
            {
                return Vector3.zero;
            }

            Vector2 f = _flow[Index(x, y)];
            return new Vector3(f.x, 0f, f.y);
        }
    }
}
