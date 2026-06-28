using System.Collections.Generic;
using Tartisians.Data;
using Tartisians.Gameplay.Weapons;

namespace Tartisians.Gameplay.Progression
{
    public enum OptionKind
    {
        UpgradeTrait, // 보유 무기의 특정 특성 강화
        NewWeapon,    // 새 무기 획득
        LevelPassive, // 보유 플레이어 강화 레벨업
        NewPassive,   // 새 플레이어 강화 획득
    }

    /// <summary>후보 1개의 데이터(문자열·적용은 상위에서). 순수 생성이라 테스트 가능.</summary>
    public struct OptionDescriptor
    {
        public OptionKind Kind;
        public WeaponDefinition Weapon;       // UpgradeTrait(대상 무기 정의) / NewWeapon
        public WeaponInstance WeaponTarget;   // UpgradeTrait(대상 인스턴스)
        public TraitKind Trait;               // UpgradeTrait(어떤 특성)
        public PassiveItemDefinition Passive; // NewPassive / LevelPassive
        public int ResultLevel;               // 적용 후 레벨(표시용)
    }

    /// <summary>
    /// 현재 빌드 + 카탈로그로부터 가능한 레벨업 후보 전체를 생성하는 순수 로직.
    /// 무기 강화는 '무기 × 지원 특성(미만렙)' 단위로 쪼개져 나온다(같은 무기의 여러 특성이 동시에 후보가 될 수 있음).
    /// 진화는 이번 설계에서 제외.
    /// </summary>
    public static class UpgradePool
    {
        public static void Generate(
            BuildState s,
            IReadOnlyList<WeaponDefinition> weaponCatalog,
            IReadOnlyList<PassiveItemDefinition> passiveCatalog,
            List<OptionDescriptor> results)
        {
            results.Clear();
            if (s == null)
            {
                return;
            }

            // 1) 보유 무기의 특성 강화(무기 × 미만렙 특성)
            for (int i = 0; i < s.Weapons.Count; i++)
            {
                WeaponInstance w = s.Weapons[i];
                IReadOnlyList<WeaponDefinition.WeaponTrait> traits = w.Def != null ? w.Def.Traits : null;
                if (traits == null)
                {
                    continue;
                }

                for (int t = 0; t < traits.Count; t++)
                {
                    TraitKind kind = traits[t].Kind;
                    if (w.CanUpgrade(kind))
                    {
                        results.Add(new OptionDescriptor
                        {
                            Kind = OptionKind.UpgradeTrait,
                            Weapon = w.Def,
                            WeaponTarget = w,
                            Trait = kind,
                            ResultLevel = w.TraitLevel(kind) + 1,
                        });
                    }
                }
            }

            // 2) 새 무기(여유 시)
            if (!s.WeaponsFull && weaponCatalog != null)
            {
                for (int i = 0; i < weaponCatalog.Count; i++)
                {
                    WeaponDefinition d = weaponCatalog[i];
                    if (d != null && !s.HasWeapon(d))
                    {
                        results.Add(new OptionDescriptor
                        {
                            Kind = OptionKind.NewWeapon,
                            Weapon = d,
                            ResultLevel = 1,
                        });
                    }
                }
            }

            // 3) 보유 플레이어 강화 레벨업
            for (int i = 0; i < s.Passives.Count; i++)
            {
                PassiveOwned p = s.Passives[i];
                if (!p.IsMaxLevel)
                {
                    results.Add(new OptionDescriptor
                    {
                        Kind = OptionKind.LevelPassive,
                        Passive = p.Def,
                        ResultLevel = p.Level + 1,
                    });
                }
            }

            // 4) 새 플레이어 강화(여유 시)
            if (!s.PassivesFull && passiveCatalog != null)
            {
                for (int i = 0; i < passiveCatalog.Count; i++)
                {
                    PassiveItemDefinition d = passiveCatalog[i];
                    if (d != null && !s.HasPassive(d))
                    {
                        results.Add(new OptionDescriptor
                        {
                            Kind = OptionKind.NewPassive,
                            Passive = d,
                            ResultLevel = 1,
                        });
                    }
                }
            }
        }
    }
}
