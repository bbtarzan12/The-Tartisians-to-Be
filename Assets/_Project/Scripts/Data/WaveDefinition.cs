using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 스폰 규칙. M3에서는 간단히 일정 간격으로 maxAlive까지 무작위 적을 스폰한다.
    /// 시간대별 램프/보스는 후속 단계에서 확장한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Wave Definition", fileName = "WaveDefinition")]
    public sealed class WaveDefinition : ScriptableObject
    {
        [SerializeField] float _spawnInterval = 0.2f;
        [SerializeField] int _maxAlive = 200;
        [SerializeField] float _spawnRadius = 18f;
        [SerializeField] EnemyDefinition[] _enemies;

        public float SpawnInterval => Mathf.Max(0.01f, _spawnInterval);
        public int MaxAlive => _maxAlive;
        public float SpawnRadius => _spawnRadius;
        public bool HasEnemies => _enemies != null && _enemies.Length > 0;

        public EnemyDefinition PickRandom()
        {
            if (!HasEnemies)
            {
                return null;
            }

            return _enemies[Random.Range(0, _enemies.Length)];
        }
    }
}
