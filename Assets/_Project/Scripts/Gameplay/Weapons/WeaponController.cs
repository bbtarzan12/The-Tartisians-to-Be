using System;
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
    /// 발사 타이머로 굴리고, 무기의 fireMode/aimMode/motion에 따라 발사·조준·이동을 분기한다(M8).
    /// 유효 스탯 = WeaponInstance(정의×레벨×전역 패시브 수정자).
    /// </summary>
    public sealed class WeaponController : MonoBehaviour
    {
        const float ClusterRadius = 4f; // DensestCluster 밀집 판정 반경
        const float LaneHalfWidth = 0.9f;

        [SerializeField] Projectile _projectilePrefab;
        [SerializeField] float _leadFactor = 0.6f; // 예측 사격 강도(0=없음)

        PrefabPool<Projectile> _pool;
        EnemyRegistry _registry;
        BuildState _build;
        ObstacleField _obstacles;
        WeaponVfx _vfx;
        readonly List<Enemy> _candidates = new();
        readonly List<Vector3> _flatPositions = new();
        Vector3 _aimFrom;
        Comparison<Enemy> _byDistance;
        Comparison<Enemy> _byHealth;

        void Awake()
        {
            if (_projectilePrefab != null)
            {
                _pool = new PrefabPool<Projectile>(_projectilePrefab, null, defaultCapacity: 64, maxSize: 1000);
            }

            ServiceLocator.TryGet(out _registry);
            ServiceLocator.TryGet(out _build);
            _vfx = GetComponent<WeaponVfx>();

            _byDistance = (a, b) => (a.Position - _aimFrom).sqrMagnitude.CompareTo((b.Position - _aimFrom).sqrMagnitude);
            _byHealth = (a, b) => a.CurrentHealth.CompareTo(b.CurrentHealth);
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
                    Fire(w.Def, eff);
                }

                if (w.FireTimer > eff.FireInterval)
                {
                    w.FireTimer = 0f; // 과누적 클램프(긴 프레임/정지 후 복귀)
                }
            }
        }

        void Fire(WeaponDefinition def, in EffectiveWeaponStats eff)
        {
            switch (def.FireMode)
            {
                case WeaponFireMode.SpreadProjectile: FireSpread(def, eff); break;
                case WeaponFireMode.AuraField: FireAura(eff); break;
                case WeaponFireMode.PierceLine: FireLance(def, eff); break;
                case WeaponFireMode.Orbital: FireOrbit(eff); break;
                default: FireNearest(def, eff); break;
            }
        }

        // 조준 모드로 정렬된 표적 eff.Amount명에게 각각 1발(이동 행동은 def.Motion).
        void FireNearest(WeaponDefinition def, in EffectiveWeaponStats eff)
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

            OrderCandidates(self, def.AimMode);
            ProjectileMotionConfig motion = MotionOf(def, eff);

            // 호밍은 표적이 적어도 eff.Amount발을 부채꼴로 일제사격(각자 호밍해 재획득) → 더 화려함.
            if (def.Motion == ProjectileMotion.Homing && eff.Amount > 1)
            {
                FireHomingVolley(def, eff, self, motion);
                return;
            }

            int shots = Mathf.Min(eff.Amount, _candidates.Count);
            for (int s = 0; s < shots; s++)
            {
                LaunchAt(_candidates[s], eff, self, def, motion);
            }
        }

        // eff.Amount발을 조준 방향 중심 부채꼴로 동시 발사. 각 미사일이 이후 최근접을 호밍한다.
        void FireHomingVolley(WeaponDefinition def, in EffectiveWeaponStats eff, Vector3 self, in ProjectileMotionConfig motion)
        {
            Enemy focus = _candidates[0]; // OrderCandidates로 최근접이 앞에 옴
            Vector3 dir = focus.Position - self;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                dir = transform.forward;
            }

            dir.Normalize();
            int n = Mathf.Max(1, eff.Amount);
            float spreadTotal = Mathf.Min(150f, 45f * (n - 1)); // 미사일 간 45°(넓게 사출), 최대 150°
            float start = -spreadTotal * 0.5f;
            float step = n > 1 ? spreadTotal / (n - 1) : 0f;
            float spawnY = focus.Position.y;

            for (int i = 0; i < n; i++)
            {
                float ang = n == 1 ? 0f : start + step * i;
                Vector3 d = Quaternion.Euler(0f, ang, 0f) * dir;
                Vector3 spawn = self;
                spawn.y = spawnY;
                Projectile p = _pool.Get();
                p.transform.position = spawn;
                p.Launch(d, eff.ProjectileSpeed, eff.Damage, eff.Pierce, eff.Lifetime, _pool, def.Color, def.VfxScale, motion);
            }
        }

        // 조준 방향을 중심으로 eff.Amount발을 eff.Area(부채각, 도) 범위로 분산.
        void FireSpread(WeaponDefinition def, in EffectiveWeaponStats eff)
        {
            Vector3 self = transform.position;
            Enemy focus = AimTarget(self, eff, def);
            if (focus == null)
            {
                return;
            }

            Vector3 dir = focus.Position - self;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            dir.Normalize();
            float spawnY = focus.Position.y;
            int n = Mathf.Max(1, eff.Amount);
            float fan = Mathf.Max(0f, eff.Area);
            float start = -fan * 0.5f;
            float step = n > 1 ? fan / (n - 1) : 0f;
            ProjectileMotionConfig motion = MotionOf(def, eff);

            for (int i = 0; i < n; i++)
            {
                float ang = n == 1 ? 0f : start + step * i;
                Vector3 d = Quaternion.Euler(0f, ang, 0f) * dir;
                Vector3 spawn = self;
                spawn.y = spawnY;
                Projectile p = _pool.Get();
                p.transform.position = spawn;
                p.Launch(d, eff.ProjectileSpeed, eff.Damage, eff.Pierce, eff.Lifetime, _pool, def.Color, def.VfxScale, motion);
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

        // 조준 방향(기본 MostInLine)으로 길이 eff.Area·반폭 고정 관통 라인 안의 모든 적에게 즉시 데미지.
        void FireLance(WeaponDefinition def, in EffectiveWeaponStats eff)
        {
            if (_registry == null)
            {
                return;
            }

            Vector3 self = transform.position;
            Enemy focus = AimTarget(self, eff, def);
            if (focus == null)
            {
                return;
            }

            Vector3 dir = focus.Position - self;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f)
            {
                return;
            }

            dir.Normalize();
            float length = Mathf.Max(1f, eff.Area);

            if (_vfx != null)
            {
                _vfx.FlashLance(self, dir, length, def.Color);
            }

            IReadOnlyList<Enemy> active = _registry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead)
                {
                    continue;
                }

                if (WeaponGeometry.PointInLane(self, dir, length, LaneHalfWidth, e.Position))
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

        // 단일 표적 조준(방향/포커스 1명). 모드별 선택.
        Enemy AimTarget(Vector3 self, in EffectiveWeaponStats eff, WeaponDefinition def)
        {
            switch (def.AimMode)
            {
                case WeaponAimMode.LowestHealth:
                    return LowestHealthVisible(self, eff.Range);

                case WeaponAimMode.DensestCluster:
                {
                    GatherVisible(self, eff.Range);
                    if (_candidates.Count == 0)
                    {
                        return null;
                    }

                    BuildFlat();
                    int idx = WeaponAiming.DensestClusterIndex(self, _flatPositions, ClusterRadius);
                    return idx >= 0 ? _candidates[idx] : null;
                }

                case WeaponAimMode.MostInLine:
                {
                    float length = Mathf.Max(eff.Range, eff.Area);
                    GatherVisible(self, length);
                    if (_candidates.Count == 0)
                    {
                        return null;
                    }

                    BuildFlat();
                    int idx = WeaponAiming.BestLaneIndex(self, _flatPositions, length, LaneHalfWidth);
                    return idx >= 0 ? _candidates[idx] : null;
                }

                default:
                    return NearestVisible(self, eff.Range);
            }
        }

        // _candidates를 조준 모드에 맞게 앞쪽부터 정렬(다발 표적 선택용).
        void OrderCandidates(Vector3 self, WeaponAimMode mode)
        {
            _aimFrom = self;
            switch (mode)
            {
                case WeaponAimMode.LowestHealth:
                    _candidates.Sort(_byHealth);
                    break;

                case WeaponAimMode.DensestCluster:
                {
                    BuildFlat();
                    int idx = WeaponAiming.DensestClusterIndex(self, _flatPositions, ClusterRadius);
                    if (idx > 0)
                    {
                        (_candidates[0], _candidates[idx]) = (_candidates[idx], _candidates[0]);
                    }

                    break;
                }

                default: // Nearest / MostInLine(투사체에선 근접 폴백)
                    _candidates.Sort(_byDistance);
                    break;
            }
        }

        ProjectileMotionConfig MotionOf(WeaponDefinition def, in EffectiveWeaponStats eff)
        {
            return new ProjectileMotionConfig
            {
                Motion = def.Motion,
                HomingTurnRateDeg = def.HomingTurnRate,
                HomingArmTime = 0.3f, // 초기 사출(직진) 시간 — 발사 직후 흩어진 뒤 호밍
                RicochetRange = def.RicochetRange,
                OutDuration = eff.Lifetime * 0.5f, // 부메랑: 절반은 나가고 절반은 복귀
            };
        }

        void LaunchAt(Enemy target, in EffectiveWeaponStats eff, Vector3 self, WeaponDefinition def, in ProjectileMotionConfig motion)
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
            p.Launch(dir, eff.ProjectileSpeed, eff.Damage, eff.Pierce, eff.Lifetime, _pool, def.Color, def.VfxScale, motion);
        }

        void BuildFlat()
        {
            _flatPositions.Clear();
            for (int i = 0; i < _candidates.Count; i++)
            {
                Vector3 p = _candidates[i].Position;
                p.y = 0f;
                _flatPositions.Add(p);
            }
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

        Enemy LowestHealthVisible(Vector3 self, float range)
        {
            if (_registry == null)
            {
                return null;
            }

            Enemy best = null;
            float bestHp = float.MaxValue;
            float rangeSq = range * range;
            IReadOnlyList<Enemy> active = _registry.Active;
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

                if (e.CurrentHealth < bestHp)
                {
                    bestHp = e.CurrentHealth;
                    best = e;
                }
            }

            return best;
        }
    }
}
