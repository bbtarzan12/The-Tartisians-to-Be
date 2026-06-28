using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>
    /// 조준 모드 계산용 순수 헬퍼(XZ 평면). 적 위치 리스트만 받아 인덱스를 고른다 — MonoBehaviour와
    /// 분리해 단위 테스트한다. 호출부가 인덱스로 실제 적/방향을 매핑한다.
    /// </summary>
    public static class WeaponAiming
    {
        /// <summary>
        /// origin에서 각 적 방향으로 쏜 직선 띠(length·halfWidth)가 가장 많은 적을 덮는 적의 인덱스.
        /// 동수면 더 가까운 적 우선. 후보 없으면 -1. (관통 무기의 MostInLine)
        /// </summary>
        public static int BestLaneIndex(Vector3 origin, IReadOnlyList<Vector3> positions, float length, float halfWidth)
        {
            if (positions == null || positions.Count == 0)
            {
                return -1;
            }

            int best = -1;
            int bestCount = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 dir = positions[i] - origin;
                dir.y = 0f;
                if (dir.sqrMagnitude < 1e-6f)
                {
                    continue;
                }

                dir.Normalize();
                int count = 0;
                for (int j = 0; j < positions.Count; j++)
                {
                    if (WeaponGeometry.PointInLane(origin, dir, length, halfWidth, positions[j]))
                    {
                        count++;
                    }
                }

                float distSq = (positions[i] - origin).sqrMagnitude;
                if (count > bestCount || (count == bestCount && distSq < bestDistSq))
                {
                    bestCount = count;
                    bestDistSq = distSq;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// 반경 radius 안에 다른 적이 가장 많은(밀집) 적의 인덱스. 동수면 더 가까운 적 우선
        /// (origin 기준). 후보 없으면 -1. (산탄·부메랑의 DensestCluster)
        /// </summary>
        public static int DensestClusterIndex(Vector3 origin, IReadOnlyList<Vector3> positions, float radius)
        {
            if (positions == null || positions.Count == 0)
            {
                return -1;
            }

            float r2 = radius * radius;
            int best = -1;
            int bestCount = -1;
            float bestDistSq = float.MaxValue;

            for (int i = 0; i < positions.Count; i++)
            {
                int count = 0;
                for (int j = 0; j < positions.Count; j++)
                {
                    Vector3 d = positions[j] - positions[i];
                    d.y = 0f;
                    if (d.sqrMagnitude <= r2)
                    {
                        count++; // 자기 자신 포함
                    }
                }

                float distSq = (positions[i] - origin).sqrMagnitude;
                if (count > bestCount || (count == bestCount && distSq < bestDistSq))
                {
                    bestCount = count;
                    bestDistSq = distSq;
                    best = i;
                }
            }

            return best;
        }
    }
}
