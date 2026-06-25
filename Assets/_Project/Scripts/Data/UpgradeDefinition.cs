using UnityEngine;

namespace Tartisians.Data
{
    public enum UpgradeKind
    {
        MoveSpeed,
        MaxHealth,
        PickupRadius,
        WeaponDamage,
        WeaponFireRate,
        WeaponPierce,
        ProjectileSpeed,
    }

    /// <summary>레벨업 시 선택 가능한 업그레이드 한 종류.</summary>
    [CreateAssetMenu(menuName = "Tartisians/Upgrade Definition", fileName = "UpgradeDefinition")]
    public sealed class UpgradeDefinition : ScriptableObject
    {
        [SerializeField] string _title = "Upgrade";
        [SerializeField, TextArea] string _description = "";
        [SerializeField] UpgradeKind _kind = UpgradeKind.WeaponDamage;
        [SerializeField] float _value = 1f;

        public string Title => _title;
        public string Description => _description;
        public UpgradeKind Kind => _kind;
        public float Value => _value;
    }
}
