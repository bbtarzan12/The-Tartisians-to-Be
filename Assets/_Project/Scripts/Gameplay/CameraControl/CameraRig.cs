using Unity.Cinemachine;
using UnityEngine;

namespace Tartisians.Gameplay.CameraControl
{
    /// <summary>
    /// Cinemachine 카메라가 플레이어를 추종하도록 타깃을 연결한다(쿼터뷰).
    /// 타깃이 지정되지 않으면 "Player" 태그로 찾아 연결한다.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform _target;

        CinemachineCamera _camera;

        void Awake() => _camera = GetComponent<CinemachineCamera>();

        void Start()
        {
            if (_target == null)
            {
                GameObject tagged = GameObject.FindGameObjectWithTag("Player");
                if (tagged != null)
                {
                    _target = tagged.transform;
                }
            }

            if (_target != null)
            {
                AssignTarget(_target);
            }
        }

        public void AssignTarget(Transform target)
        {
            _target = target;
            _camera.Follow = target;
            _camera.LookAt = target;
        }
    }
}
