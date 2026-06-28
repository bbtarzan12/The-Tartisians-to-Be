namespace Tartisians.Data
{
    /// <summary>
    /// 무기가 업그레이드할 수 있는 특성 종류. 무기마다 지원하는 특성 목록이 다르다
    /// (예: 범위는 오라/창/궤도엔 있고 단일 투사체엔 없음). 값은 직렬화 안정성을 위해 고정.
    /// </summary>
    public enum TraitKind
    {
        Damage = 0,          // 공격력
        Amount = 1,          // 다발(동시 투사체/위성/발수)
        Cooldown = 2,        // 쿨다운(발사 간격 감소)
        ProjectileSpeed = 3, // 탄속
        Pierce = 4,          // 관통
        Area = 5,            // 범위(오라 반경/창 길이/궤도 반경 등)
    }

    public static class TraitKinds
    {
        public const int Count = 6;
    }
}
