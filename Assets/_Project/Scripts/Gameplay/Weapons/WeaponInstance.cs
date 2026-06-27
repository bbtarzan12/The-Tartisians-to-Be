using Tartisians.Data;
using Tartisians.Gameplay.Progression;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>특정 레벨·수정자에서 계산된 무기의 유효 스탯(한 발사 시점의 실제 값).</summary>
    public struct EffectiveWeaponStats
    {
        public float Damage;
        public float FireInterval;
        public float ProjectileSpeed;
        public int Pierce;
        public float Range;
        public float Lifetime;
        public int Amount;
        public float Area;
    }

    /// <summary>
    /// 보유 무기 1개의 런타임 상태(정의 참조 + 현재 레벨 + 자체 발사 타이머).
    /// 유효 스탯 = (기본값 + 레벨 델타) × 전역 패시브 수정자. 순수 계산이라 단위 테스트 가능.
    /// </summary>
    public sealed class WeaponInstance
    {
        public WeaponDefinition Def { get; }
        public int Level { get; private set; }
        public float FireTimer; // 인벤토리가 굴리는 발사 누적 타이머(런타임 전용)

        public WeaponInstance(WeaponDefinition def, int level = 1)
        {
            Def = def;
            Level = Mathf.Clamp(level, 1, def != null ? def.MaxLevel : 1);
        }

        public bool IsMaxLevel => Def != null && Level >= Def.MaxLevel;

        public bool LevelUp()
        {
            if (IsMaxLevel)
            {
                return false;
            }

            Level++;
            return true;
        }

        public EffectiveWeaponStats Compute(in PassiveModifiers m)
        {
            int n = Level - 1; // Lv1 기준 누적 횟수
            float damage = (Def.Damage + Def.DamagePerLevel * n) * (1f + m.MightPct);
            float interval = Def.FireInterval / (1f + Def.FireRateReducePerLevel * n) / (1f + m.CooldownPct);
            float speed = (Def.ProjectileSpeed + Def.ProjectileSpeedPerLevel * n) * (1f + m.ProjectileSpeedPct);
            int pierce = Def.Pierce + Mathf.FloorToInt(Def.PiercePerLevel * n);
            int amount = Def.Amount + Mathf.FloorToInt(Def.AmountPerLevel * n) + m.AmountAdd;
            float area = (Def.Area + Def.AreaPerLevel * n) * (1f + m.AreaPct);

            return new EffectiveWeaponStats
            {
                Damage = damage,
                FireInterval = Mathf.Max(0.02f, interval),
                ProjectileSpeed = speed,
                Pierce = Mathf.Max(0, pierce),
                Range = Def.Range,
                Lifetime = Def.Lifetime,
                Amount = Mathf.Max(1, amount),
                Area = area,
            };
        }
    }
}
