using Tartisians.Core.Services;
using Tartisians.Systems.Navigation;
using UnityEngine;

namespace Tartisians.Gameplay.Navigation
{
    /// <summary>
    /// 아레나 그리드 흐름장을 생성·갱신한다. 장애물(NavObstacle)을 차단 셀로 표시하고,
    /// 플레이어가 다른 셀로 이동할 때만 비용장을 재계산(저비용)한다. FlowField를 ServiceLocator에 등록.
    /// </summary>
    public sealed class FlowFieldController : MonoBehaviour
    {
        [SerializeField] Transform _target;
        [SerializeField] Vector2 _areaSize = new(40f, 40f); // 월드 XZ 크기
        [SerializeField] float _cellSize = 1f;

        [Header("Debug Gizmos")]
        [SerializeField] bool _drawGrid = true;
        [SerializeField] bool _drawObstacles = true;
        [SerializeField] bool _drawFlow = true;
        [SerializeField] bool _drawCost = false;

        FlowField _field;
        int _lastGoalX = int.MinValue;
        int _lastGoalY = int.MinValue;

        void ComputeGrid(out Vector3 origin, out int width, out int height)
        {
            Vector3 c = transform.position;
            origin = new Vector3(c.x - _areaSize.x * 0.5f, 0f, c.z - _areaSize.y * 0.5f);
            width = Mathf.CeilToInt(_areaSize.x / _cellSize);
            height = Mathf.CeilToInt(_areaSize.y / _cellSize);
        }

        void Awake()
        {
            ComputeGrid(out Vector3 origin, out int w, out int h);

            _field = new FlowField(origin, _cellSize, w, h);
            ServiceLocator.Register(_field);

            MarkObstacles();

            if (_target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    _target = p.transform;
                }
            }
        }

        void OnDestroy()
        {
            if (ServiceLocator.TryGet(out FlowField current) && ReferenceEquals(current, _field))
            {
                ServiceLocator.Unregister<FlowField>();
            }
        }

        void MarkObstacles()
        {
            _field.ClearBlocked();
            NavObstacle[] obstacles = FindObjectsByType<NavObstacle>(FindObjectsSortMode.None);
            for (int i = 0; i < obstacles.Length; i++)
            {
                Bounds b = obstacles[i].WorldBounds;
                _field.WorldToCell(new Vector3(b.min.x, 0f, b.min.z), out int x0, out int y0);
                _field.WorldToCell(new Vector3(b.max.x, 0f, b.max.z), out int x1, out int y1);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        _field.SetBlocked(x, y, true);
                    }
                }
            }
        }

        void Update()
        {
            if (_target == null || _field == null)
            {
                return;
            }

            _field.WorldToCell(_target.position, out int gx, out int gy);
            if (gx != _lastGoalX || gy != _lastGoalY)
            {
                _lastGoalX = gx;
                _lastGoalY = gy;
                _field.Compute(_target.position);
            }
        }

        // ─────────────────────────── 디버그 시각화 ───────────────────────────
        // 씬 뷰(및 Game 뷰의 Gizmos 토글)에서 그리드·장애물·흐름을 그린다.
        // 편집 모드에선 그리드+장애물(예상)만, 플레이 모드에선 실제 비용/흐름까지 표시.
        void OnDrawGizmos()
        {
            if (!_drawGrid && !_drawObstacles && !_drawFlow && !_drawCost)
            {
                return;
            }

            ComputeGrid(out Vector3 origin, out int w, out int h);
            float cs = _cellSize;
            const float y = 0.05f;
            bool playing = Application.isPlaying && _field != null;

            // 그리드 외곽 + 셀 라인
            if (_drawGrid)
            {
                Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
                for (int x = 0; x <= w; x++)
                {
                    Gizmos.DrawLine(new Vector3(origin.x + x * cs, y, origin.z), new Vector3(origin.x + x * cs, y, origin.z + h * cs));
                }

                for (int z = 0; z <= h; z++)
                {
                    Gizmos.DrawLine(new Vector3(origin.x, y, origin.z + z * cs), new Vector3(origin.x + w * cs, y, origin.z + z * cs));
                }

                // 외곽 박스 강조
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireCube(new Vector3(origin.x + w * cs * 0.5f, y, origin.z + h * cs * 0.5f), new Vector3(w * cs, 0.01f, h * cs));
            }

            // 편집 모드용 장애물 목록(플레이 모드는 _field.IsBlocked 사용)
            NavObstacle[] obstacles = !playing ? FindObjectsByType<NavObstacle>(FindObjectsSortMode.None) : null;

            for (int gy = 0; gy < h; gy++)
            {
                for (int gx = 0; gx < w; gx++)
                {
                    Vector3 center = new Vector3(origin.x + (gx + 0.5f) * cs, y, origin.z + (gy + 0.5f) * cs);
                    bool blocked = playing ? _field.IsBlocked(gx, gy) : EditModeBlocked(center, obstacles);

                    if (blocked)
                    {
                        if (_drawObstacles)
                        {
                            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.45f);
                            Gizmos.DrawCube(center, new Vector3(cs * 0.9f, 0.02f, cs * 0.9f));
                        }

                        continue;
                    }

                    if (playing && _drawCost)
                    {
                        ushort cost = _field.GetCost(gx, gy);
                        if (cost != ushort.MaxValue)
                        {
                            float t = Mathf.Clamp01(cost / (float)(w + h));
                            Gizmos.color = new Color(t, 1f - t, 0.2f, 0.18f); // 가까움=녹색, 멀음=적색
                            Gizmos.DrawCube(center, new Vector3(cs * 0.9f, 0.01f, cs * 0.9f));
                        }
                    }

                    if (playing && _drawFlow)
                    {
                        Vector3 dir = _field.SampleDirection(center);
                        if (dir != Vector3.zero)
                        {
                            Gizmos.color = Color.cyan;
                            Vector3 tip = center + dir * (cs * 0.42f);
                            Gizmos.DrawLine(center, tip);
                            Gizmos.DrawSphere(tip, cs * 0.07f);
                        }
                    }
                }
            }
        }

        static bool EditModeBlocked(Vector3 center, NavObstacle[] obstacles)
        {
            if (obstacles == null)
            {
                return false;
            }

            for (int i = 0; i < obstacles.Length; i++)
            {
                Bounds b = obstacles[i].WorldBounds;
                if (center.x >= b.min.x && center.x <= b.max.x && center.z >= b.min.z && center.z <= b.max.z)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
