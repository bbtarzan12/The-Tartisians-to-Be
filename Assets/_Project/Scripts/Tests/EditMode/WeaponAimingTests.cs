using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class WeaponAimingTests
    {
        static readonly Vector3 Origin = Vector3.zero;

        [Test]
        public void BestLane_PicksDirectionCoveringMostEnemies()
        {
            // +Z축에 3마리 일렬, +X축에 1마리. 최다적중은 +Z 라인.
            var pts = new List<Vector3>
            {
                new Vector3(0, 0, 3),
                new Vector3(0, 0, 6),
                new Vector3(0, 0, 9),
                new Vector3(8, 0, 0),
            };

            int idx = WeaponAiming.BestLaneIndex(Origin, pts, length: 20f, halfWidth: 1f);
            Vector3 dir = (pts[idx] - Origin).normalized;
            Assert.Greater(dir.z, 0.9f, "+Z 방향(일렬 3마리)을 골라야 한다.");
        }

        [Test]
        public void BestLane_Empty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, WeaponAiming.BestLaneIndex(Origin, new List<Vector3>(), 10f, 1f));
        }

        [Test]
        public void BestLane_SingleEnemy_PicksIt()
        {
            var pts = new List<Vector3> { new Vector3(0, 0, 5) };
            Assert.AreEqual(0, WeaponAiming.BestLaneIndex(Origin, pts, 10f, 1f));
        }

        [Test]
        public void DensestCluster_PicksEnemyInTightGroup()
        {
            // 0~2번이 (10,*,0) 근처에 밀집, 3번은 멀리 외톨이.
            var pts = new List<Vector3>
            {
                new Vector3(10, 0, 0),
                new Vector3(10.5f, 0, 0.4f),
                new Vector3(9.6f, 0, 0.3f),
                new Vector3(-20, 0, -20),
            };

            int idx = WeaponAiming.DensestClusterIndex(Origin, pts, radius: 2f);
            Assert.Contains(idx, new[] { 0, 1, 2 }, "밀집 그룹 내 적을 골라야 한다.");
        }

        [Test]
        public void DensestCluster_Empty_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, WeaponAiming.DensestClusterIndex(Origin, new List<Vector3>(), 2f));
        }

        [Test]
        public void DensestCluster_TieBreaksToNearer()
        {
            // 두 외톨이(반경 내 자기 자신만, count=1 동수) → origin에 더 가까운 0번.
            var pts = new List<Vector3>
            {
                new Vector3(3, 0, 0),
                new Vector3(30, 0, 0),
            };

            int idx = WeaponAiming.DensestClusterIndex(Origin, pts, radius: 1f);
            Assert.AreEqual(0, idx);
        }
    }
}
