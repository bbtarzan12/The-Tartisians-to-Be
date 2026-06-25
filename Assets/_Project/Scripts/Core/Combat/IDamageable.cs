namespace Tartisians.Core.Combat
{
    /// <summary>
    /// 데미지를 받을 수 있는 대상의 공통 계약. 플레이어·적 모두 구현한다.
    /// DamageSystem(M4)이 이 인터페이스를 통해 데미지를 적용한다.
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(float amount);
    }
}
