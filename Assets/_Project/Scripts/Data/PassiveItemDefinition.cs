using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 패시브 아이템 한 종류(무기 비종속 플레이어 강화: 이속/체력/자석).
    /// 효과 값은 레벨에 비례(ValuePerLevel × level). 종류는 <see cref="PassiveKind"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Passive Item Definition", fileName = "PassiveItem")]
    public sealed class PassiveItemDefinition : ScriptableObject
    {
        [SerializeField] string _id;
        [SerializeField] string _displayName = "Passive";
        [SerializeField, TextArea] string _description = "";
        [SerializeField] Sprite _icon;
        [SerializeField] PassiveKind _kind = PassiveKind.MoveSpeed;
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
