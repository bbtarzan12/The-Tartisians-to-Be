namespace Tartisians.Data
{
    /// <summary>무기가 표적/발사 방향을 고르는 방식. WeaponController가 해석한다.</summary>
    public enum WeaponAimMode
    {
        Nearest,        // 최근접 적
        MostInLine,     // 직선 띠에 최다 적중되는 방향(관통)
        DensestCluster, // 적이 가장 밀집한 방향/표적(산탄·부메랑)
        LowestHealth,   // 체력이 가장 낮은 적(막타·정리)
    }
}
