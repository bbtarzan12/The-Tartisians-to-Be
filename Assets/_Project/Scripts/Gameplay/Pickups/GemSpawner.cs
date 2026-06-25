using Tartisians.Core.Events;
using Tartisians.Core.Services;
using Tartisians.Gameplay.Events;
using Tartisians.Gameplay.Progression;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Pickups
{
    /// <summary>
    /// EnemyDiedEvent를 구독해 사망 위치에 XP 젬을 풀에서 스폰한다.
    /// 젬 수집 시 콜백으로 풀에 반환한다.
    /// </summary>
    public sealed class GemSpawner : MonoBehaviour
    {
        [SerializeField] XpGem _gemPrefab;
        [SerializeField] Transform _player;

        PrefabPool<XpGem> _pool;
        RunStats _stats;
        EventBinding<EnemyDiedEvent> _binding;

        void Awake()
        {
            if (_gemPrefab != null)
            {
                _pool = new PrefabPool<XpGem>(_gemPrefab, transform, defaultCapacity: 64, maxSize: 2000);
            }

            if (_player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    _player = p.transform;
                }
            }

            ServiceLocator.TryGet(out _stats);
        }

        void OnEnable()
        {
            _binding = new EventBinding<EnemyDiedEvent>(OnEnemyDied);
            EventBus<EnemyDiedEvent>.Register(_binding);
        }

        void OnDisable()
        {
            if (_binding != null)
            {
                EventBus<EnemyDiedEvent>.Deregister(_binding);
            }
        }

        void OnEnemyDied(EnemyDiedEvent e)
        {
            if (_pool == null || e.XpReward <= 0)
            {
                return;
            }

            if (_stats == null)
            {
                ServiceLocator.TryGet(out _stats);
            }

            XpGem gem = _pool.Get();
            gem.transform.position = new Vector3(e.Position.x, 0.5f, e.Position.z);
            gem.Configure(e.XpReward, _player, _stats, Release);
        }

        void Release(XpGem gem) => _pool.Release(gem);
    }
}
