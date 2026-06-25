namespace Tartisians.Systems.Pooling
{
    /// <summary>
    /// 풀에서 꺼내지거나 반환될 때 알림을 받고 싶은 컴포넌트가 구현한다.
    /// (예: 속도/수명 초기화, 파티클 정지). PrefabPool이 자동으로 호출한다.
    /// </summary>
    public interface IPoolable
    {
        void OnSpawned();
        void OnDespawned();
    }
}
