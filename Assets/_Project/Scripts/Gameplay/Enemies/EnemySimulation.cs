using System.Collections.Generic;
using Tartisians.Gameplay.Combat;
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

        SpatialHashGrid _grid;
        Health _playerHealth;
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
                    p.TryGetComponent(out _playerHealth);
                }
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

            Vector3 targetPos = _target.position;
            float dt = Time.fixedDeltaTime;
            float contactR2 = _contactRadius * _contactRadius;

            for (int i = 0; i < count; i++)
            {
                Enemy enemy = active[i];
                Vector3 self = _positions[i];

                _grid.Query(self, _separationRadius, _neighbors);
                Vector3 separation = EnemySteering.Separation(self, _positions, _neighbors, _separationRadius);

                float speed = enemy.Definition != null ? enemy.Definition.MoveSpeed : 3f;
                Vector3 delta = EnemySteering.ComputeDelta(self, targetPos, separation, speed, _separationWeight, dt);
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
