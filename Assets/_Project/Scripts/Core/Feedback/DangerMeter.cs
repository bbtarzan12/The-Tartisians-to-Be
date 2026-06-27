using UnityEngine;

namespace Tartisians.Core.Feedback
{
    /// <summary>
    /// 플레이어 피격 누적 강도(0~1)의 순수 상태. 화면 가장자리 붉은 비네트의 알파를 구동한다.
    /// 지속 접촉 데미지(매 프레임 소량)에도 깜빡이지 않도록 누적·감쇠 모델을 쓴다.
    /// </summary>
    public struct DangerMeter
    {
        float _value;

        public float Value => Mathf.Clamp01(_value);

        /// <summary>피격 시 강도를 올린다(누적, 1로 포화). intensity는 0~1 기여분.</summary>
        public void Hit(float intensity) => _value = Mathf.Clamp01(_value + Mathf.Max(0f, intensity));

        /// <summary>
        /// 지수 감쇠(현재값에 비례). 일정 강도의 지속 피격에서 안정적인 중간값(≈누적률/rate)으로 수렴해
        /// 선형 감쇠의 0/1 바운스 문제를 피한다. dt당 rate·dt 비율만큼 감소.
        /// </summary>
        public void Decay(float dt, float rate) => _value = Mathf.Max(0f, _value - _value * Mathf.Max(0f, rate) * dt);

        public void Reset() => _value = 0f;
    }
}
