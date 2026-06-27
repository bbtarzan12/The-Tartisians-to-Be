using System.Collections.Generic;
using Tartisians.Data;
using Tartisians.Gameplay.Weapons;

namespace Tartisians.Gameplay.Progression
{
    public enum OptionKind
    {
        Evolution,    // 무기 진화(우선)
        LevelWeapon,  // 보유 무기 레벨업
        NewWeapon,    // 새 무기 획득
        LevelPassive, // 보유 패시브 레벨업
        NewPassive,   // 새 패시브 획득
    }

    /// <summary>후보 1개의 데이터(문자열·적용은 상위에서). 순수 생성이라 테스트 가능.</summary>
    public struct OptionDescriptor
    {
        public OptionKind Kind;
        public WeaponDefinition Weapon;       // NewWeapon / Evolution(=진화 결과 정의)
        public PassiveItemDefinition Passive; // NewPassive / LevelPassive
        public WeaponInstance WeaponTarget;   // LevelWeapon / Evolution(=대상 인스턴스)
        public int ResultLevel;               // 적용 후 레벨(표시용)
    }

    /// <summary>
    /// 현재 빌드 + 카탈로그로부터 가능한 레벨업 후보 전체를 생성하는 순수 로직.
    /// 우선순위: 진화 → 무기 레벨업 → 새 무기 → 패시브 레벨업 → 새 패시브.
    /// (이름이 UnityEditor.BuildOptions와 겹치지 않도록 UpgradePool로 명명.)
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

            // 1) 진화(조건 충족 무기)
            for (int i = 0; i < s.Weapons.Count; i++)
            {
                WeaponInstance w = s.Weapons[i];
                if (s.CanEvolve(w))
                {
                    results.Add(new OptionDescriptor
                    {
                        Kind = OptionKind.Evolution,
                        WeaponTarget = w,
                        Weapon = w.Def.EvolvesInto,
                        ResultLevel = 1,
                    });
                }
            }

            // 2) 보유 무기 레벨업
            for (int i = 0; i < s.Weapons.Count; i++)
            {
                WeaponInstance w = s.Weapons[i];
                if (!w.IsMaxLevel)
                {
                    results.Add(new OptionDescriptor
                    {
                        Kind = OptionKind.LevelWeapon,
                        WeaponTarget = w,
                        Weapon = w.Def,
                        ResultLevel = w.Level + 1,
                    });
                }
            }

            // 3) 새 무기(여유 시)
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

            // 4) 보유 패시브 레벨업
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

            // 5) 새 패시브(여유 시)
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
