using NUnit.Framework;
using Tartisians.Core.Feedback;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class HitReactStateTests
    {
        [Test]
        public void Trigger_FlashAmountStartsAtOne()
        {
            var s = new HitReactState();
            s.Trigger(0.1f, 0.1f);
            Assert.AreEqual(1f, s.FlashAmount, 1e-4f);
            Assert.IsTrue(s.IsActive);
        }

        [Test]
        public void Tick_FullDuration_DecaysToZeroAndInactive()
        {
            var s = new HitReactState();
            s.Trigger(0.1f, 0.1f);
            s.Tick(0.2f); // 수명보다 길게
            Assert.AreEqual(0f, s.FlashAmount, 1e-4f);
            Assert.IsFalse(s.IsActive);
        }

        [Test]
        public void FlashAmount_HalfwayIsAboutHalf()
        {
            var s = new HitReactState();
            s.Trigger(0.1f, 0.1f);
            s.Tick(0.05f);
            Assert.AreEqual(0.5f, s.FlashAmount, 1e-3f);
        }

        [Test]
        public void ScaleMultiplier_StartAndEndAreOne_MiddleIsLarger()
        {
            var s = new HitReactState();
            s.Trigger(0.1f, 0.1f);
            Assert.AreEqual(1f, s.ScaleMultiplier(0.5f), 1e-3f, "시작은 1");

            s.Tick(0.05f); // 진행도 0.5 → sin(π/2)=1 → 최대
            Assert.Greater(s.ScaleMultiplier(0.5f), 1.4f);

            s.Tick(0.05f); // 끝
            Assert.AreEqual(1f, s.ScaleMultiplier(0.5f), 1e-3f, "끝은 1");
        }

        [Test]
        public void Reset_ClearsActive()
        {
            var s = new HitReactState();
            s.Trigger(0.1f, 0.1f);
            s.Reset();
            Assert.IsFalse(s.IsActive);
            Assert.AreEqual(0f, s.FlashAmount);
        }
    }

    public class DangerMeterTests
    {
        [Test]
        public void New_StartsAtZero()
        {
            var d = new DangerMeter();
            Assert.AreEqual(0f, d.Value);
        }

        [Test]
        public void Hit_RaisesValue_SaturatesAtOne()
        {
            var d = new DangerMeter();
            d.Hit(0.3f);
            Assert.AreEqual(0.3f, d.Value, 1e-4f);
            d.Hit(1f);
            Assert.AreEqual(1f, d.Value, 1e-4f);
        }

        [Test]
        public void Decay_IsProportional_FloorsAtZero()
        {
            var d = new DangerMeter();
            d.Hit(0.5f);
            d.Decay(0.1f, 2f); // 0.5 * (1 - 0.2) = 0.4 (지수 감쇠)
            Assert.AreEqual(0.4f, d.Value, 1e-4f);
            d.Decay(10f, 2f); // rate·dt > 1 → 0으로 클램프
            Assert.AreEqual(0f, d.Value);
        }

        [Test]
        public void Decay_SustainedHits_ConvergeToStableMidpoint()
        {
            // 매 스텝 동일 강도 누적 + 지수 감쇠 → 0도 1도 아닌 중간값으로 수렴.
            var d = new DangerMeter();
            for (int i = 0; i < 400; i++)
            {
                d.Hit(0.05f);
                d.Decay(0.02f, 6f);
            }

            Assert.Greater(d.Value, 0.1f, "지속 피격이면 0이 아니어야");
            Assert.Less(d.Value, 0.9f, "약한 지속 피격이면 포화하지 않아야");
        }
    }

    public class FloatingTextAnimTests
    {
        [Test]
        public void Progress_Clamps()
        {
            Assert.AreEqual(0f, FloatingTextAnim.Progress(0f, 1f));
            Assert.AreEqual(0.5f, FloatingTextAnim.Progress(0.5f, 1f), 1e-4f);
            Assert.AreEqual(1f, FloatingTextAnim.Progress(5f, 1f), "초과 시 1로 클램프");
            Assert.AreEqual(1f, FloatingTextAnim.Progress(0.5f, 0f), "수명 0이면 1");
        }

        [Test]
        public void RiseOffset_MonotonicUpToRise()
        {
            Assert.AreEqual(0f, FloatingTextAnim.RiseOffset(0f, 2f), 1e-4f);
            Assert.AreEqual(2f, FloatingTextAnim.RiseOffset(1f, 2f), 1e-4f);
            Assert.Greater(FloatingTextAnim.RiseOffset(0.75f, 2f), FloatingTextAnim.RiseOffset(0.25f, 2f));
        }

        [Test]
        public void Alpha_FullBeforeFadeStart_ZeroAtEnd()
        {
            Assert.AreEqual(1f, FloatingTextAnim.Alpha(0.5f, 0.6f), 1e-4f, "fadeStart 전엔 1");
            Assert.AreEqual(0f, FloatingTextAnim.Alpha(1f, 0.6f), 1e-4f, "끝엔 0");
            Assert.AreEqual(0.5f, FloatingTextAnim.Alpha(0.8f, 0.6f), 1e-3f, "중간 선형");
        }

        [Test]
        public void PopScale_StartsLarge_SettlesToOne()
        {
            Assert.AreEqual(1.5f, FloatingTextAnim.PopScale(0f, 1.5f, 0.2f), 1e-4f);
            Assert.AreEqual(1f, FloatingTextAnim.PopScale(0.2f, 1.5f, 0.2f), 1e-4f);
            Assert.AreEqual(1f, FloatingTextAnim.PopScale(0.9f, 1.5f, 0.2f), 1e-4f, "popTime 이후 1");
        }
    }
}
