using System.Collections.Generic;
using Tartisians.Core.Services;
using Tartisians.Gameplay.Combat;
using Tartisians.Systems.Crowd;
using Tartisians.Systems.Navigation;
using Tartisians.Systems.Spatial;
using UnityEngine;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>
    /// 모든 적의 이동을 한 곳에서 갱신한다(데이터 지향). 매 FixedUpdate:
    /// 1) 흐름장에서 선호속도(전역 라우팅) 수집 2) PBD 군중 솔버로 비침투/벽 제약을 통일 투영
    /// 3) 결과를 Move()로 적용 + 플레이어 접촉 데미지.
    /// 군중 처리(밀지 않음·공간 없으면 정지·벽 비침투)는 전부 <see cref="CrowdSolver"/>가 담당한다.
    /// </summary>
    public sealed class EnemySimulation : MonoBehaviour
    {
        [SerializeField] EnemySpawner _spawner;
        [SerializeField] Transform _target;
        [SerializeField] float _cellSize = 2f;
        [SerializeField] float _contactRadius = 1.2f;
        [SerializeField] CrowdSolver _solver = new();

        SpatialHashGrid _grid;
        Health _playerHealth;
        FlowField _flowField;
        ObstacleField _obstacles;

        readonly List<Vector3> _positions = new(256);
        readonly List<Vector3> _velocities = new(256);
        readonly List<Vector3> _preferred = new(256);
        readonly List<float> _radii = new(256);
        readonly List<float> _maxSpeeds = new(256);

        void Awake()
        {
            _grid = new SpatialHashGrid(_cellSize);
            if (_target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    _target = p.transform;
                }
            }

            // _target이 인스펙터로 미리 연결돼 있어도 항상 Health를 해석한다(접촉 데미지용).
            if (_target != null)
            {
                _target.TryGetComponent(out _playerHealth);
            }
        }

        void FixedUpdate()
        {
            if (_spawner == null || _target == null)
            {
                return;
            }

            IReadOnlyList<Enemy> active = _spawner.Registry.Active;
            int count = active.Count;
            if (count == 0)
            {
                return;
            }

            if (_flowField == null)
            {
                ServiceLocator.TryGet(out _flowField);
            }

            if (_obstacles == null)
            {
                ServiceLocator.TryGet(out _obstacles);
            }

            Vector3 targetPos = _target.position;
            targetPos.y = 0f;
            float dt = Time.fixedDeltaTime;

            // 1) 적 상태 + 선호속도(흐름장 → 전역 라우팅, 폴백 직선) 수집
            _positions.Clear();
            _velocities.Clear();
            _preferred.Clear();
            _radii.Clear();
            _maxSpeeds.Clear();

            for (int i = 0; i < count; i++)
            {
                Enemy e = active[i];
                Vector3 p = e.Position;
                p.y = 0f;
                float radius = e.Definition != null ? e.Definition.Radius : 0.5f;
                float speed = e.Definition != null ? e.Definition.MoveSpeed : 3f;

                Vector3 dir;
                if (_flowField != null)
                {
                    dir = _flowField.SampleDirection(p);
                    if (dir == Vector3.zero)
                    {
                        dir = targetPos - p;
                    }
                }
                else
                {
                    dir = targetPos - p;
                }

                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                }

                Vector3 v = e.Velocity;
                v.y = 0f;

                _positions.Add(p);
                _velocities.Add(v);
                _preferred.Add(dir * speed);
                _radii.Add(radius);
                _maxSpeeds.Add(speed);
            }

            // 2) PBD 군중 솔버: 적-적/적-벽 비침투를 하나의 제약 투영으로 해소
            //    벽 충돌은 해석적 ObstacleField(매끄러움) — 격자 흐름장은 선호속도 라우팅 전용
            _solver.Step(count, _positions, _velocities, _preferred, _radii, _maxSpeeds, _grid,
                _obstacles, dt);

            // 3) 적용 + 접촉 데미지
            float contactR2 = _contactRadius * _contactRadius;
            for (int i = 0; i < count; i++)
            {
                Enemy e = active[i];
                Vector3 oldP = e.Position;
                Vector3 newP = _positions[i];
                Vector3 step = new Vector3(newP.x - oldP.x, 0f, newP.z - oldP.z);
                e.Move(step);
                e.TickFx(dt); // 피격 플래시/펀치 감쇠(중앙 틱)

                if (_playerHealth != null)
                {
                    Vector3 toPlayer = oldP - targetPos;
                    toPlayer.y = 0f;
                    if (toPlayer.sqrMagnitude <= contactR2)
                    {
                        float dps = e.Definition != null ? e.Definition.ContactDamagePerSecond : 5f;
                        _playerHealth.TakeDamage(dps * dt);
                    }
                }
            }
        }
    }
}
