using System.Collections.Generic;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Enemies;
using Tartisians.Gameplay.Progression;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>
    /// 플레이어에 부착되어 일정 간격으로 사거리 내 최근접 적을 향해 투사체를 자동 발사한다.
    /// 무기 수치는 RunStats(업그레이드 반영)에서 읽고, 없으면 WeaponDefinition로 폴백한다.
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] WeaponDefinition _weapon;
        [SerializeField] Projectile _projectilePrefab;
        [SerializeField] float _leadFactor = 0.6f; // 예측 사격 강도(0=없음, 1=완전 선행)

        PrefabPool<Projectile> _pool;
        EnemyRegistry _registry;
        RunStats _stats;
        float _timer;

        float Range => _stats != null ? _stats.WeaponRange : (_weapon != null ? _weapon.Range : 0f);
        float FireInterval => _stats != null ? _stats.WeaponFireInterval : (_weapon != null ? _weapon.FireInterval : 1f);

        void Awake()
        {
            if (_projectilePrefab != null)
            {
                _pool = new PrefabPool<Projectile>(_projectilePrefab, null, defaultCapacity: 64, maxSize: 1000);
            }

            ServiceLocator.TryGet(out _registry);
            ServiceLocator.TryGet(out _stats);
        }

        void Update()
        {
            if (_pool == null)
            {
                return;
            }

            if (_registry == null)
            {
                ServiceLocator.TryGet(out _registry);
            }

            if (_stats == null)
            {
                ServiceLocator.TryGet(out _stats);
            }

            _timer += Time.deltaTime;
            float interval = FireInterval;
            while (_timer >= interval)
            {
                _timer -= interval;
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
            float bestSq = Range * Range;

            IReadOnlyList<Enemy> active = _registry.Active;
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

            float speed = _stats != null ? _stats.ProjectileSpeed : _weapon.ProjectileSpeed;

            // 예측 사격: 투사체 도달 시점의 적 예상 위치를 겨냥(약간)
            Vector3 aimPoint = Targeting.PredictAimPoint(self, nearest.Position, nearest.Velocity, speed, _leadFactor);
            Vector3 dir = aimPoint - self;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            dir.Normalize();

            float damage = _stats != null ? _stats.WeaponDamage : _weapon.Damage;
            int pierce = _stats != null ? _stats.WeaponPierce : _weapon.Pierce;
            float lifetime = _stats != null ? _stats.WeaponLifetime : _weapon.Lifetime;

            // 고정 높이가 아니라 대상 적의 높이(게임플레이 평면)에서 발사한다.
            // 키 작은 적도 투사체에 맞도록 한다(투사체는 dir.y=0으로 수평 비행).
            Vector3 spawn = self;
            spawn.y = nearest.Position.y;

            Projectile proj = _pool.Get();
            proj.transform.position = spawn;
            proj.Launch(dir, speed, damage, pierce, lifetime, _pool);
        }
    }
}
