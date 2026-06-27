using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 무기 한 종류의 데이터 정의. 기본 스탯 + 레벨당 성장 델타 + 발사 형태 + 진화 링크.
    /// 런타임 <c>WeaponInstance</c>가 (기본값 + 레벨 델타) × 전역 패시브 수정자로 유효 스탯을 계산한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        [Header("식별/표시")]
        [SerializeField] string _id;
        [SerializeField] string _displayName = "Weapon";
        [SerializeField, TextArea] string _description = "";
        [SerializeField] Sprite _icon;
        [SerializeField] WeaponFireMode _fireMode = WeaponFireMode.NearestProjectile;
        [SerializeField] int _maxLevel = 8;
        [SerializeField] Color _color = Color.white; // VFX 색(투사체/오라/궤도/빔 공통)
        [SerializeField] float _vfxScale = 1f;       // VFX 크기 배율(진화형은 크게)

        [Header("기본 스탯 (Lv1)")]
        [SerializeField] float _fireInterval = 0.6f;
        [SerializeField] float _damage = 5f;
        [SerializeField] float _projectileSpeed = 14f;
        [SerializeField] int _pierce = 0;
        [SerializeField] float _range = 12f;
        [SerializeField] float _lifetime = 2f;
        [SerializeField] int _amount = 1;   // 동시 투사체/위성/부채꼴 발수
        [SerializeField] float _area = 1f;   // 오라 반경/창 길이/효과 크기 기준

        [Header("레벨당 성장 (Lv2부터 누적)")]
        [SerializeField] float _damagePerLevel = 1f;
        [SerializeField] float _fireRateReducePerLevel = 0f; // interval /= (1 + this×(L-1))
        [SerializeField] float _projectileSpeedPerLevel = 0f;
        [SerializeField] float _piercePerLevel = 0f;          // 누적 후 floor
        [SerializeField] float _amountPerLevel = 0f;          // 누적 후 floor (예: 0.5 → 2레벨당 +1)
        [SerializeField] float _areaPerLevel = 0f;

        [Header("진화")]
        [SerializeField] WeaponDefinition _evolvesInto;
        [SerializeField] PassiveItemDefinition _requiredPassive;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public WeaponFireMode FireMode => _fireMode;
        public int MaxLevel => Mathf.Max(1, _maxLevel);
        public Color Color => _color;
        public float VfxScale => Mathf.Max(0.05f, _vfxScale);

        public float FireInterval => Mathf.Max(0.02f, _fireInterval);
        public float Damage => _damage;
        public float ProjectileSpeed => _projectileSpeed;
        public int Pierce => _pierce;
        public float Range => _range;
        public float Lifetime => _lifetime;
        public int Amount => Mathf.Max(1, _amount);
        public float Area => _area;

        public float DamagePerLevel => _damagePerLevel;
        public float FireRateReducePerLevel => _fireRateReducePerLevel;
        public float ProjectileSpeedPerLevel => _projectileSpeedPerLevel;
        public float PiercePerLevel => _piercePerLevel;
        public float AmountPerLevel => _amountPerLevel;
        public float AreaPerLevel => _areaPerLevel;

        public WeaponDefinition EvolvesInto => _evolvesInto;
        public PassiveItemDefinition RequiredPassive => _requiredPassive;
        public bool CanEvolve => _evolvesInto != null;
    }
}
