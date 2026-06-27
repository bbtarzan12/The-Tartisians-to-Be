using System.Collections.Generic;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Enemies;
using Tartisians.Gameplay.Progression;
using Tartisians.Systems.Combat;
using Tartisians.Systems.Crowd;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>
    /// 플레이어의 무기 인벤토리 실행기. <see cref="BuildState"/>의 보유 무기 전부를 각자
    /// 발사 타이머로 굴리고, 무기의 fireMode에 따라 발사 방식을 분기한다(M8).
    /// 유효 스탯 = WeaponInstance(정의×레벨×전역 패시브 수정자).
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] Projectile _projectilePrefab;
        [SerializeField] float _leadFactor = 0.6f; // 예측 사격 강도(0=없음)

        PrefabPool<Projectile> _pool;
        EnemyRegistry _registry;
        BuildState _build;
        ObstacleField _obstacles;
        readonly List<Enemy> _candidates = new();

        void Awake()
        {
            if (_projectilePrefab != null)
            {
                _pool = new PrefabPool<Projectile>(_projectilePrefab, null, defaultCapacity: 64, maxSize: 1000);
            }

            ServiceLocator.TryGet(out _registry);
            ServiceLocator.TryGet(out _build);
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

            if (_build == null)
            {
                ServiceLocator.TryGet(out _build);
                if (_build == null)
                {
                    return;
                }
            }

            if (_obstacles == null)
            {
                ServiceLocator.TryGet(out _obstacles);
            }

            PassiveModifiers mods = _build.ComputeModifiers();
            float dt = Time.deltaTime;
            List<WeaponInstance> weapons = _build.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInstance w = weapons[i];
                EffectiveWeaponStats eff = w.Compute(mods);
                w.FireTimer += dt;

                int safety = 4; // 한 프레임 다발 발사 방지
                while (w.FireTimer >= eff.FireInterval && safety-- > 0)
                {
                    w.FireTimer -= eff.FireInterval;
                    Fire(w.Def.FireMode, eff);
                }

                if (w.FireTimer > eff.FireInterval)
                {
                    w.FireTimer = 0f; // 과누적 클램프(긴 프레임/정지 후 복귀)
                }
            }
        }

        void Fire(WeaponFireMode mode, in EffectiveWeaponStats eff)
        {
            switch (mode)
            {
                case WeaponFireMode.SpreadProjectile: FireSpread(eff); break;
                case WeaponFireMode.AuraField: FireAura(eff); break;
                case WeaponFireMode.PierceLine: FireLance(eff); break;
                case WeaponFireMode.Orbital: FireOrbit(eff); break;
                default: FireNearest(eff); break;
            }
        }

        // 사거리 내 시야 확보된 최근접 적 eff.Amount명에게 각각 1발.
        void FireNearest(in EffectiveWeaponStats eff)
        {
            if (_registry == null || _registry.Count == 0)
            {
                return;
            }

            Vector3 self = transform.position;
            GatherVisible(self, eff.Range);
            if (_candidates.Count == 0)
            {
                return;
            }

            int shots = Mathf.Min(eff.Amount, _candidates.Count);
            for (int s = 0; s < shots; s++)
            {
                // s번째로 가까운 적을 앞으로 선택정렬
                int best = s;
                float bestSq = (_candidates[s].Position - self).sqrMagnitude;
                for (int j = s + 1; j < _candidates.Count; j++)
                {
                    float d = (_candidates[j].Position - self).sqrMagnitude;
                    if (d < bestSq) { bestSq = d; best = j; }
                }

                (_candidates[s], _candidates[best]) = (_candidates[best], _candidates[s]);
                LaunchAt(_candidates[s], eff, self);
            }
        }

        // 최근접 적 방향을 중심으로 eff.Amount발을 eff.Area(부채각, 도) 범위로 분산.
        void FireSpread(in EffectiveWeaponStats eff)
        {
            Vector3 self = transform.position;
            Enemy nearest = NearestVisible(self, eff.Range);
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
            float spawnY = nearest.Position.y;
            int n = Mathf.Max(1, eff.Amount);
            float fan = Mathf.Max(0f, eff.Area);
            float start = -fan * 0.5f;
            float step = n > 1 ? fan / (n - 1) : 0f;

            for (int i = 0; i < n; i++)
            {
                float ang = n == 1 ? 0f : start + step * i;
                Vector3 d = Quaternion.Euler(0f, ang, 0f) * dir;
                Vector3 spawn = self;
                spawn.y = spawnY;
                Projectile p = _pool.Get();
                p.transform.position = spawn;
                p.Launch(d, eff.ProjectileSpeed, eff.Damage, eff.Pierce, eff.Lifetime, _pool);
            }
        }

        // 플레이어 중심 eff.Area 반경 내 모든 적에게 즉시 데미지(투사체 없음).
        void FireAura(in EffectiveWeaponStats eff)
        {
            if (_registry == null)
            {
                return;
            }

            Vector3 self = transform.position;
            float rSq = eff.Area * eff.Area;
            IReadOnlyList<Enemy> active = _registry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead)
                {
                    continue;
                }

                Vector3 d = e.Position - self;
                d.y = 0f;
                if (d.sqrMagnitude <= rSq)
                {
                    DamageSystem.Apply(e, eff.Damage);
                }
            }
        }

        // 최근접 적 방향으로 길이 eff.Area·반폭 고정인 관통 라인 안의 모든 적에게 즉시 데미지.
        void FireLance(in EffectiveWeaponStats eff)
        {
            if (_registry == null)
            {
                return;
            }

            Vector3 self = transform.position;
            Enemy nearest = NearestVisible(self, Mathf.Max(eff.Range, eff.Area));
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
            float length = Mathf.Max(1f, eff.Area);
            const float halfWidth = 0.9f;
            IReadOnlyList<Enemy> active = _registry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead)
                {
                    continue;
                }

                if (WeaponGeometry.PointInLane(self, dir, length, halfWidth, e.Position))
                {
                    DamageSystem.Apply(e, eff.Damage);
                }
            }
        }

        // 플레이어 주위를 도는 eff.Amount개 위성 위치에서 펄스 데미지(상시 회전, 90°/s).
        void FireOrbit(in EffectiveWeaponStats eff)
        {
            if (_registry == null)
            {
                return;
            }

            Vector3 self = transform.position;
            int n = Mathf.Max(1, eff.Amount);
            float radius = Mathf.Max(0.5f, eff.Area);
            const float satRadiusSq = 1.0f; // 위성 접촉 반경^2
            float baseAng = Time.time * (90f * Mathf.Deg2Rad);
            IReadOnlyList<Enemy> active = _registry.Active;

            for (int k = 0; k < n; k++)
            {
                float a = baseAng + k * (Mathf.PI * 2f / n);
                Vector3 sat = self + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                for (int i = 0; i < active.Count; i++)
                {
                    Enemy e = active[i];
                    if (e.IsDead)
                    {
                        continue;
                    }

                    Vector3 d = e.Position - sat;
                    d.y = 0f;
                    if (d.sqrMagnitude <= satRadiusSq)
                    {
                        DamageSystem.Apply(e, eff.Damage);
                    }
                }
            }
        }

        void LaunchAt(Enemy target, in EffectiveWeaponStats eff, Vector3 self)
        {
            Vector3 aim = Targeting.PredictAimPoint(self, target.Position, target.Velocity, eff.ProjectileSpeed, _leadFactor);
            Vector3 dir = aim - self;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            dir.Normalize();
            Vector3 spawn = self;
            spawn.y = target.Position.y;
            Projectile p = _pool.Get();
            p.transform.position = spawn;
            p.Launch(dir, eff.ProjectileSpeed, eff.Damage, eff.Pierce, eff.Lifetime, _pool);
        }

        void GatherVisible(Vector3 self, float range)
        {
            _candidates.Clear();
            IReadOnlyList<Enemy> active = _registry.Active;
            float rangeSq = range * range;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead)
                {
                    continue;
                }

                if ((e.Position - self).sqrMagnitude > rangeSq)
                {
                    continue;
                }

                if (_obstacles != null && _obstacles.Blocks(self, e.Position))
                {
                    continue;
                }

                _candidates.Add(e);
            }
        }

        Enemy NearestVisible(Vector3 self, float range)
        {
            if (_registry == null)
            {
                return null;
            }

            Enemy best = null;
            float bestSq = range * range;
            IReadOnlyList<Enemy> active = _registry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead)
                {
                    continue;
                }

                float sq = (e.Position - self).sqrMagnitude;
                if (sq > bestSq)
                {
                    continue;
                }

                if (_obstacles != null && _obstacles.Blocks(self, e.Position))
                {
                    continue;
                }

                bestSq = sq;
                best = e;
            }

            return best;
        }
    }
}
