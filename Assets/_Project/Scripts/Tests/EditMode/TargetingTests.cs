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
    }
}
