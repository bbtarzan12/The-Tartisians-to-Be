using NUnit.Framework;
using Tartisians.Core.Combat;
using Tartisians.Systems.Combat;

namespace Tartisians.Tests.EditMode
{
    public class DamageSystemTests
    {
        sealed class Dummy : IDamageable
        {
            public float Received;
            public bool Dead;
            public bool IsDead => Dead;
            public void TakeDamage(float amount) => Received += amount;
        }

        [Test]
        public void Apply_DealsDamage_WhenAlive()
        {
            var d = new Dummy();
            DamageSystem.Apply(d, 7f);
            Assert.AreEqual(7f, d.Received);
        }

        [Test]
        public void Apply_Ignored_WhenDead()
        {
            var d = new Dummy { Dead = true };
            DamageSystem.Apply(d, 7f);
            Assert.AreEqual(0f, d.Received);
        }

        [Test]
        public void Apply_Ignored_WhenNonPositive()
        {
            var d = new Dummy();
            DamageSystem.Apply(d, 0f);
            DamageSystem.Apply(d, -5f);
            Assert.AreEqual(0f, d.Received);
        }
    }
}
