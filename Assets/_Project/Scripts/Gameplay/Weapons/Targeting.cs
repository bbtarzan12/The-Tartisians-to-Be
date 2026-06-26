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

        /// <summary>
        /// 예측 사격 조준점. 투사체 도달 시간(거리/속도)만큼 타깃이 더 이동할 위치를 1차 예측한다.
        /// leadFactor로 예측 강도를 조절(0=현재 위치, 1=완전 선행). 순수 함수.
        /// </summary>
        public static Vector3 PredictAimPoint(Vector3 shooter, Vector3 targetPos, Vector3 targetVelocity, float projectileSpeed, float leadFactor)
        {
            if (projectileSpeed <= 0f)
            {
                return targetPos;
            }

            float travelTime = Vector3.Distance(shooter, targetPos) / projectileSpeed;
            return targetPos + targetVelocity * (travelTime * leadFactor);
        }
    }
}
