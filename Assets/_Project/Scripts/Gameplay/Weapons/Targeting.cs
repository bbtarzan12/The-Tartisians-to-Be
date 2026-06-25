using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>타게팅 순수 계산부. EditMode에서 단위 테스트한다.</summary>
    public static class Targeting
    {
        /// <summary>maxRange 내 가장 가까운 위치의 인덱스. 없으면 -1.</summary>
        public static int NearestIndexInRange(Vector3 from, IReadOnlyList<Vector3> positions, float maxRange)
        {
            int best = -1;
            float bestSq = maxRange * maxRange;
            for (int i = 0; i < positions.Count; i++)
            {
                float sq = (positions[i] - from).sqrMagnitude;
                if (sq <= bestSq)
                {
                    bestSq = sq;
                    best = i;
                }
            }

            return best;
        }
    }
}
