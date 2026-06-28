namespace Tartisians.Data
{
    /// <summary>
    /// 패시브 아이템의 효과 종류. 무기 영향 특성(힘/쿨다운/범위/다발/탄속)은 무기별 특성(TraitKind)으로
    /// 이전됐고, 패시브는 '무기와 무관한 플레이어 강화'만 담당한다. 값은 기존 에셋 직렬화 호환을 위해 고정.
    /// </summary>
    public enum PassiveKind
    {
        Magnet = 5,    // 자석 반경 +% (플레이어 유틸)
        MaxHealth = 6, // 최대 체력 +(고정)
        MoveSpeed = 7, // 이동 속도 +(고정)
    }
}
