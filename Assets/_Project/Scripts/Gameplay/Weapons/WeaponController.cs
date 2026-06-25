using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Enemies;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>
    /// 플레이어에 부착되어 일정 간격으로 사거리 내 최근접 적을 향해 투사체를 자동 발사한다.
    /// 적 목록은 ServiceLocator에 등록된 EnemyRegistry에서 가져온다.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] WeaponDefinition _weapon;
        [SerializeField] Projectile _projectilePrefab;
        [SerializeField] float _muzzleHeight = 1f;

        PrefabPool<Projectile> _pool;
        EnemyRegistry _registry;
        float _timer;

        void Awake()
        {
            if (_projectilePrefab != null)
            {
                _pool = new PrefabPool<Projectile>(_projectilePrefab, null, defaultCapacity: 64, maxSize: 1000);
            }

            ServiceLocator.TryGet(out _registry);
        }

        void Update()
        {
            if (_weapon == null || _pool == null)
            {
                return;
            }

            if (_registry == null)
            {
                ServiceLocator.TryGet(out _registry);
            }

            _timer += Time.deltaTime;
            while (_timer >= _weapon.FireInterval)
            {
                _timer -= _weapon.FireInterval;
                Fire();
            }
        }

        void Fire()
        {
            if (_registry == null || _registry.Count == 0)
            {
                return;
            }

            Vector3 self = transform.position;
            Enemy nearest = null;
            float bestSq = _weapon.Range * _weapon.Range;

            System.Collections.Generic.IReadOnlyList<Enemy> active = _registry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead)
                {
                    continue;
                }

                float sq = (e.Position - self).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    nearest = e;
                }
            }

            if (nearest == null)
            {
                return;
            }

            Vector3 dir = nearest.Position - self;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            dir.Normalize();
            Projectile proj = _pool.Get();
            proj.transform.position = self + Vector3.up * _muzzleHeight;
            proj.Launch(dir, _weapon, _pool);
        }
    }
}
