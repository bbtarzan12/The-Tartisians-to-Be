using System.Collections.Generic;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 업그레이드 후보에서 중복 없이 N개를 뽑는 순수 로직. RNG는 주입받아 결정론적으로 테스트한다.
    /// </summary>
    public static class UpgradePicker
    {
        /// <summary>0..count-1 중 중복 없는 최대 take개의 인덱스를 results에 채운다(Fisher–Yates 부분 셔플).</summary>
        public static void PickDistinct(int count, int take, System.Func<int, int> nextInt, List<int> results)
        {
            results.Clear();
            if (count <= 0 || take <= 0)
            {
                return;
            }

            var pool = new List<int>(count);
            for (int i = 0; i < count; i++)
            {
                pool.Add(i);
            }

            int n = take < count ? take : count;
            for (int i = 0; i < n; i++)
            {
                int j = i + nextInt(count - i); // i..count-1
                (pool[i], pool[j]) = (pool[j], pool[i]);
                results.Add(pool[i]);
            }
        }
    }
}
