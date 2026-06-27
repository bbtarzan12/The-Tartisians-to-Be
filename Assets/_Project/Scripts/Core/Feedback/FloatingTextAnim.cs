using UnityEngine;

namespace Tartisians.Core.Feedback
{
    /// <summary>
    /// 떠오르는 데미지 숫자의 순수 애니메이션 커브 모음. 나이/수명만으로 오프셋·알파·스케일을 계산해
    /// UI 렌더링과 분리한다(단위 테스트 가능).
    /// </summary>
    public static class FloatingTextAnim
    {
        /// <summary>0~1 진행도(수명 0 이하나 초과 시 1로 클램프).</summary>
        public static float Progress(float age, float lifetime)
            => lifetime <= 0f ? 1f : Mathf.Clamp01(age / lifetime);

        /// <summary>위로 떠오르는 오프셋(진행도에 비례, 최대 rise).</summary>
        public static float RiseOffset(float progress, float rise)
            => rise * Mathf.Clamp01(progress);

        /// <summary>알파: fadeStart 전까지 1, 이후 끝(1)까지 선형으로 0.</summary>
        public static float Alpha(float progress, float fadeStart)
        {
            if (progress <= fadeStart)
            {
                return 1f;
            }

            float t = (progress - fadeStart) / Mathf.Max(0.0001f, 1f - fadeStart);
            return Mathf.Clamp01(1f - t);
        }

        /// <summary>등장 팝: 0~popTime 구간에서 popScale→1로 보간, 이후 1.</summary>
        public static float PopScale(float progress, float popScale, float popTime)
        {
            if (popTime <= 0f || progress >= popTime)
            {
                return 1f;
            }

            return Mathf.Lerp(popScale, 1f, progress / popTime);
        }
    }
}
