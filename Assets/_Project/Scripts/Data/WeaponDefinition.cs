using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 무기 한 종류의 데이터 정의. 자동 발사형 투사체 무기 기준.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [SerializeField] float _fireInterval = 0.6f;
        [SerializeField] float _damage = 5f;
        [SerializeField] float _projectileSpeed = 14f;
        [SerializeField] int _pierce = 0;
        [SerializeField] float _range = 12f;
        [SerializeField] float _lifetime = 2f;

        public float FireInterval => Mathf.Max(0.02f, _fireInterval);
        public float Damage => _damage;
        public float ProjectileSpeed => _projectileSpeed;
        public int Pierce => _pierce;
        public float Range => _range;
        public float Lifetime => _lifetime;
    }
}
