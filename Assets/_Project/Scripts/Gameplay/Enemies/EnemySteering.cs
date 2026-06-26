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

        /// <summary>타깃을 직선으로 향하는 seek + separation. (직선 추적용)</summary>
        public static Vector3 ComputeDelta(Vector3 self, Vector3 target, Vector3 separation, float speed, float separationWeight, float deltaTime)
        {
            return ComputeFromSeek(target - self, separation, speed, separationWeight, deltaTime);
        }

        /// <summary>
        /// 주어진 seek 방향(예: Flow Field 샘플)과 separation을 합쳐 이번 프레임 이동량을 계산한다.
        /// seek 방향이 외부에서 결정되므로 흐름장 기반 우회에 사용한다.
        /// </summary>
        public static Vector3 ComputeFromSeek(Vector3 seekDir, Vector3 separation, float speed, float separationWeight, float deltaTime)
        {
            return ComputeMove(seekDir, separation * separationWeight, speed, deltaTime);
        }

        /// <summary>
        /// seek 방향 + 이미 가중된 회피 벡터(separation·벽 반발 합)를 합쳐 이동량을 계산한다.
        /// 회피 항을 외부에서 조합하므로 벽 반발(거리장 그래디언트)까지 더할 수 있다.
        /// </summary>
        public static Vector3 ComputeMove(Vector3 seekDir, Vector3 avoidance, float speed, float deltaTime)
        {
            Vector3 seek = seekDir;
            seek.y = 0f;
            if (seek.sqrMagnitude > 1e-6f)
            {
                seek.Normalize();
            }

            Vector3 dir = seek + avoidance;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1e-6f)
            {
                dir.Normalize();
            }

            return dir * (speed * deltaTime);
        }
    }
}
