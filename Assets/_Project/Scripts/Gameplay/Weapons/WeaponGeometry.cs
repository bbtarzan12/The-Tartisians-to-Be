using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>무기 발사 판정용 순수 기하 헬퍼(XZ 평면, y 무시). 단위 테스트 대상.</summary>
    public static class WeaponGeometry
    {
        /// <summary>
        /// origin에서 dir(정규화 XZ) 방향으로 길이 length, 반폭 halfWidth인 직선 띠(관통 라인) 안에
        /// point가 들어오는지. along∈[0,length] 이고 수직거리 ≤ halfWidth면 true.
        /// </summary>
        public static bool PointInLane(Vector3 origin, Vector3 dir, float length, float halfWidth, Vector3 point)
        {
            float rx = point.x - origin.x;
            float rz = point.z - origin.z;
            float along = rx * dir.x + rz * dir.z; // 진행축 투영(내적)
            if (along < 0f || along > length)
            {
                return false;
            }

            float px = rx - dir.x * along; // 수직 성분
            float pz = rz - dir.z * along;
            return px * px + pz * pz <= halfWidth * halfWidth;
        }
    }
}
