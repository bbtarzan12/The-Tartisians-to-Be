using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Gameplay.Enemies;

namespace Tartisians.Tests.EditMode
{
    public class SpawnScheduleTests
    {
        static readonly float[] Starts = { 0f, 45f, 90f, 150f, 210f, 270f };

        [Test]
        public void PhaseIndex_Empty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, SpawnSchedule.PhaseIndexAt(new float[0], 10f));
        }

        [Test]
        public void PhaseIndex_BeforeFirst_ReturnsZero()
        {
            Assert.AreEqual(0, SpawnSchedule.PhaseIndexAt(Starts, -5f));
        }

        [Test]
        public void PhaseIndex_OnBoundary_PicksThatPhase()
        {
            Assert.AreEqual(2, SpawnSchedule.PhaseIndexAt(Starts, 90f));
        }

        [Test]
        public void PhaseIndex_BetweenBoundaries_PicksLowerPhase()
        {
            Assert.AreEqual(3, SpawnSchedule.PhaseIndexAt(Starts, 200f));
        }

        [Test]
        public void PhaseIndex_PastLast_ReturnsLast()
        {
            Assert.AreEqual(5, SpawnSchedule.PhaseIndexAt(Starts, 9999f));
        }

        [Test]
        public void Weighted_AllZero_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, SpawnSchedule.WeightedIndex(new[] { 0f, 0f, 0f }, 0.5f));
        }

        [Test]
        public void Weighted_Empty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, SpawnSchedule.WeightedIndex(new float[0], 0.5f));
        }

        [Test]
        public void Weighted_RollZero_PicksFirstPositive()
        {
            // 첫 항목 0가중치 → roll 0이면 두 번째(양수 첫 항목).
            Assert.AreEqual(1, SpawnSchedule.WeightedIndex(new[] { 0f, 1f, 1f }, 0f));
        }

        [Test]
        public void Weighted_RollNearOne_PicksLastPositive()
        {
            Assert.AreEqual(2, SpawnSchedule.WeightedIndex(new[] { 1f, 1f, 1f }, 0.999f));
        }

        [Test]
        public void Weighted_RespectsCumulativeBoundaries()
        {
            // 가중치 [0.7, 0.3]: 누적경계 0.7. roll 0.5 → 0번, roll 0.8 → 1번.
            var w = new List<float> { 0.7f, 0.3f };
            Assert.AreEqual(0, SpawnSchedule.WeightedIndex(w, 0.5f));
            Assert.AreEqual(1, SpawnSchedule.WeightedIndex(w, 0.8f));
        }

        [Test]
        public void Weighted_SkipsZeroWeightInMiddle()
        {
            // [0.5, 0, 0.5]: 가운데는 절대 안 뽑힘. roll 0.6 → 2번.
            Assert.AreEqual(2, SpawnSchedule.WeightedIndex(new[] { 0.5f, 0f, 0.5f }, 0.6f));
            Assert.AreEqual(0, SpawnSchedule.WeightedIndex(new[] { 0.5f, 0f, 0.5f }, 0.4f));
        }

        [Test]
        public void Weighted_NegativeRoll_ClampsToFirst()
        {
            Assert.AreEqual(0, SpawnSchedule.WeightedIndex(new[] { 1f, 1f }, -1f));
        }
    }
}
