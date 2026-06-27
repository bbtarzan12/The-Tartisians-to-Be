using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>패시브 아이템의 효과 종류. 전역(모든 무기/플레이어)에 적용된다.</summary>
    public enum PassiveKind
    {
        Might,           // 전 무기 데미지 +% (레벨당 ValuePerLevel, 0.1 = +10%)
        Cooldown,        // 전 무기 발사 간격 -% (연사 +%)
        Area,            // 오라/폭발/창 크기 +%
        Amount,          // 동시 투사체 +개 (레벨당, 보통 정수)
        ProjectileSpeed, // 투사체 속도 +%
        Magnet,          // 자석 반경 +% (플레이어 유틸)
        MaxHealth,       // 최대 체력 +(고정)
        MoveSpeed,       // 이동 속도 +(고정)
    }

    /// <summary>
    /// 패시브 아이템 한 종류. 레벨업으로 누적되며 일부는 무기 진화 조건이 된다.
    /// 효과 값은 레벨에 비례(ValuePerLevel × level).
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Passive Item Definition", fileName = "PassiveItem")]
    public sealed class PassiveItemDefinition : ScriptableObject
    {
        [SerializeField] string _id;
        [SerializeField] string _displayName = "Passive";
        [SerializeField, TextArea] string _description = "";
        [SerializeField] Sprite _icon;
        [SerializeField] PassiveKind _kind = PassiveKind.Might;
        [SerializeField] float _valuePerLevel = 0.1f;
        [SerializeField] int _maxLevel = 5;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public PassiveKind Kind => _kind;
        public float ValuePerLevel => _valuePerLevel;
        public int MaxLevel => Mathf.Max(1, _maxLevel);

        /// <summary>주어진 레벨에서의 누적 효과 값(0..MaxLevel로 클램프).</summary>
        public float ValueAtLevel(int level) => _valuePerLevel * Mathf.Clamp(level, 0, MaxLevel);
    }
}
