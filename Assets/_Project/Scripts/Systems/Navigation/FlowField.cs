using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Systems.Navigation
{
    /// <summary>
    /// XZ 평면 그리드 위의 Flow Field(흐름장). 목표(플레이어)까지의 통합 비용장을
    /// BFS로 계산하고, 각 셀에 목표로 향하는 방향(8방향 경사 하강)을 저장한다.
    /// 대량 적이 NavMesh/A* 없이 셀 방향만 샘플(O(1))해 장애물을 우회한다.
    /// (벽 충돌은 해석적 ObstacleField가 담당 — 격자 거리장은 라우팅용.)
    ///
    /// 큰 맵에선 <see cref="ComputeWindow"/>로 목표 주변 창(window)만 계산한다.
    /// 적은 항상 플레이어 근처(스폰 반경 이내)에 있으므로 창 밖 흐름은 불필요 →
    /// 재계산 비용이 맵 크기와 무관하게 창 면적으로 일정해진다.
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
        readonly Queue<int> _queue;

        // 현재 유효한 창 범위(셀, 포함). 창 밖 셀은 흐름이 stale → 샘플 시 zero 반환.
        int _winMinX;
        int _winMinY;
        int _winMaxX;
        int _winMaxY;
        bool _windowed;

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
            _queue = new Queue<int>(n);

            _winMaxX = Width - 1;
            _winMaxY = Height - 1;
        }

        bool InWindow(int x, int y) => x >= _winMinX && x <= _winMaxX && y >= _winMinY && y <= _winMaxY;

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

        /// <summary>목표 위치 기준으로 전체 격자의 통합 비용장과 흐름 방향을 다시 계산한다.</summary>
        public void Compute(Vector3 goalWorld) => ComputeWindow(goalWorld, int.MaxValue);

        /// <summary>
        /// 목표 셀 주변 반경 radius(셀)의 창만 계산한다. 창 밖은 건드리지 않아(클리어/BFS/흐름 모두 창 한정)
        /// 비용이 맵 크기와 무관하게 창 면적 O((2r+1)²)으로 일정하다. radius가 격자보다 크면 전체 계산과 같다.
        /// </summary>
        public void ComputeWindow(Vector3 goalWorld, int radius)
        {
            WorldToCell(goalWorld, out int gx, out int gy);
            gx = Mathf.Clamp(gx, 0, Width - 1);
            gy = Mathf.Clamp(gy, 0, Height - 1);

            radius = Mathf.Clamp(radius, 0, Width + Height); // 오버플로 방지 + 전체 격자 상한
            _winMinX = Mathf.Max(0, gx - radius);
            _winMinY = Mathf.Max(0, gy - radius);
            _winMaxX = Mathf.Min(Width - 1, gx + radius);
            _winMaxY = Mathf.Min(Height - 1, gy + radius);
            _windowed = true;

            // 비용 초기화(창 한정)
            for (int y = _winMinY; y <= _winMaxY; y++)
            {
                for (int x = _winMinX; x <= _winMaxX; x++)
                {
                    _cost[Index(x, y)] = Unreachable;
                }
            }

            _queue.Clear();
            int goal = Index(gx, gy);
            _cost[goal] = 0;
            _queue.Enqueue(goal);

            // BFS 통합 비용장(4방향, 장애물 통과 금지, 창 한정)
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
                    if (!InWindow(nx, ny))
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

            BuildFlow(_winMinX, _winMinY, _winMaxX, _winMaxY);
        }

        // 창 [minX..maxX]×[minY..maxY] 한정 흐름 빌드. 이웃도 창 밖이면 무시(경계서 stale 참조 방지).
        void BuildFlow(int minX, int minY, int maxX, int maxY)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
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
                            if (nx < minX || nx > maxX || ny < minY || ny > maxY)
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

        /// <summary>월드 위치에서의 흐름 방향(XZ 단위벡터). 범위/창 밖·도달 불가면 zero(→ 직선 추적 폴백).</summary>
        public Vector3 SampleDirection(Vector3 world)
        {
            WorldToCell(world, out int x, out int y);
            if (!InBounds(x, y) || (_windowed && !InWindow(x, y)))
            {
                return Vector3.zero;
            }

            Vector2 f = _flow[Index(x, y)];
            return new Vector3(f.x, 0f, f.y);
        }
    }
}
