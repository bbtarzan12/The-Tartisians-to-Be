using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Gameplay.Enemies;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class EnemySteeringTests
    {
        [Test]
        public void ComputeDelta_MovesTowardTarget()
        {
            Vector3 self = Vector3.zero;
            Vector3 target = new Vector3(10f, 0f, 0f);

            Vector3 delta = EnemySteering.ComputeDelta(self, target, Vector3.zero, 3f, 1f, 0.02f);

            Assert.Greater(delta.x, 0f, "타깃(+X) 방향으로 이동해야 한다.");
            Assert.AreEqual(3f * 0.02f, delta.magnitude, 1e-4f, "이동량은 speed*dt 여야 한다.");
        }

        [Test]
        public void ComputeDelta_IgnoresYAxis()
        {
            Vector3 delta = EnemySteering.ComputeDelta(Vector3.zero, new Vector3(0f, 100f, 5f), Vector3.zero, 3f, 1f, 0.02f);
            Assert.AreEqual(0f, delta.y, 1e-5f, "이동은 XZ 평면에만 일어나야 한다.");
        }

        [Test]
        public void Separation_PushesAwayFromCloseNeighbor()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),   // 0: self
                new Vector3(0.3f, 0f, 0f), // 1: 오른쪽에 가까이
            };
            var indices = new List<int> { 0, 1 };

            Vector3 sep = EnemySteering.Separation(Vector3.zero, positions, indices, 1f);

            Assert.Less(sep.x, 0f, "오른쪽 이웃으로부터 왼쪽(-X)으로 밀려나야 한다.");
        }

        [Test]
        public void Separation_NoNeighborsInRange_ReturnsZero()
        {
            var positions = new List<Vector3> { Vector3.zero, new Vector3(5f, 0f, 0f) };
            var indices = new List<int> { 0, 1 };

            Vector3 sep = EnemySteering.Separation(Vector3.zero, positions, indices, 1f);

            Assert.AreEqual(Vector3.zero, sep);
        }
    }
}
