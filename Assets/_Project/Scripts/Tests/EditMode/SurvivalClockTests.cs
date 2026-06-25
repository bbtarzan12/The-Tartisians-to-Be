using NUnit.Framework;
using Tartisians.Gameplay.Flow;

namespace Tartisians.Tests.EditMode
{
    public class SurvivalClockTests
    {
        [Test]
        public void Tick_AccumulatesElapsed_AndReportsRemaining()
        {
            var clock = new SurvivalClock(10f);
            clock.Tick(3f);
            Assert.AreEqual(3f, clock.Elapsed, 1e-4f);
            Assert.AreEqual(7f, clock.Remaining, 1e-4f);
            Assert.IsFalse(clock.IsComplete);
        }

        [Test]
        public void IsComplete_WhenElapsedReachesDuration()
        {
            var clock = new SurvivalClock(5f);
            clock.Tick(5f);
            Assert.IsTrue(clock.IsComplete);
            Assert.AreEqual(0f, clock.Remaining, 1e-4f);
        }

        [Test]
        public void Reset_ClearsElapsed()
        {
            var clock = new SurvivalClock(5f);
            clock.Tick(5f);
            clock.Reset();
            Assert.AreEqual(0f, clock.Elapsed);
            Assert.IsFalse(clock.IsComplete);
        }
    }
}
