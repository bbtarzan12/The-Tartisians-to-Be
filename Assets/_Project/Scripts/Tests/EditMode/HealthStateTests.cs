using NUnit.Framework;
using Tartisians.Gameplay.Combat;

namespace Tartisians.Tests.EditMode
{
    public class HealthStateTests
    {
        [Test]
        public void New_StartsAtFullHealth()
        {
            var hp = new HealthState(100f);
            Assert.AreEqual(100f, hp.Current);
            Assert.AreEqual(100f, hp.Max);
            Assert.IsFalse(hp.IsDead);
        }

        [Test]
        public void TakeDamage_ReducesCurrent_ReturnsApplied()
        {
            var hp = new HealthState(100f);
            float applied = hp.TakeDamage(30f);
            Assert.AreEqual(30f, applied);
            Assert.AreEqual(70f, hp.Current);
        }

        [Test]
        public void TakeDamage_Lethal_ClampsToZeroAndDies()
        {
            var hp = new HealthState(50f);
            float applied = hp.TakeDamage(80f);
            Assert.AreEqual(50f, applied, "실제 적용 데미지는 남은 체력까지만.");
            Assert.AreEqual(0f, hp.Current);
            Assert.IsTrue(hp.IsDead);
        }

        [Test]
        public void TakeDamage_WhenDead_DoesNothing()
        {
            var hp = new HealthState(10f);
            hp.TakeDamage(20f);
            float applied = hp.TakeDamage(5f);
            Assert.AreEqual(0f, applied);
            Assert.AreEqual(0f, hp.Current);
        }

        [Test]
        public void Heal_ClampsToMax()
        {
            var hp = new HealthState(100f);
            hp.TakeDamage(40f);
            hp.Heal(100f);
            Assert.AreEqual(100f, hp.Current);
        }

        [Test]
        public void Heal_WhenDead_DoesNotRevive()
        {
            var hp = new HealthState(10f);
            hp.TakeDamage(10f);
            hp.Heal(50f);
            Assert.IsTrue(hp.IsDead);
            Assert.AreEqual(0f, hp.Current);
        }
    }
}
