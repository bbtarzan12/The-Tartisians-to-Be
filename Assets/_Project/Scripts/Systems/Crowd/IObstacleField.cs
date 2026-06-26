using UnityEngine;

namespace Tartisians.Systems.Crowd
{
    /// <summary>
    /// 정적 장애물에 대한 부호거리장(SDF) 질의. PBD 군중 솔버가 벽을
    /// '무한 질량 위치 제약'으로 다룰 때 사용한다(적-적 비침투와 동일한 제약 형태).
    /// </summary>
    public interface IObstacleField
    {
        /// <summary>월드 위치에서 가장 가까운 장애물까지의 거리(월드 단위). 장애물이 없으면 큰 값.</summary>
        float Distance(Vector3 world);

        /// <summary>장애물에서 멀어지는 단위 방향(XZ). 평지(장애물 없음)면 zero.</summary>
        Vector3 Normal(Vector3 world);
    }
}
