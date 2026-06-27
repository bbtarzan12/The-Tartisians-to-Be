using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Progression;
using Tartisians.Gameplay.Weapons;
using UnityEditor;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class BuildStateTests
    {
        static WeaponDefinition Weapon(int maxLevel = 8, WeaponDefinition evolvesInto = null, PassiveItemDefinition requiredPassive = null)
        {
            var w = ScriptableObject.CreateInstance<WeaponDefinition>();
            var so = new SerializedObject(w);
            so.FindProperty("_maxLevel").intValue = maxLevel;
            so.FindProperty("_damage").floatValue = 5f;
            if (evolvesInto != null) so.FindProperty("_evolvesInto").objectReferenceValue = evolvesInto;
            if (requiredPassive != null) so.FindProperty("_requiredPassive").objectReferenceValue = requiredPassive;
            so.ApplyModifiedPropertiesWithoutUndo();
            return w;
        }

        static PassiveItemDefinition Passive(PassiveKind kind = PassiveKind.Might, float perLevel = 0.1f, int maxLevel = 5)
        {
            var p = ScriptableObject.CreateInstance<PassiveItemDefinition>();
            var so = new SerializedObject(p);
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_valuePerLevel").floatValue = perLevel;
            so.FindProperty("_maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
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
            var a = Weapon();
            var b = Weapon();
            var c = Weapon();
            var s = new BuildState { MaxWeapons = 2 };
            Assert.IsNotNull(s.AddWeapon(a));
            Assert.AreSame(s.AddWeapon(a), s.FindWeapon(a)); // 중복 → 기존 반환
            Assert.IsNotNull(s.AddWeapon(b));
            Assert.IsNull(s.AddWeapon(c), "cap 초과 추가는 null");
            Assert.AreEqual(2, s.Weapons.Count);
            Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(c);
        }

        [Test]
        public void ComputeModifiers_AggregatesPassives()
        {
            var might = Passive(PassiveKind.Might, 0.1f);
            var amount = Passive(PassiveKind.Amount, 1f);
            var s = new BuildState();
            s.AddPassive(might);  // L1 → +0.1
            s.AddPassive(amount); // L1 → +1
            PassiveModifiers m = s.ComputeModifiers();
            Assert.AreEqual(0.1f, m.MightPct, 1e-4f);
            Assert.AreEqual(1, m.AmountAdd);
            Object.DestroyImmediate(might); Object.DestroyImmediate(amount);
        }

        [Test]
        public void Generate_EmptyBuild_OffersNewWeaponsAndPassives()
        {
            var w1 = Weapon();
            var w2 = Weapon();
            var p1 = Passive();
            var s = new BuildState();
            var weapons = new List<WeaponDefinition> { w1, w2 };
            var passives = new List<PassiveItemDefinition> { p1 };
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, weapons, passives, results);
            Assert.AreEqual(2, CountKind(results, OptionKind.NewWeapon));
            Assert.AreEqual(1, CountKind(results, OptionKind.NewPassive));
            Assert.AreEqual(0, CountKind(results, OptionKind.LevelWeapon));
            Object.DestroyImmediate(w1); Object.DestroyImmediate(w2); Object.DestroyImmediate(p1);
        }

        [Test]
        public void Generate_OwnedWeapon_OffersLevelUp_NotDuplicateNew()
        {
            var a = Weapon();
            var b = Weapon();
            var s = new BuildState();
            s.AddWeapon(a);
            var weapons = new List<WeaponDefinition> { a, b };
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, weapons, null, results);
            Assert.AreEqual(1, CountKind(results, OptionKind.LevelWeapon), "보유 a는 레벨업으로");
            Assert.AreEqual(1, CountKind(results, OptionKind.NewWeapon), "미보유 b만 새 무기");
            Object.DestroyImmediate(a); Object.DestroyImmediate(b);
        }

        [Test]
        public void Generate_MaxLevelWeapon_NoLevelUp()
        {
            var a = Weapon(maxLevel: 1); // 즉시 만렙
            var s = new BuildState();
            s.AddWeapon(a);
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, null, null, results);
            Assert.AreEqual(0, CountKind(results, OptionKind.LevelWeapon));
            Object.DestroyImmediate(a);
        }

        [Test]
        public void Evolution_GatedByMaxLevelAndPassive_ThenReplaces()
        {
            var evo = Weapon(maxLevel: 1);
            var req = Passive(PassiveKind.Amount, 1f, maxLevel: 3);
            var baseW = Weapon(maxLevel: 2, evolvesInto: evo, requiredPassive: req);
            var s = new BuildState();
            var w = s.AddWeapon(baseW);

            Assert.IsFalse(s.CanEvolve(w), "만렙 아님");
            w.LevelUp(); // L2 = max
            Assert.IsFalse(s.CanEvolve(w), "요구 패시브 없음");
            var p = s.AddPassive(req);
            Assert.IsFalse(s.CanEvolve(w), "패시브 만렙 아님");
            while (p.LevelUp()) { }
            Assert.IsTrue(s.CanEvolve(w), "조건 충족");

            // 후보에도 진화 등장
            var results = new List<OptionDescriptor>();
            UpgradePool.Generate(s, null, null, results);
            Assert.AreEqual(1, CountKind(results, OptionKind.Evolution));

            var evolved = s.Evolve(w);
            Assert.IsNotNull(evolved);
            Assert.AreEqual(evo, evolved.Def);
            Assert.IsTrue(s.HasWeapon(evo));
            Assert.IsFalse(s.HasWeapon(baseW), "기본 무기는 교체됨");

            Object.DestroyImmediate(evo); Object.DestroyImmediate(req); Object.DestroyImmediate(baseW);
        }
    }
}
