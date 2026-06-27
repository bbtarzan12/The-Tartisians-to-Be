namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 보유 패시브에서 집계한 전역 무기 수정자. <c>WeaponInstance.Compute</c>가 소비한다.
    /// (Magnet/MaxHealth/MoveSpeed 같은 플레이어 스탯은 여기 포함하지 않는다.)
    /// </summary>
    public struct PassiveModifiers
    {
        public float MightPct;            // 데미지 +비율 (0.1 = +10%)
        public float CooldownPct;         // 발사 간격 감소 비율(= 연사 +비율)
        public float AreaPct;             // 효과 크기 +비율
        public float ProjectileSpeedPct;  // 투사체 속도 +비율
        public int AmountAdd;             // 동시 투사체 +개

        public static PassiveModifiers None => default;
    }
}
