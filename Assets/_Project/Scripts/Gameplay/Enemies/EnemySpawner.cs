using System.Collections.Generic;
using Tartisians.Core.Events;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Events;
using Tartisians.Gameplay.Vfx;
using Tartisians.Systems.Crowd;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>
    /// WaveDefinition에 따라 플레이어 주변 링에 적을 풀에서 스폰한다.
    /// 사망 시 풀로 반환하고 레지스트리에서 제거한다.
    /// </summary>
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] Enemy _enemyPrefab;
        [Tooltip("시간축 시나리오. 지정되면 이 곡선으로 구동하고 WaveDefinition은 무시한다.")]
        [SerializeField] SpawnScenario _scenario;
        [Tooltip("시나리오가 없을 때 쓰는 단순 폴백 스폰 규칙.")]
        [SerializeField] WaveDefinition _wave;
        [SerializeField] Transform _target;

        [Header("Spawn placement")]
        [SerializeField] float _arenaHalfExtent = 18f;     // 벽 안쪽 스폰 한계(±)
        [SerializeField] float _spawnBandDepth = 10f;      // SpawnRadius부터 바깥으로 샘플링할 폭
        [SerializeField] float _minPlayerClearance = 9f;   // 플레이어로부터 최소 거리(클램프 후에도)
        [SerializeField, Range(0f, 0.2f)] float _offscreenMargin = 0.04f; // 뷰포트 여유(이만큼 화면 밖)
        [SerializeField] int _spawnAttempts = 24;          // 거부 샘플링 시도 횟수

        PrefabPool<Enemy> _pool;
        readonly EnemyRegistry _registry = new();
        float _timer;
        Camera _cam;
        ObstacleField _obstacles;

        // 시나리오 구동 상태
        float _elapsed;            // 판 경과 시간(timeScale=0이면 자연 정지)
        int _nextBurst;            // 다음에 터질 버스트 인덱스
        float _activeRadius = 18f; // 현재 단계의 스폰 링 반경
        readonly List<float> _weightScratch = new();

        public EnemyRegistry Registry => _registry;

        void Awake()
        {
            if (_enemyPrefab != null)
            {
                _pool = new PrefabPool<Enemy>(_enemyPrefab, transform, defaultCapacity: 64, maxSize: 2000);
            }

            ServiceLocator.Register(_registry);

            if (_target == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    _target = p.transform;
                }
            }
        }

        void Update()
        {
            if (_enemyPrefab == null || _pool == null)
            {
                return;
            }

            if (_scenario != null && _scenario.HasPhases)
            {
                TickScenario(Time.deltaTime);
                return;
            }

            if (_wave == null || !_wave.HasEnemies)
            {
                return;
            }

            _activeRadius = _wave.SpawnRadius;
            _timer += Time.deltaTime;
            while (_timer >= _wave.SpawnInterval && _registry.Count < _wave.MaxAlive)
            {
                _timer -= _wave.SpawnInterval;
                SpawnOne();
            }
        }

        /// <summary>시나리오 곡선으로 정상 스폰 + 일회성 버스트를 구동한다.</summary>
        void TickScenario(float dt)
        {
            _elapsed += dt;

            SpawnScenario.Phase phase = _scenario.PhaseAt(_elapsed);
            if (phase == null)
            {
                return;
            }

            _activeRadius = _scenario.RadiusOf(phase);

            // 예정 시각이 지난 버스트를 순서대로 발사(정상 상한 무시, 풀 한계까지).
            while (_nextBurst < _scenario.BurstCount && _scenario.GetBurst(_nextBurst).Time <= _elapsed)
            {
                FireBurst(_scenario.GetBurst(_nextBurst));
                _nextBurst++;
            }

            float interval = Mathf.Max(0.01f, phase.SpawnInterval);
            _timer += dt;
            while (_timer >= interval && _registry.Count < phase.MaxAlive)
            {
                _timer -= interval;
                SpawnFromPhase(phase);
            }
        }

        void FireBurst(SpawnScenario.Burst burst)
        {
            if (burst.Enemy == null || burst.Count <= 0)
            {
                return;
            }

            int ceiling = _pool.MaxSize;
            for (int i = 0; i < burst.Count && _registry.Count < ceiling; i++)
            {
                SpawnDef(burst.Enemy);
            }
        }

        void SpawnFromPhase(SpawnScenario.Phase phase)
        {
            EnemyDefinition def = PickWeighted(phase);
            if (def != null)
            {
                SpawnDef(def);
            }
        }

        EnemyDefinition PickWeighted(SpawnScenario.Phase phase)
        {
            if (phase.Weights == null || phase.Weights.Length == 0)
            {
                return null;
            }

            _weightScratch.Clear();
            for (int i = 0; i < phase.Weights.Length; i++)
            {
                _weightScratch.Add(phase.Weights[i].Enemy != null ? phase.Weights[i].Value : 0f);
            }

            int idx = SpawnSchedule.WeightedIndex(_weightScratch, Random.value);
            return idx >= 0 ? phase.Weights[idx].Enemy : null;
        }

        public Enemy SpawnOne()
        {
            if (_wave == null || _pool == null)
            {
                return null;
            }

            EnemyDefinition def = _wave.PickRandom();
            return def != null ? SpawnDef(def) : null;
        }

        Enemy SpawnDef(EnemyDefinition def)
        {
            if (def == null || _pool == null)
            {
                return null;
            }

            Enemy enemy = _pool.Get();

            Vector3 center = _target != null ? _target.position : Vector3.zero;
            Vector3 pos = ComputeSpawnPosition(center, def.Radius);
            enemy.SetPosition(pos); // transform + Rigidbody.position 동시 설정(원점 끌림 방지)

            enemy.Initialize(def);
            enemy.Despawned -= HandleDespawn;
            enemy.Despawned += HandleDespawn;
            _registry.Add(enemy);
            return enemy;
        }

        /// <summary>
        /// 화면 밖 + 아레나 안 + 장애물 밖 위치를 거부 샘플링으로 고른다.
        /// 플레이어 주변 [SpawnRadius, SpawnRadius+depth] 링에서 각도/거리를 뽑아 아레나로 클램프하고,
        /// 화면 안이거나 장애물 안이거나 너무 가까우면 버린다. 모두 실패 시 마지막 후보(아레나 안 보장).
        /// </summary>
        Vector3 ComputeSpawnPosition(Vector3 center, float enemyRadius)
        {
            if (_cam == null)
            {
                _cam = Camera.main;
            }

            if (_obstacles == null)
            {
                ServiceLocator.TryGet(out _obstacles);
            }

            center.y = 0f;
            float limit = Mathf.Max(0f, _arenaHalfExtent - enemyRadius);
            float minDist = _activeRadius > 0f ? _activeRadius : 18f;
            float maxDist = minDist + Mathf.Max(0f, _spawnBandDepth);
            float clearance2 = _minPlayerClearance * _minPlayerClearance;

            Vector3 fallback = new Vector3(center.x, 1f, center.z);
            for (int i = 0; i < _spawnAttempts; i++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist = Mathf.Lerp(minDist, maxDist, Random.value);
                Vector3 p = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;
                p.x = Mathf.Clamp(p.x, -limit, limit);
                p.z = Mathf.Clamp(p.z, -limit, limit);
                p.y = 1f;
                fallback = p;

                Vector3 flat = new Vector3(p.x - center.x, 0f, p.z - center.z);
                if (flat.sqrMagnitude < clearance2) // 클램프로 플레이어에 너무 붙음
                {
                    continue;
                }

                if (_obstacles != null && _obstacles.Distance(p) < enemyRadius + 0.1f) // 장애물 안
                {
                    continue;
                }

                if (IsOnScreen(p)) // 화면 안이면 버림 → 화면 밖에서만 스폰
                {
                    continue;
                }

                return p;
            }

            return fallback;
        }

        bool IsOnScreen(Vector3 world)
        {
            if (_cam == null)
            {
                return false;
            }

            Vector3 vp = _cam.WorldToViewportPoint(world);
            float m = _offscreenMargin;
            return vp.z > 0f && vp.x >= -m && vp.x <= 1f + m && vp.y >= -m && vp.y <= 1f + m;
        }

        void HandleDespawn(Enemy enemy)
        {
            enemy.Despawned -= HandleDespawn;
            _registry.Remove(enemy);

            // 사망 처리: VFX(풀) 재생 + 사망 이벤트 발행(XP 젬은 M5에서 구독)
            Vector3 pos = enemy.Position;
            int xp = enemy.Definition != null ? enemy.Definition.XpReward : 0;

            if (ServiceLocator.TryGet(out VfxService vfx))
            {
                vfx.PlayDeath(pos);
            }

            EventBus<EnemyDiedEvent>.Raise(new EnemyDiedEvent { Position = pos, XpReward = xp });

            _pool.Release(enemy);
        }
    }
}
