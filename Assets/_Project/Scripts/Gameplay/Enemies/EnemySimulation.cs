using System.Collections.Generic;
using Tartisians.Core.Services;
using Tartisians.Gameplay.Combat;
using Tartisians.Systems.Navigation;
using Tartisians.Systems.Spatial;
using UnityEngine;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>
    /// 모든 적의 이동을 한 곳에서 갱신한다(데이터 지향). 매 FixedUpdate:
    /// 1) 활성 적 위치로 공간 해시 재구성 2) seek+separation 이동 3) 플레이어 접촉 데미지.
    /// GameObject마다 Update를 돌리지 않아 대량에서도 저렴하다.
    /// </summary>
    public sealed class EnemySimulation : MonoBehaviour
    {
        [SerializeField] EnemySpawner _spawner;
        [SerializeField] Transform _target;
        [SerializeField] float _cellSize = 2f;
        [SerializeField] float _separationRadius = 1.2f;
        [SerializeField] float _separationWeight = 1.5f;
        [SerializeField] float _contactRadius = 1.2f;
        [SerializeField] float _wallClearance = 1.5f;
        [SerializeField] float _wallRepelWeight = 2f;

        SpatialHashGrid _grid;
        Health _playerHealth;
        FlowField _flowField;
        readonly List<Vector3> _positions = new(256);
        readonly List<int> _neighbors = new(32);

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

            _positions.Clear();
            for (int i = 0; i < count; i++)
            {
                _positions.Add(active[i].Position);
            }

            _grid.Rebuild(_positions);

            if (_flowField == null)
            {
                ServiceLocator.TryGet(out _flowField);
            }

            Vector3 targetPos = _target.position;
            float dt = Time.fixedDeltaTime;
            float contactR2 = _contactRadius * _contactRadius;

            for (int i = 0; i < count; i++)
            {
                Enemy enemy = active[i];
                Vector3 self = _positions[i];

                _grid.Query(self, _separationRadius, _neighbors);
                Vector3 separation = EnemySteering.Separation(self, _positions, _neighbors, _separationRadius);

                // 글로벌 네비게이션: 흐름장 방향 샘플(장애물 우회). 없거나 셀 밖이면 직선 추적으로 폴백.
                Vector3 seekDir = targetPos - self;
                Vector3 avoidance = separation * _separationWeight;

                if (_flowField != null)
                {
                    Vector3 flow = _flowField.SampleDirection(self);
                    if (flow != Vector3.zero)
                    {
                        seekDir = flow;
                    }

                    // 벽 반발: 거리장 그래디언트로 벽에서 멀어지는 힘(가까울수록 강함)
                    float wallDist = _flowField.DistanceToObstacle(self);
                    if (wallDist < _wallClearance)
                    {
                        Vector3 grad = _flowField.ObstacleGradient(self);
                        avoidance += grad * (_wallRepelWeight * (1f - wallDist / _wallClearance));
                    }
                }

                float speed = enemy.Definition != null ? enemy.Definition.MoveSpeed : 3f;
                Vector3 delta = EnemySteering.ComputeMove(seekDir, avoidance, speed, dt);

                // 침투 방지: 이동 후 위치가 장애물(적 반경) 안이면 그래디언트로 정확히 밀어낸다.
                if (_flowField != null)
                {
                    Vector3 newPos = self + delta;
                    float radius = enemy.Definition != null ? enemy.Definition.Radius : 0.5f;
                    float d = _flowField.DistanceToObstacle(newPos);
                    if (d < radius)
                    {
                        Vector3 grad = _flowField.ObstacleGradient(newPos);
                        if (grad != Vector3.zero)
                        {
                            delta += grad * (radius - d);
                        }
                    }
                }

                enemy.Move(delta);

                if (_playerHealth != null && (self - targetPos).sqrMagnitude <= contactR2)
                {
                    float dps = enemy.Definition != null ? enemy.Definition.ContactDamagePerSecond : 5f;
                    _playerHealth.TakeDamage(dps * dt);
                }
            }
        }
    }
}
