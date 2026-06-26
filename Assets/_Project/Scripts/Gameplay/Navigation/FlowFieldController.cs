using Tartisians.Core.Services;
using Tartisians.Systems.Crowd;
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
        [Tooltip("목표 주변 이 반경(셀)만 흐름 재계산. 0=전체. 격자보다 크면 전체와 동일(작은 맵 자동 무시).")]
        [SerializeField] int _windowRadius = 40; // 스폰 반경(≈28셀) + 여유. 큰 맵에서 재계산 비용 일정화.

        [Header("Debug Gizmos")]
        [SerializeField] bool _drawGrid = true;
        [SerializeField] bool _drawObstacles = true;
        [SerializeField] bool _drawFlow = true;
        [SerializeField] bool _drawCost = false;
        [Tooltip("기즈모를 플레이어 주변 이 반경(셀)만 그린다. 0=전체. 큰 맵에서 매 프레임 전체 순회 방지.")]
        [SerializeField] int _gizmoRadius = 24;

        FlowField _field;
        ObstacleField _obstacles;
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

            _obstacles = new ObstacleField();
            ServiceLocator.Register(_obstacles);

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

            if (ServiceLocator.TryGet(out ObstacleField currentObs) && ReferenceEquals(currentObs, _obstacles))
            {
                ServiceLocator.Unregister<ObstacleField>();
            }
        }

        void MarkObstacles()
        {
            _field.ClearBlocked();
            _obstacles.Clear();
            NavObstacle[] obstacles = FindObjectsByType<NavObstacle>(FindObjectsInactive.Exclude);
            for (int i = 0; i < obstacles.Length; i++)
            {
                Bounds b = obstacles[i].WorldBounds;

                // 흐름장(라우팅): 박스와 '실제로 겹치는' 셀만 차단.
                // min은 floor(셀 시작), max는 ceil-1(경계에 딱 닿기만 하는 셀은 제외)
                // → 차단 셀이 벽/장애물 footprint와 정확히 일치(±쪽 한 칸 과다 차단 제거).
                _field.WorldToCell(new Vector3(b.min.x, 0f, b.min.z), out int x0, out int y0);
                int x1 = Mathf.CeilToInt((b.max.x - _field.Origin.x) / _field.CellSize) - 1;
                int y1 = Mathf.CeilToInt((b.max.z - _field.Origin.z) / _field.CellSize) - 1;
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        _field.SetBlocked(x, y, true);
                    }
                }

                // 해석적 충돌장: 실제 박스 그대로 등록(매끄러운 벽 충돌)
                _obstacles.Add(b.min, b.max);
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
                if (_windowRadius > 0)
                {
                    _field.ComputeWindow(_target.position, _windowRadius);
                }
                else
                {
                    _field.Compute(_target.position);
                }
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

            // 그릴 영역을 플레이어(목표) 주변으로 한정 — 큰 맵에서 매 프레임 전체 셀 순회 방지.
            int cgx = w / 2;
            int cgy = h / 2;
            if (_target != null)
            {
                cgx = Mathf.FloorToInt((_target.position.x - origin.x) / cs);
                cgy = Mathf.FloorToInt((_target.position.z - origin.z) / cs);
            }

            int r = _gizmoRadius > 0 ? _gizmoRadius : Mathf.Max(w, h);
            int minX = Mathf.Clamp(cgx - r, 0, w - 1);
            int maxX = Mathf.Clamp(cgx + r, 0, w - 1);
            int minY = Mathf.Clamp(cgy - r, 0, h - 1);
            int maxY = Mathf.Clamp(cgy + r, 0, h - 1);

            // 그리드 외곽(전체) + 셀 라인(영역 한정)
            if (_drawGrid)
            {
                Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
                Gizmos.DrawWireCube(new Vector3(origin.x + w * cs * 0.5f, y, origin.z + h * cs * 0.5f), new Vector3(w * cs, 0.01f, h * cs));

                Gizmos.color = new Color(1f, 1f, 1f, 0.12f);
                for (int x = minX; x <= maxX + 1; x++)
                {
                    Gizmos.DrawLine(new Vector3(origin.x + x * cs, y, origin.z + minY * cs), new Vector3(origin.x + x * cs, y, origin.z + (maxY + 1) * cs));
                }

                for (int z = minY; z <= maxY + 1; z++)
                {
                    Gizmos.DrawLine(new Vector3(origin.x + minX * cs, y, origin.z + z * cs), new Vector3(origin.x + (maxX + 1) * cs, y, origin.z + z * cs));
                }
            }

            // 편집 모드용 장애물 목록(플레이 모드는 _field.IsBlocked 사용)
            NavObstacle[] obstacles = !playing ? FindObjectsByType<NavObstacle>(FindObjectsInactive.Exclude) : null;

            // 현재 그리는 뷰의 카메라 — 화면(프러스텀) 밖 셀은 건너뛴다.
            Camera cam = Camera.current;

            for (int gy = minY; gy <= maxY; gy++)
            {
                for (int gx = minX; gx <= maxX; gx++)
                {
                    Vector3 center = new Vector3(origin.x + (gx + 0.5f) * cs, y, origin.z + (gy + 0.5f) * cs);

                    if (cam != null)
                    {
                        Vector3 vp = cam.WorldToViewportPoint(center);
                        if (vp.z <= 0f || vp.x < -0.02f || vp.x > 1.02f || vp.y < -0.02f || vp.y > 1.02f)
                        {
                            continue; // 화면 밖 → 그리지 않음
                        }
                    }

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
