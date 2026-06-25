using Tartisians.Data;
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

        PrefabPool<Enemy> _pool;
        readonly EnemyRegistry _registry = new();
        float _timer;

        public EnemyRegistry Registry => _registry;

        void Awake()
        {
            _pool = new PrefabPool<Enemy>(_enemyPrefab, transform, defaultCapacity: 64, maxSize: 2000);
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
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 pos = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _wave.SpawnRadius;
            pos.y = 1f;
            enemy.transform.position = pos;

            enemy.Initialize(def);
            enemy.Despawned -= HandleDespawn;
            enemy.Despawned += HandleDespawn;
            _registry.Add(enemy);
            return enemy;
        }

        void HandleDespawn(Enemy enemy)
        {
            enemy.Despawned -= HandleDespawn;
            _registry.Remove(enemy);
            _pool.Release(enemy);
        }
    }
}
