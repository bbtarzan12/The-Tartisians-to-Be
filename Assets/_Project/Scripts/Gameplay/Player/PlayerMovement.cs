using UnityEngine;

namespace Tartisians.Gameplay.Player
{
    /// <summary>
    /// 플레이어 이동의 순수 계산부. MonoBehaviour와 분리해 EditMode에서 단위 테스트한다.
    /// 입력은 XZ 평면으로 매핑하고, 대각 입력이 빨라지지 않도록 정규화한다.
    /// </summary>
    public static class PlayerMovement
    {
        public static Vector3 ComputeDelta(Vector2 input, float speed, float deltaTime)
        {
            Vector3 dir = new Vector3(input.x, 0f, input.y);
            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            return dir * (speed * deltaTime);
        }
    }
}
