using UnityEngine;

namespace Tartisians.Gameplay.Player
{
    /// <summary>
    /// 캐릭터 비주얼(스킨드 메시 + Animator)을 게임플레이 이동에 동기화한다.
    /// 부모(실제 이동 주체)의 평면 이동량으로 Animator의 Speed(0~1)를 구동하고,
    /// 진행 방향으로 부드럽게 회전시킨다. 이동 로직(PlayerController)과 분리된 표현 전용 셸이라
    /// 입력 주체(플레이어/오토플레이)가 누구든 동일하게 동작한다.
    /// </summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        static readonly int SpeedHash = Animator.StringToHash("Speed");

        [SerializeField] Animator _animator;
        [SerializeField] Transform _motionSource;   // 이동 주체. 비우면 부모를 사용
        [SerializeField] float _walkSpeedReference = 5f; // 이 속도(유닛/초)에서 Speed=1
        [SerializeField] float _speedDamp = 0.12f;   // Speed 파라미터 평활화 시간
        [SerializeField] float _turnSpeed = 12f;     // 진행 방향 회전 속도
        [SerializeField] float _moveThreshold = 0.05f; // 이 평면속도 미만은 정지로 간주

        Transform _source;
        Vector3 _lastPos;
        float _speedParam;

        void Awake()
        {
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            _source = _motionSource != null ? _motionSource : transform.parent;
            if (_source != null)
            {
                _lastPos = _source.position;
            }
        }

        void Update()
        {
            if (_source == null || _animator == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            Vector3 delta = _source.position - _lastPos;
            _lastPos = _source.position;
            delta.y = 0f;

            float planarSpeed = delta.magnitude / dt;

            // Idle(0)↔Walk(1) 블렌드 값. 기준 속도로 정규화 후 평활화.
            float target = _walkSpeedReference > 0f ? Mathf.Clamp01(planarSpeed / _walkSpeedReference) : 0f;
            _speedParam = Mathf.MoveTowards(_speedParam, target, dt / Mathf.Max(0.001f, _speedDamp));
            _animator.SetFloat(SpeedHash, _speedParam);

            // 이동 중이면 진행 방향으로 회전(고정 쿼터뷰 카메라 기준 캐릭터 방향).
            if (planarSpeed > _moveThreshold)
            {
                Quaternion want = Quaternion.LookRotation(delta.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, want, 1f - Mathf.Exp(-_turnSpeed * dt));
            }
        }
    }
}
