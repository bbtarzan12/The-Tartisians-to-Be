using System;

namespace Tartisians.Core.Events
{
    /// <summary>EventBus가 호출하는 바인딩 표면. 인수 있는/없는 핸들러를 모두 노출한다.</summary>
    public interface IEventBinding<T>
    {
        Action<T> OnEvent { get; }
        Action OnEventNoArgs { get; }
    }

    /// <summary>
    /// 특정 이벤트 타입 T에 대한 콜백 묶음. 인수를 받는 핸들러와 받지 않는 핸들러를
    /// 함께 보관한다. 필드는 빈 델리게이트로 초기화돼 null 검사 없이 호출 가능하다.
    /// </summary>
    public sealed class EventBinding<T> : IEventBinding<T> where T : IEvent
    {
        Action<T> _onEvent = _ => { };
        Action _onEventNoArgs = () => { };

        Action<T> IEventBinding<T>.OnEvent => _onEvent;
        Action IEventBinding<T>.OnEventNoArgs => _onEventNoArgs;

        public EventBinding(Action<T> onEvent) => _onEvent = onEvent;
        public EventBinding(Action onEventNoArgs) => _onEventNoArgs = onEventNoArgs;

        public void Add(Action<T> onEvent) => _onEvent += onEvent;
        public void Remove(Action<T> onEvent) => _onEvent -= onEvent;
        public void Add(Action onEventNoArgs) => _onEventNoArgs += onEventNoArgs;
        public void Remove(Action onEventNoArgs) => _onEventNoArgs -= onEventNoArgs;
    }
}
