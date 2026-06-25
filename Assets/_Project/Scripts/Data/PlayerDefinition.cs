using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 플레이어 기본 스탯을 담는 데이터 정의. 밸런싱은 .asset 인스턴스에서 조정한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Player Definition", fileName = "PlayerDefinition")]
    public sealed class PlayerDefinition : ScriptableObject
    {
        [SerializeField] float _maxHealth = 100f;
        [SerializeField] float _moveSpeed = 6f;
        [SerializeField] float _pickupRadius = 2.5f;

        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float PickupRadius => _pickupRadius;
    }
}
