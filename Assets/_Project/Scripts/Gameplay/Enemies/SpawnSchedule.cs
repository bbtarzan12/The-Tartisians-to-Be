using System.Collections.Generic;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>
    /// 시나리오 스폰의 순수 선택 로직(UnityEngine 비의존, EditMode 테스트 대상).
    /// 단계 인덱스 결정과 가중치 기반 인덱스 선택만 담당한다.
    /// </summary>
    public static class SpawnSchedule
    {
        /// <summary>StartTime들 중 time 이하인 마지막 단계 인덱스. 비면 -1, 첫 단계보다 이르면 0.</summary>
        public static int PhaseIndexAt(IReadOnlyList<float> startTimes, float time)
        {
            if (startTimes == null || startTimes.Count == 0)
            {
                return -1;
            }

            int idx = 0;
            for (int i = 0; i < startTimes.Count; i++)
            {
                if (startTimes[i] <= time)
                {
                    idx = i;
                }
                else
                {
                    break;
                }
            }

            return idx;
        }

        /// <summary>
        /// 가중치 누적 분포에서 roll(∈[0,1))에 대응하는 인덱스.
        /// 양수 가중치만 고려. 총합이 0 이하면 -1.
        /// </summary>
        public static int WeightedIndex(IReadOnlyList<float> weights, float roll)
        {
            if (weights == null || weights.Count == 0)
            {
                return -1;
            }

            float total = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                }
            }

            if (total <= 0f)
            {
                return -1;
            }

            if (roll < 0f)
            {
                roll = 0f;
            }

            float target = roll * total;
            float acc = 0f;
            int last = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                last = i;
                acc += weights[i];
                if (target < acc)
                {
                    return i;
                }
            }

            // 부동소수 경계로 끝까지 떨어지면 마지막 양수 항목.
            return last;
        }
    }
}
