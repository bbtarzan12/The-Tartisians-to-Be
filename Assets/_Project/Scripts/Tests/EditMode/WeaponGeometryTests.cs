using NUnit.Framework;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class WeaponGeometryTests
    {
        static readonly Vector3 Origin = Vector3.zero;
        static readonly Vector3 Dir = Vector3.forward; // (0,0,1)

        [Test]
        public void PointOnAxis_WithinLength_True()
        {
            Assert.IsTrue(WeaponGeometry.PointInLane(Origin, Dir, 10f, 1f, new Vector3(0, 0, 5)));
        }

        [Test]
        public void PointBeyondLength_False()
        {
            Assert.IsFalse(WeaponGeometry.PointInLane(Origin, Dir, 10f, 1f, new Vector3(0, 0, 11)));
        }

        [Test]
        public void PointBehindOrigin_False()
        {
            Assert.IsFalse(WeaponGeometry.PointInLane(Origin, Dir, 10f, 1f, new Vector3(0, 0, -1)));
        }

        [Test]
        public void PointOutsideHalfWidth_False()
        {
            Assert.IsFalse(WeaponGeometry.PointInLane(Origin, Dir, 10f, 1f, new Vector3(2, 0, 5)));
        }

        [Test]
        public void PointInsideHalfWidth_True()
        {
            Assert.IsTrue(WeaponGeometry.PointInLane(Origin, Dir, 10f, 1f, new Vector3(0.5f, 0, 5)));
        }

        [Test]
        public void IgnoresY()
        {
            Assert.IsTrue(WeaponGeometry.PointInLane(Origin, Dir, 10f, 1f, new Vector3(0, 9f, 5)));
        }
    }
}
