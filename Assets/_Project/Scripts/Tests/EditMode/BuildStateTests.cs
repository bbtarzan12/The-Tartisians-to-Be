using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Progression;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class BuildStateTests
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        static WeaponDefinition Weapon(params WeaponDefinition.WeaponTrait[] traits)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            typeof(WeaponDefinition).GetField("_damage", BF).SetValue(w, 5f);
            typeof(WeaponDefinition).GetField("_traits", BF).SetValue(w, traits);
            return w;
        }

        static WeaponDefinition.WeaponTrait T(TraitKind kind, int max = 5, float step = 1f)
            => new WeaponDefinition.WeaponTrait { Kind = kind, MaxLevel = max, Step = step };

        static PassiveItemDefinition Passive(PassiveKind kind = PassiveKind.MoveSpeed, float perLevel = 1f, int maxLevel = 5)
        {
            var p = ScriptableObject.CreateInstance<PassiveItemDefinition>();
            typeof(PassiveItemDefinition).GetField("_kind", BF).SetValue(p, kind);
            typeof(PassiveItemDefinition).GetField("_valuePerLevel", BF).SetValue(p, perLevel);
            typeof(PassiveItemDefinition).GetField("_maxLevel", BF).SetValue(p, maxLevel);
            return p;
        }

        static int CountKind(List<OptionDescriptor> list, OptionKind kind)
        {
            int n = 0;
            foreach (OptionDescriptor o in list)
            {
                if (o.Kind == kind) n++;
            }
            return n;
        }

        [Test]
        public void AddWeapon_RespectsCapAndDedup()
        {
            var a = Weapon(T(TraitKind.Damage));
            var b = Weapon(T(TraitKind.Damage));
            var c = Weapon(T(TraitKind.Damage));
            var s = new BuildState { MaxWeapons = 2 };
            Assert.IsNotNull(s.AddWeapon(a));
            Assert.AreSame(s.AddWeapon(a), s.FindWeapon(a)); // 중복 → 기존 반환
            Assert.IsNotNull(s.AddWeapon(b));
            Assert.IsNull(s.AddWeapon(c), "cap 초과 추가는 null");
            Assert.AreEqual(2, s.Weapons.Count);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c);
        }

        [Test]
        public void Generate_EmptyBuild_OffersNewWeaponsAndPassives()
        {
            var w1 = Weapon(T(TraitKind.Damage));
            var w2 = Weapon(T(TraitKind.Damage));
            var p1 = Passive();
            var s = new BuildState();
            var weapons = new List<WeaponDefinition> { w1, w2 };
            var passives = new List<PassiveItemDefinition> { p1 };
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, weapons, passives, results);
            Assert.AreEqual(2, CountKind(results, OptionKind.NewWeapon));
            Assert.AreEqual(1, CountKind(results, OptionKind.NewPassive));
            Assert.AreEqual(0, CountKind(results, OptionKind.UpgradeTrait));
            Object.DestroyImmediate(w1); Object.DestroyImmediate(w2); Object.DestroyImmediate(p1);
        }

        [Test]
        public void Generate_OwnedWeapon_OffersTraitPerSupported_NotDuplicateNew()
        {
            var a = Weapon(T(TraitKind.Damage), T(TraitKind.Amount), T(TraitKind.Cooldown)); // 3 특성
            var b = Weapon(T(TraitKind.Damage));
            var s = new BuildState();
            s.AddWeapon(a);
            var weapons = new List<WeaponDefinition> { a, b };
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, weapons, null, results);
            Assert.AreEqual(3, CountKind(results, OptionKind.UpgradeTrait), "보유 a의 지원 특성 3개");
            Assert.AreEqual(1, CountKind(results, OptionKind.NewWeapon), "미보유 b만 새 무기");
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void Generate_MaxedTrait_NotOffered()
        {
            var a = Weapon(T(TraitKind.Damage, max: 1)); // 1번 강화하면 만렙
            var s = new BuildState();
            var w = s.AddWeapon(a);
            w.UpgradeTrait(TraitKind.Damage);
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, null, null, results);
            Assert.AreEqual(0, CountKind(results, OptionKind.UpgradeTrait));
            Object.DestroyImmediate(a);
        }

        [Test]
        public void Generate_TraitResultLevel_IsNextLevel()
        {
            var a = Weapon(T(TraitKind.Damage, max: 3));
            var s = new BuildState();
            var w = s.AddWeapon(a);
            w.UpgradeTrait(TraitKind.Damage); // 현재 1
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, null, null, results);
            OptionDescriptor d = results.Find(o => o.Kind == OptionKind.UpgradeTrait);
            Assert.AreEqual(TraitKind.Damage, d.Trait);
            Assert.AreEqual(2, d.ResultLevel, "다음 단계는 2");
            Object.DestroyImmediate(a);
        }

        [Test]
        public void Generate_OwnedPassive_OffersLevelUp()
        {
            var p = Passive(PassiveKind.MoveSpeed, 1f, maxLevel: 3);
            var s = new BuildState();
            s.AddPassive(p);
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, null, new List<PassiveItemDefinition> { p }, results);
            Assert.AreEqual(1, CountKind(results, OptionKind.LevelPassive));
            Assert.AreEqual(0, CountKind(results, OptionKind.NewPassive), "보유 패시브는 신규 후보 아님");
            Object.DestroyImmediate(p);
        }
    }
}
