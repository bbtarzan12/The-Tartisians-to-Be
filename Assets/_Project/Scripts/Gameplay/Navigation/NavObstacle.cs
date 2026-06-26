using UnityEngine;

namespace Tartisians.Gameplay.Navigation
{
    /// <summary>
    /// 흐름장에서 차단 셀로 표시될 정적 장애물 마커. Collider의 월드 바운즈로 셀을 막는다.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class NavObstacle : MonoBehaviour
    {
        Collider _collider;

        public Bounds WorldBounds
        {
            get
            {
                if (_collider == null)
                {
                    _collider = GetComponent<Collider>();
                }

                return _collider.bounds;
            }
        }
    }
}
