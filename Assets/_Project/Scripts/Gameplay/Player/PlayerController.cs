using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Input;
using Tartisians.Gameplay.Progression;
using Tartisians.Systems.Crowd;
using UnityEngine;

namespace Tartisians.Gameplay.Player
{
    /// <summary>
    /// 입력을 받아 Rigidbody(키네마틱)를 XZ 평면으로 이동시키는 얇은 셸.
    /// 이동 계산은 순수 PlayerMovement에 위임한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] PlayerDefinition _definition;
        [SerializeField] MonoBehaviour _inputSource; // IMoveInputSource 구현체
        [SerializeField] Vector2 _arenaHalfExtent = new(19f, 19f); // 아레나 절반 크기. (0,0)이면 제한 없음
        [SerializeField] float _collisionRadius = 0.5f; // 플레이어 캡슐 반경(벽·장애물 밀어내기용)

        Rigidbody _rb;
        IMoveInputSource _input;
        Health _health;
        RunStats _stats;
        ObstacleField _obstacles;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _input = _inputSource as IMoveInputSource ?? GetComponent<IMoveInputSource>();
            TryGetComponent(out _health);
            ServiceLocator.TryGet(out _stats);

            if (_definition != null && _health != null)
            {
                _health.Configure(_definition.MaxHealth);
            }
        }

        void FixedUpdate()
        {
            if (_input == null)
            {
                return;
            }

            if (_stats == null)
            {
                ServiceLocator.TryGet(out _stats);
            }

            float speed = _stats != null ? _stats.MoveSpeed : (_definition != null ? _definition.MoveSpeed : 0f);
            if (speed <= 0f)
            {
                return;
            }

            Vector3 delta = PlayerMovement.ComputeDelta(_input.MoveInput, speed, Time.fixedDeltaTime);
            if (delta == Vector3.zero)
            {
                return;
            }

            Vector3 newPos = _rb.position + delta;

            // 아레나 경계로 제한 → 플레이어가 흐름장(=맵) 밖으로 못 나감
            if (_arenaHalfExtent.x > 0f && _arenaHalfExtent.y > 0f)
            {
                newPos.x = Mathf.Clamp(newPos.x, -_arenaHalfExtent.x, _arenaHalfExtent.x);
                newPos.z = Mathf.Clamp(newPos.z, -_arenaHalfExtent.y, _arenaHalfExtent.y);
            }

            // 장애물(둘레 벽 + 내부 장애물) 밀어내기 — 적과 동일한 해석적 ObstacleField 사용.
            // 키네마틱 이동이라 물리 충돌이 없으므로 직접 SDF로 빼낸다(내부 벽 관통 방지).
            if (_obstacles == null)
            {
                ServiceLocator.TryGet(out _obstacles);
            }

            if (_obstacles != null)
            {
                for (int i = 0; i < 2; i++)
                {
                    float d = _obstacles.Distance(newPos);
                    if (d >= _collisionRadius)
                    {
                        break;
                    }

                    Vector3 n = _obstacles.Normal(newPos);
                    if (n == Vector3.zero)
                    {
                        break;
                    }

                    newPos += n * (_collisionRadius - d);
                }
            }

            _rb.MovePosition(newPos);
        }
    }
}
