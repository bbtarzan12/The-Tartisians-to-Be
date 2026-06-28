namespace Tartisians.Data
{
    /// <summary>투사체의 이동 행동. Projectile이 매 물리 스텝에 적용한다. (Straight=0이 기본)</summary>
    public enum ProjectileMotion
    {
        Straight,  // 직선
        Homing,    // 가장 가까운 적을 향해 조향(곡선)
        Boomerang, // 나갔다가 플레이어로 되돌아옴(왕복 2회 타격)
        Ricochet,  // 명중 후 다음 가까운 적으로 튕김(연쇄)
    }
}
