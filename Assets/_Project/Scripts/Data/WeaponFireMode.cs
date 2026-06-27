namespace Tartisians.Data
{
    /// <summary>무기 발사 형태. 인벤토리가 모드별 발사 핸들러를 분기한다.</summary>
    public enum WeaponFireMode
    {
        NearestProjectile, // 사거리 내 최근접 적 자동 조준 투사체
        SpreadProjectile,  // 이동 방향 부채꼴 다중 투사체(조준 안 함)
        AuraField,         // 플레이어 중심 원형 지속 틱(투사체 없음)
        PierceLine,        // 바라보는 방향 직선 관통
        Orbital,           // 주위를 도는 위성 접촉(상시)
    }
}
