using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class TargetingTests
    {
        [Test]
        public void NearestIndexInRange_PicksClosest()
        {
            var positions = new List<Vector3>
            {
                new Vector3(5f, 0f, 0f),
                new Vector3(2f, 0f, 0f), // 최근접
                new Vector3(8f, 0f, 0f),
            };

            int idx = Targeting.NearestIndexInRange(Vector3.zero, positions, 12f);
            Assert.AreEqual(1, idx);
        }

        [Test]
        public void NearestIndexInRange_NoneInRange_ReturnsMinusOne()
        {
            var positions = new List<Vector3> { new Vector3(20f, 0f, 0f) };
            int idx = Targeting.NearestIndexInRange(Vector3.zero, positions, 12f);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void NearestIndexInRange_Empty_ReturnsMinusOne()
        {
            int idx = Targeting.NearestIndexInRange(Vector3.zero, new List<Vector3>(), 12f);
            Assert.AreEqual(-1, idx);
        }

        [Test]
        public void PredictAimPoint_LeadsAheadOfMovingTarget()
        {
            // 타깃 (10,0,0)에서 +z로 5/s 이동, 투사체 20/s → 도달 0.5s, 선행 0.5*5=2.5
            Vector3 aim = Targeting.PredictAimPoint(Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(0f, 0f, 5f), 20f, 1f);
            Assert.AreEqual(10f, aim.x, 1e-3f);
            Assert.AreEqual(2.5f, aim.z, 1e-3f, "이동 방향(+z)으로 앞서 겨냥해야 한다.");
        }

        [Test]
        public void PredictAimPoint_StationaryTarget_AimsAtTarget()
        {
            Vector3 target = new Vector3(8f, 0f, 0f);
            Vector3 aim = Targeting.PredictAimPoint(Vector3.zero, target, Vector3.zero, 20f, 1f);
            Assert.AreEqual(target, aim);
        }

        [Test]
        public void PredictAimPoint_ZeroLead_AimsAtTarget()
        {
            Vector3 target = new Vector3(8f, 0f, 3f);
            Vector3 aim = Targeting.PredictAimPoint(Vector3.zero, target, new Vector3(0f, 0f, 9f), 20f, 0f);
            Assert.AreEqual(target, aim);
        }
    }
}
