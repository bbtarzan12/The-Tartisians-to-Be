using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>
    /// 적 스티어링의 순수 계산부(seek + separation). EditMode에서 단위 테스트한다.
    /// 모두 XZ 평면 기준이며 결과는 정규화 후 속도·dt로 스케일한다.
    /// </summary>
    public static class EnemySteering
    {
        /// <summary>이웃들로부터 밀려나는 분리 벡터(거리가 가까울수록 강함).</summary>
        public static Vector3 Separation(Vector3 self, IReadOnlyList<Vector3> positions, IReadOnlyList<int> indices, float radius)
        {
            Vector3 push = Vector3.zero;
            for (int i = 0; i < indices.Count; i++)
            {
                Vector3 d = self - positions[indices[i]];
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > 1e-4f && dist < radius)
                {
                    push += d / dist * (1f - dist / radius);
                }
            }

            return push;
        }

        /// <summary>seek(타깃 방향)와 separation을 합쳐 이번 프레임 이동량을 계산한다.</summary>
        public static Vector3 ComputeDelta(Vector3 self, Vector3 target, Vector3 separation, float speed, float separationWeight, float deltaTime)
        {
            Vector3 seek = target - self;
            seek.y = 0f;
            if (seek.sqrMagnitude > 1e-6f)
            {
                seek.Normalize();
            }

            Vector3 dir = seek + separation * separationWeight;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                dir.Normalize();
            }

            return dir * (speed * deltaTime);
        }
    }
}
