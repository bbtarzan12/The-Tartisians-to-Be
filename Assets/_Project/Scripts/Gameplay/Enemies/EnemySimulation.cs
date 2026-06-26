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
        const float MaxEnemyRadius = 0.8f; // 이웃 질의 반경 보정용(Brute 기준)

        [SerializeField] float _separationRadius = 1.2f;
        [SerializeField] float _contactRadius = 1.2f;
        [SerializeField] float _wallClearance = 1.5f;

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
                float radius = enemy.Definition != null ? enemy.Definition.Radius : 0.5f;
                float speed = enemy.Definition != null ? enemy.Definition.MoveSpeed : 3f;

                // 1) 흐름 방향(장애물 우회). 없거나 셀 밖이면 직선 폴백.
                Vector3 seekDir = targetPos - self;
                if (_flowField != null)
                {
                    Vector3 flow = _flowField.SampleDirection(self);
                    if (flow != Vector3.zero)
                    {
                        seekDir = flow;
                    }

                    // 벽 접선: 흐름이 벽으로 향하는 성분을 제거(벽을 따라 미끄러짐)
                    float wallDist = _flowField.DistanceToObstacle(self);
                    if (wallDist < _wallClearance)
                    {
                        Vector3 away = _flowField.ObstacleGradient(self);
                        float into = Vector3.Dot(seekDir, -away);
                        if (into > 0f)
                        {
                            seekDir += away * into; // 벽으로 들어가는 성분 제거
                        }
                    }
                }

                // 흐름으로 전진한 잠정 위치
                Vector3 tentative = self + EnemySteering.ComputeMove(seekDir, Vector3.zero, speed, dt);

                // 2) 무푸시 위치 보정(PBD): 이웃과 겹치면 '힘'이 아니라 '위치'를 절반씩 밀어 해소.
                //    압력이 누적되지 않아 군중이 서로/벽으로 밀어붙이지 않고, 막히면 자연히 멈춘다.
                _grid.Query(self, radius + MaxEnemyRadius, _neighbors);
                for (int k = 0; k < _neighbors.Count; k++)
                {
                    int j = _neighbors[k];
                    if (j == i)
                    {
                        continue;
                    }

                    float minDist = radius + (active[j].Definition != null ? active[j].Definition.Radius : 0.5f);
                    Vector3 d = tentative - _positions[j];
                    d.y = 0f;
                    float dist = d.magnitude;
                    if (dist > 1e-4f && dist < minDist)
                    {
                        tentative += d / dist * ((minDist - dist) * 0.5f);
                    }
                }

                // 3) 벽 하드 클램프(적-적보다 우선): 보정 후에도 벽 안이면 SDF로 정확히 밀어냄.
                if (_flowField != null)
                {
                    float wd = _flowField.DistanceToObstacle(tentative);
                    if (wd < radius)
                    {
                        Vector3 away = _flowField.ObstacleGradient(tentative);
                        if (away != Vector3.zero)
                        {
                            tentative += away * (radius - wd);
                        }
                    }
                }

                // 위치 보정(겹침·벽)이 이동에 더해져 속도가 튀지 않도록 한 프레임 변위를 이동 예산으로 제한.
                Vector3 step = tentative - self;
                float maxStep = speed * dt;
                if (step.sqrMagnitude > maxStep * maxStep)
                {
                    step = step.normalized * maxStep;
                }

                enemy.Move(step);

                if (_playerHealth != null && (self - targetPos).sqrMagnitude <= contactR2)
                {
                    float dps = enemy.Definition != null ? enemy.Definition.ContactDamagePerSecond : 5f;
                    _playerHealth.TakeDamage(dps * dt);
                }
            }
        }
    }
}
