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
            if (_wave == null || _enemyPrefab == null || !_wave.HasEnemies)
            {
                return;
            }

            _timer += Time.deltaTime;
            while (_timer >= _wave.SpawnInterval && _registry.Count < _wave.MaxAlive)
            {
                _timer -= _wave.SpawnInterval;
                SpawnOne();
            }
        }

        public Enemy SpawnOne()
        {
            if (_wave == null || _pool == null)
            {
                return null;
            }

            EnemyDefinition def = _wave.PickRandom();
            if (def == null)
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
            float minDist = _wave != null ? _wave.SpawnRadius : 18f;
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
