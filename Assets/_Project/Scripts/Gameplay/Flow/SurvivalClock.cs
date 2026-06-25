using UnityEngine;

namespace Tartisians.Gameplay.Flow
{
    /// <summary>생존 타이머의 순수 상태. EditMode에서 단위 테스트한다.</summary>
    public sealed class SurvivalClock
    {
        public float Duration { get; }
        public float Elapsed { get; private set; }

        public SurvivalClock(float duration) => Duration = Mathf.Max(0f, duration);

        public float Remaining => Mathf.Max(0f, Duration - Elapsed);
        public bool IsComplete => Elapsed >= Duration;

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                Elapsed += deltaTime;
            }
        }

        public void Reset() => Elapsed = 0f;
    }
}
