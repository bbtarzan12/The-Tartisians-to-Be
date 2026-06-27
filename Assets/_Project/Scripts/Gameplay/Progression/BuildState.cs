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
    /// 한 판의 빌드(보유 무기 + 패시브)를 보관·변경하는 순수 상태. 진화 판정과 전역 수정자 집계를 담당.
    /// MonoBehaviour가 아니므로 단위 테스트 가능. 후보 생성은 <see cref="BuildOptions"/> 참조.
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

        /// <summary>보유 패시브에서 전역 무기 수정자를 집계한다.</summary>
        public PassiveModifiers ComputeModifiers()
        {
            var m = new PassiveModifiers();
            for (int i = 0; i < Passives.Count; i++)
            {
                PassiveOwned p = Passives[i];
                float v = p.Def.ValueAtLevel(p.Level);
                switch (p.Def.Kind)
                {
                    case PassiveKind.Might: m.MightPct += v; break;
                    case PassiveKind.Cooldown: m.CooldownPct += v; break;
                    case PassiveKind.Area: m.AreaPct += v; break;
                    case PassiveKind.ProjectileSpeed: m.ProjectileSpeedPct += v; break;
                    case PassiveKind.Amount: m.AmountAdd += Mathf.RoundToInt(v); break;
                    // Magnet/MaxHealth/MoveSpeed는 플레이어 스탯(여기 미포함)
                }
            }

            return m;
        }

        /// <summary>무기가 만렙 + 진화 링크 보유 + 요구 패시브 만렙(없으면 무조건) 충족 시 true.</summary>
        public bool CanEvolve(WeaponInstance w)
        {
            if (w == null || !w.Def.CanEvolve || !w.IsMaxLevel)
            {
                return false;
            }

            PassiveItemDefinition req = w.Def.RequiredPassive;
            if (req == null)
            {
                return true;
            }

            PassiveOwned p = FindPassive(req);
            return p != null && p.IsMaxLevel;
        }

        /// <summary>기본 무기를 진화 무기로 교체하고 새 인스턴스를 반환. 조건 미충족 시 null.</summary>
        public WeaponInstance Evolve(WeaponInstance w)
        {
            if (!CanEvolve(w))
            {
                return null;
            }

            int idx = Weapons.IndexOf(w);
            if (idx < 0)
            {
                return null;
            }

            var evolved = new WeaponInstance(w.Def.EvolvesInto);
            Weapons[idx] = evolved;
            return evolved;
        }
    }
}
