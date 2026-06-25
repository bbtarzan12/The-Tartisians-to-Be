namespace Tartisians.Core.Events
{
    /// <summary>
    /// EventBus를 통해 라우팅되는 모든 이벤트의 마커 인터페이스.
    /// 할당을 피하려면 구현 타입을 struct로 정의한다.
    /// </summary>
    public interface IEvent { }
}
