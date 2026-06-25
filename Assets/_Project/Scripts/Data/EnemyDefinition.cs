using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 적 한 종류의 데이터 정의. 밸런싱은 .asset 인스턴스에서 조정한다.
    /// 아트가 없으므로 당분간 스케일/머티리얼로 종류를 구분한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Enemy Definition", fileName = "EnemyDefinition")]
    public sealed class EnemyDefinition : ScriptableObject
    {
        [SerializeField] float _maxHealth = 10f;
        [SerializeField] float _moveSpeed = 3f;
        [SerializeField] float _contactDamagePerSecond = 5f;
        [SerializeField] int _xpReward = 1;
        [SerializeField] float _radius = 0.5f;
        [SerializeField] Vector3 _scale = Vector3.one;
        [SerializeField] Material _material;

        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float ContactDamagePerSecond => _contactDamagePerSecond;
        public int XpReward => _xpReward;
        public float Radius => _radius;
        public Vector3 Scale => _scale;
        public Material Material => _material;
    }
}
