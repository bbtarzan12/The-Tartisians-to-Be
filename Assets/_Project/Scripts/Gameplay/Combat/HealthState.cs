using UnityEngine;

namespace Tartisians.Gameplay.Combat
{
    /// <summary>
    /// 체력의 순수 상태/로직. MonoBehaviour와 분리해 EditMode에서 단위 테스트한다.
    /// </summary>
    public sealed class HealthState
    {
        public float Max { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public HealthState(float max) => Reset(max);

        public void Reset(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
        }

        /// <summary>실제 적용된 데미지를 반환한다(이미 사망/0 이하면 0).</summary>
        public float TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return 0f;
            }

            float applied = Mathf.Min(Current, amount);
            Current -= applied;
            return applied;
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Min(Max, Current + amount);
        }

        /// <summary>최대 체력을 새 값으로 바꾸되 현재 체력은 유지(상한 클램프). 증가분만큼 회복 옵션.</summary>
        public void SetMax(float max, bool healByDelta)
        {
            float newMax = Mathf.Max(1f, max);
            float delta = newMax - Max;
            Max = newMax;
            if (healByDelta && delta > 0f)
            {
                Current += delta;
            }

            Current = Mathf.Min(Current, Max);
        }
    }
}
