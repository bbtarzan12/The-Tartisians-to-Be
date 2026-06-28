using System.Reflection;
using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class WeaponInstanceTests
    {
        static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic;

        static WeaponDefinition.WeaponTrait T(TraitKind kind, int max, float step)
            => new WeaponDefinition.WeaponTrait { Kind = kind, MaxLevel = max, Step = step };

        static WeaponDefinition MakeWeapon(
            float damage = 5f, int amount = 1, float fireInterval = 1f,
            params WeaponDefinition.WeaponTrait[] traits)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            var t = typeof(WeaponDefinition);
            t.GetField("_damage", BF).SetValue(w, damage);
            t.GetField("_amount", BF).SetValue(w, amount);
            t.GetField("_fireInterval", BF).SetValue(w, fireInterval);
            t.GetField("_traits", BF).SetValue(w, traits);
            return w;
        }

        [Test]
        public void Compute_NoUpgrades_UsesBase()
        {
            var w = MakeWeapon(damage: 5f, traits: T(TraitKind.Damage, 5, 1f));
            var s = new WeaponInstance(w).Compute();
            Assert.AreEqual(5f, s.Damage, 1e-4f);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_DamageTrait_AddsStepPerLevel()
        {
            var w = MakeWeapon(damage: 5f, traits: T(TraitKind.Damage, 5, 1.5f));
            var inst = new WeaponInstance(w);
            inst.UpgradeTrait(TraitKind.Damage);
            inst.UpgradeTrait(TraitKind.Damage); // Lv2
            Assert.AreEqual(2, inst.TraitLevel(TraitKind.Damage));
            Assert.AreEqual(5f + 1.5f * 2f, inst.Compute().Damage, 1e-4f);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_AmountTrait_AddsIntegerPerLevel()
        {
            var w = MakeWeapon(amount: 1, traits: T(TraitKind.Amount, 3, 1f));
            var inst = new WeaponInstance(w);
            inst.UpgradeTrait(TraitKind.Amount);
            inst.UpgradeTrait(TraitKind.Amount);
            Assert.AreEqual(1 + 2, inst.Compute().Amount); // base1 + 2
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_CooldownTrait_ReducesInterval()
        {
            var w = MakeWeapon(fireInterval: 1f, traits: T(TraitKind.Cooldown, 5, 1f));
            var inst = new WeaponInstance(w);
            inst.UpgradeTrait(TraitKind.Cooldown); // /(1+1*1)=/2
            Assert.AreEqual(0.5f, inst.Compute().FireInterval, 1e-4f);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Upgrade_ClampsAtTraitMax()
        {
            var w = MakeWeapon(traits: T(TraitKind.Damage, 2, 1f));
            var inst = new WeaponInstance(w);
            Assert.IsTrue(inst.UpgradeTrait(TraitKind.Damage));  // 1
            Assert.IsTrue(inst.UpgradeTrait(TraitKind.Damage));  // 2 (max)
            Assert.IsFalse(inst.UpgradeTrait(TraitKind.Damage)); // clamp
            Assert.AreEqual(2, inst.TraitLevel(TraitKind.Damage));
            Assert.IsFalse(inst.CanUpgrade(TraitKind.Damage));
            Object.DestroyImmediate(w);
        }

        [Test]
        public void UnsupportedTrait_CannotUpgrade_LevelZero()
        {
            var w = MakeWeapon(traits: T(TraitKind.Damage, 5, 1f)); // 범위 미지원
            var inst = new WeaponInstance(w);
            Assert.IsFalse(inst.CanUpgrade(TraitKind.Area));
            Assert.IsFalse(inst.UpgradeTrait(TraitKind.Area));
            Assert.AreEqual(0, inst.TraitLevel(TraitKind.Area));
            Object.DestroyImmediate(w);
        }

        [Test]
        public void TotalUpgrades_SumsAllTraitLevels()
        {
            var w = MakeWeapon(traits: new[] { T(TraitKind.Damage, 5, 1f), T(TraitKind.Amount, 3, 1f) });
            var inst = new WeaponInstance(w);
            inst.UpgradeTrait(TraitKind.Damage);
            inst.UpgradeTrait(TraitKind.Damage);
            inst.UpgradeTrait(TraitKind.Amount);
            Assert.AreEqual(3, inst.TotalUpgrades);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void HasUpgradable_FalseWhenAllMaxed()
        {
            var w = MakeWeapon(traits: T(TraitKind.Damage, 1, 1f));
            var inst = new WeaponInstance(w);
            Assert.IsTrue(inst.HasUpgradable);
            inst.UpgradeTrait(TraitKind.Damage);
            Assert.IsFalse(inst.HasUpgradable);
            Object.DestroyImmediate(w);
        }
    }
}
