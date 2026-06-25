using NUnit.Framework;
using Tartisians.Gameplay.Player;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class PlayerMovementTests
    {
        const float Speed = 5f;
        const float Dt = 0.02f;

        [Test]
        public void ComputeDelta_ZeroInput_ReturnsZero()
        {
            Vector3 delta = PlayerMovement.ComputeDelta(Vector2.zero, Speed, Dt);
            Assert.AreEqual(Vector3.zero, delta);
        }

        [Test]
        public void ComputeDelta_MapsInputToXZPlane()
        {
            Vector3 delta = PlayerMovement.ComputeDelta(new Vector2(1f, 0f), Speed, Dt);
            Assert.AreEqual(Speed * Dt, delta.x, 1e-5f);
            Assert.AreEqual(0f, delta.y, 1e-5f);
            Assert.AreEqual(0f, delta.z, 1e-5f);
        }

        [Test]
        public void ComputeDelta_DiagonalInput_IsNormalized()
        {
            Vector3 delta = PlayerMovement.ComputeDelta(new Vector2(1f, 1f), Speed, Dt);
            Assert.AreEqual(Speed * Dt, delta.magnitude, 1e-4f, "대각 입력이 직선 입력보다 빠르면 안 된다.");
        }

        [Test]
        public void ComputeDelta_PartialInput_NotForcedToFullSpeed()
        {
            Vector3 delta = PlayerMovement.ComputeDelta(new Vector2(0.5f, 0f), Speed, Dt);
            Assert.AreEqual(0.5f * Speed * Dt, delta.magnitude, 1e-5f, "작은 입력(아날로그 스틱)은 비례해야 한다.");
        }
    }
}
