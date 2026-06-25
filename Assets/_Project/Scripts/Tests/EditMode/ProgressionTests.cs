using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Gameplay.Progression;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class ExperienceStateTests
    {
        [Test]
        public void AddXp_BelowThreshold_NoLevelUp()
        {
            var xp = new ExperienceState(5, 3);
            int ups = xp.AddXp(3);
            Assert.AreEqual(0, ups);
            Assert.AreEqual(1, xp.Level);
            Assert.AreEqual(3, xp.CurrentXp);
        }

        [Test]
        public void AddXp_ReachesThreshold_LevelsUp_WithCarryover()
        {
            var xp = new ExperienceState(5, 3); // L1 요구 5
            int ups = xp.AddXp(7);
            Assert.AreEqual(1, ups);
            Assert.AreEqual(2, xp.Level);
            Assert.AreEqual(2, xp.CurrentXp, "초과분 2가 이월돼야 한다.");
            Assert.AreEqual(8, xp.XpToNext, "L2 요구는 5+3=8.");
        }

        [Test]
        public void AddXp_MultiLevelInOneGo()
        {
            var xp = new ExperienceState(5, 3); // 5, 8, 11 ...
            int ups = xp.AddXp(5 + 8 + 1);
            Assert.AreEqual(2, ups);
            Assert.AreEqual(3, xp.Level);
            Assert.AreEqual(1, xp.CurrentXp);
        }
    }

    public class UpgradePickerTests
    {
        [Test]
        public void PickDistinct_ReturnsRequestedCount_NoDuplicates()
        {
            var results = new List<int>();
            UpgradePicker.PickDistinct(8, 3, max => 0, results); // 항상 0 선택 → 부분 셔플
            Assert.AreEqual(3, results.Count);
            CollectionAssert.AllItemsAreUnique(results);
        }

        [Test]
        public void PickDistinct_TakeExceedsCount_ClampsToCount()
        {
            var results = new List<int>();
            UpgradePicker.PickDistinct(2, 5, max => 0, results);
            Assert.AreEqual(2, results.Count);
            CollectionAssert.AllItemsAreUnique(results);
        }

        [Test]
        public void PickDistinct_EmptyPool_ReturnsEmpty()
        {
            var results = new List<int>();
            UpgradePicker.PickDistinct(0, 3, max => 0, results);
            Assert.AreEqual(0, results.Count);
        }
    }

    public class RunStatsTests
    {
        [Test]
        public void Apply_WeaponDamage_Increases()
        {
            var s = new RunStats { WeaponDamage = 5f };
            var up = ScriptableObject.CreateInstance<Tartisians.Data.UpgradeDefinition>();
            SetKind(up, Tartisians.Data.UpgradeKind.WeaponDamage, 3f);
            s.Apply(up);
            Assert.AreEqual(8f, s.WeaponDamage);
            Object.DestroyImmediate(up);
        }

        [Test]
        public void Apply_FireRate_ReducesInterval()
        {
            var s = new RunStats { WeaponFireInterval = 1f };
            var up = ScriptableObject.CreateInstance<Tartisians.Data.UpgradeDefinition>();
            SetKind(up, Tartisians.Data.UpgradeKind.WeaponFireRate, 1f); // +100% rate → interval /2
            s.Apply(up);
            Assert.AreEqual(0.5f, s.WeaponFireInterval, 1e-4f);
            Object.DestroyImmediate(up);
        }

        static void SetKind(Tartisians.Data.UpgradeDefinition up, Tartisians.Data.UpgradeKind kind, float value)
        {
            var so = new UnityEditor.SerializedObject(up);
            so.FindProperty("_kind").enumValueIndex = (int)kind;
            so.FindProperty("_value").floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
