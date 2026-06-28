using Tartisians.Data;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>특정 특성 레벨에서 계산된 무기의 유효 스탯(한 발사 시점의 실제 값).</summary>
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
    /// 보유 무기 1개의 런타임 상태(정의 참조 + 특성별 레벨 + 발사 타이머).
    /// 단일 레벨이 아니라 특성별로 독립 강화한다. 유효 스탯 = 기본값 + Step×특성레벨.
    /// 전역 패시브 수정자는 더 이상 무기 스탯에 관여하지 않는다(무기 비종속 패시브만 RunStats로).
    /// 순수 계산이라 단위 테스트 가능.
    /// </summary>
    public sealed class WeaponInstance
    {
        public WeaponDefinition Def { get; }
        readonly int[] _trait = new int[TraitKinds.Count]; // TraitKind 인덱스별 강화 레벨
        public float FireTimer; // 인벤토리가 굴리는 발사 누적 타이머(런타임 전용)

        public WeaponInstance(WeaponDefinition def)
        {
            Def = def;
        }

        /// <summary>해당 특성의 현재 강화 레벨(미지원이면 0).</summary>
        public int TraitLevel(TraitKind kind)
            => Def != null && Def.SupportsTrait(kind) ? _trait[(int)kind] : 0;

        /// <summary>이 특성을 더 올릴 수 있는가(지원 + 상한 미만).</summary>
        public bool CanUpgrade(TraitKind kind)
            => Def != null && Def.SupportsTrait(kind) && _trait[(int)kind] < Def.TraitMax(kind);

        /// <summary>해당 특성을 1단계 강화. 불가면 false.</summary>
        public bool UpgradeTrait(TraitKind kind)
        {
            if (!CanUpgrade(kind))
            {
                return false;
            }

            _trait[(int)kind]++;
            return true;
        }

        /// <summary>모든 특성 강화 레벨의 합(HUD 보유현황 표시용).</summary>
        public int TotalUpgrades
        {
            get
            {
                int sum = 0;
                for (int i = 0; i < _trait.Length; i++)
                {
                    sum += _trait[i];
                }

                return sum;
            }
        }

        /// <summary>아직 올릴 수 있는 특성이 하나라도 있는가.</summary>
        public bool HasUpgradable
        {
            get
            {
                if (Def == null)
                {
                    return false;
                }

                var traits = Def.Traits;
                if (traits == null)
                {
                    return false;
                }

                for (int i = 0; i < traits.Count; i++)
                {
                    if (CanUpgrade(traits[i].Kind))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public EffectiveWeaponStats Compute()
        {
            float damage = Def.Damage + Def.TraitStep(TraitKind.Damage) * TraitLevel(TraitKind.Damage);
            float cdReduce = Def.TraitStep(TraitKind.Cooldown) * TraitLevel(TraitKind.Cooldown);
            float interval = Def.FireInterval / (1f + cdReduce);
            float speed = Def.ProjectileSpeed + Def.TraitStep(TraitKind.ProjectileSpeed) * TraitLevel(TraitKind.ProjectileSpeed);
            int pierce = Def.Pierce + Mathf.RoundToInt(Def.TraitStep(TraitKind.Pierce) * TraitLevel(TraitKind.Pierce));
            int amount = Def.Amount + Mathf.RoundToInt(Def.TraitStep(TraitKind.Amount) * TraitLevel(TraitKind.Amount));
            float area = Def.Area + Def.TraitStep(TraitKind.Area) * TraitLevel(TraitKind.Area);

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
