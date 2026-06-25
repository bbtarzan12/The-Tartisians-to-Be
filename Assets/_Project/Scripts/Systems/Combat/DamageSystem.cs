using Tartisians.Core.Combat;

namespace Tartisians.Systems.Combat
{
    /// <summary>데미지 적용의 단일 진입점. 향후 방어력·크리티컬 등을 여기서 확장한다.</summary>
    public static class DamageSystem
    {
        public static void Apply(IDamageable target, float amount)
        {
            if (target != null && !target.IsDead && amount > 0f)
            {
                target.TakeDamage(amount);
            }
        }
    }
}
