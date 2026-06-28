using System.Collections.Generic;
using Tartisians.Data;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>보유 패시브 1종의 런타임 상태(정의 + 현재 레벨).</summary>
    public sealed class PassiveOwned
    {
        public PassiveItemDefinition Def { get; }
        public int Level { get; private set; }

        public PassiveOwned(PassiveItemDefinition def, int level = 1)
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
    }

    /// <summary>
    /// 한 판의 빌드(보유 무기 + 패시브)를 보관·변경하는 순수 상태.
    /// 무기 강화는 각 <see cref="WeaponInstance"/>의 특성별 레벨, 패시브는 플레이어 강화(이속/체력/자석).
    /// MonoBehaviour가 아니므로 단위 테스트 가능. 후보 생성은 <see cref="UpgradePool"/> 참조.
    /// </summary>
    public sealed class BuildState
    {
        public int MaxWeapons = 6;
        public int MaxPassives = 6;

        public readonly List<WeaponInstance> Weapons = new();
        public readonly List<PassiveOwned> Passives = new();

        public bool WeaponsFull => Weapons.Count >= MaxWeapons;
        public bool PassivesFull => Passives.Count >= MaxPassives;

        public WeaponInstance FindWeapon(WeaponDefinition def)
        {
            for (int i = 0; i < Weapons.Count; i++)
            {
                if (Weapons[i].Def == def)
                {
                    return Weapons[i];
                }
            }

            return null;
        }

        public PassiveOwned FindPassive(PassiveItemDefinition def)
        {
            for (int i = 0; i < Passives.Count; i++)
            {
                if (Passives[i].Def == def)
                {
                    return Passives[i];
                }
            }

            return null;
        }

        public bool HasWeapon(WeaponDefinition def) => FindWeapon(def) != null;
        public bool HasPassive(PassiveItemDefinition def) => FindPassive(def) != null;

        /// <summary>무기를 새로 추가하거나(여유 시) 기존이면 반환. 추가 실패 시 null.</summary>
        public WeaponInstance AddWeapon(WeaponDefinition def)
        {
            if (def == null)
            {
                return null;
            }

            WeaponInstance existing = FindWeapon(def);
            if (existing != null)
            {
                return existing;
            }

            if (WeaponsFull)
            {
                return null;
            }

            var inst = new WeaponInstance(def);
            Weapons.Add(inst);
            return inst;
        }

        public PassiveOwned AddPassive(PassiveItemDefinition def)
        {
            if (def == null)
            {
                return null;
            }

            PassiveOwned existing = FindPassive(def);
            if (existing != null)
            {
                return existing;
            }

            if (PassivesFull)
            {
                return null;
            }

            var p = new PassiveOwned(def);
            Passives.Add(p);
            return p;
        }

        // 진화(Evolve/CanEvolve)와 전역 무기 수정자(ComputeModifiers)는 제거됨 —
        // 무기 강화는 WeaponInstance의 특성별 레벨로, 진화는 추후 재설계.
    }
}
