using Tartisians.Data;
using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 한 판 동안의 가변 스탯. SO(불변 정의)를 런타임에 복사해 보관하고
    /// 업그레이드로 수정한다. 컨트롤러들은 SO 대신 이 값을 읽는다(ServiceLocator 등록).
    /// </summary>
    public sealed class RunStats
    {
        public float MoveSpeed;
        public float MaxHealth;
        public float PickupRadius;

        public float WeaponDamage;
        public float WeaponFireInterval;
        public int WeaponPierce;
        public float ProjectileSpeed;
        public float WeaponRange;
        public float WeaponLifetime;

        public void InitFrom(PlayerDefinition player, WeaponDefinition weapon)
        {
            if (player != null)
            {
                MoveSpeed = player.MoveSpeed;
                MaxHealth = player.MaxHealth;
                PickupRadius = player.PickupRadius;
            }

            if (weapon != null)
            {
                WeaponDamage = weapon.Damage;
                WeaponFireInterval = weapon.FireInterval;
                WeaponPierce = weapon.Pierce;
                ProjectileSpeed = weapon.ProjectileSpeed;
                WeaponRange = weapon.Range;
                WeaponLifetime = weapon.Lifetime;
            }
        }

        public void Apply(UpgradeDefinition upgrade)
        {
            if (upgrade == null)
            {
                return;
            }

            switch (upgrade.Kind)
            {
                case UpgradeKind.MoveSpeed: MoveSpeed += upgrade.Value; break;
                case UpgradeKind.MaxHealth: MaxHealth += upgrade.Value; break;
                case UpgradeKind.PickupRadius: PickupRadius += upgrade.Value; break;
                case UpgradeKind.WeaponDamage: WeaponDamage += upgrade.Value; break;
                case UpgradeKind.WeaponFireRate: WeaponFireInterval /= 1f + Mathf.Max(0f, upgrade.Value); break;
                case UpgradeKind.WeaponPierce: WeaponPierce += Mathf.RoundToInt(upgrade.Value); break;
                case UpgradeKind.ProjectileSpeed: ProjectileSpeed += upgrade.Value; break;
            }
        }
    }
}
