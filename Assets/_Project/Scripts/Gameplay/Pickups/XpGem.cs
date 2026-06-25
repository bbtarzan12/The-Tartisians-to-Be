using System;
using Tartisians.Core.Events;
using Tartisians.Gameplay.Events;
using Tartisians.Gameplay.Progression;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Pickups
{
    /// <summary>
    /// 풀링되는 경험치 젬. 플레이어가 pickupRadius 안에 들어오면 자석처럼 끌려가고,
    /// 충분히 가까워지면 XP를 부여(XpCollectedEvent)한 뒤 풀로 반환한다.
    /// </summary>
    public sealed class XpGem : MonoBehaviour, IPoolable
    {
        [SerializeField] float _magnetSpeed = 12f;
        [SerializeField] float _collectDistance = 0.6f;

        Transform _player;
        RunStats _stats;
        int _xp;
        Action<XpGem> _onCollected;

        public void Configure(int xp, Transform player, RunStats stats, Action<XpGem> onCollected)
        {
            _xp = xp;
            _player = player;
            _stats = stats;
            _onCollected = onCollected;
        }

        void Update()
        {
            if (_player == null)
            {
                return;
            }

            Vector3 to = _player.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (dist <= _collectDistance)
            {
                Collect();
                return;
            }

            float radius = _stats != null ? _stats.PickupRadius : 2.5f;
            if (dist <= radius && dist > 1e-4f)
            {
                transform.position += to / dist * (_magnetSpeed * Time.deltaTime);
            }
        }

        void Collect()
        {
            EventBus<XpCollectedEvent>.Raise(new XpCollectedEvent { Amount = _xp });
            Action<XpGem> cb = _onCollected;
            _onCollected = null;
            cb?.Invoke(this);
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned() => _onCollected = null;
    }
}
