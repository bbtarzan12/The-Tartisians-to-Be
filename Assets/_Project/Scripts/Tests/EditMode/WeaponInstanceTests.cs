using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Progression;
using Tartisians.Gameplay.Weapons;
using UnityEditor;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class WeaponInstanceTests
    {
        static WeaponDefinition MakeWeapon(int maxLevel, float damage, float dmgPerLevel,
            int amount = 1, float amountPerLevel = 0f, float fireInterval = 1f, float fireRateReducePerLevel = 0f)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            var so = new SerializedObject(w);
            so.FindProperty("_maxLevel").intValue = maxLevel;
            so.FindProperty("_damage").floatValue = damage;
            so.FindProperty("_damagePerLevel").floatValue = dmgPerLevel;
            so.FindProperty("_amount").intValue = amount;
            so.FindProperty("_amountPerLevel").floatValue = amountPerLevel;
            so.FindProperty("_fireInterval").floatValue = fireInterval;
            so.FindProperty("_fireRateReducePerLevel").floatValue = fireRateReducePerLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return w;
        }

        [Test]
        public void Compute_Level1_NoMods_UsesBase()
        {
            var w = MakeWeapon(8, 5f, 1f);
            var s = new WeaponInstance(w).Compute(PassiveModifiers.None);
            Assert.AreEqual(5f, s.Damage, 1e-4f);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_Level3_AddsGrowth()
        {
            var w = MakeWeapon(8, 5f, 1f);
            var inst = new WeaponInstance(w);
            inst.LevelUp();
            inst.LevelUp(); // L3
            Assert.AreEqual(3, inst.Level);
            Assert.AreEqual(7f, inst.Compute(PassiveModifiers.None).Damage, 1e-4f); // 5 + 1*2
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_MightScalesDamage()
        {
            var w = MakeWeapon(8, 10f, 0f);
            var s = new WeaponInstance(w).Compute(new PassiveModifiers { MightPct = 0.5f });
            Assert.AreEqual(15f, s.Damage, 1e-4f);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_AmountFloorPlusPassive()
        {
            var w = MakeWeapon(8, 5f, 0f, amount: 1, amountPerLevel: 0.5f);
            var inst = new WeaponInstance(w);
            inst.LevelUp();
            inst.LevelUp(); // L3 → floor(0.5*2)=1
            int amount = inst.Compute(new PassiveModifiers { AmountAdd = 2 }).Amount;
            Assert.AreEqual(1 + 1 + 2, amount); // base1 + growth1 + passive2
            Object.DestroyImmediate(w);
        }

        [Test]
        public void Compute_CooldownReducesInterval()
        {
            var w = MakeWeapon(8, 5f, 0f, fireInterval: 1f);
            var s = new WeaponInstance(w).Compute(new PassiveModifiers { CooldownPct = 1f }); // /2
            Assert.AreEqual(0.5f, s.FireInterval, 1e-4f);
            Object.DestroyImmediate(w);
        }

        [Test]
        public void LevelUp_ClampsAtMax()
        {
            var w = MakeWeapon(2, 5f, 1f);
            var inst = new WeaponInstance(w);
            Assert.IsTrue(inst.LevelUp());  // L2
            Assert.IsFalse(inst.LevelUp()); // clamp
            Assert.AreEqual(2, inst.Level);
            Assert.IsTrue(inst.IsMaxLevel);
            Object.DestroyImmediate(w);
        }
    }
}
