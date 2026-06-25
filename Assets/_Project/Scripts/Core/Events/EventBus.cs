using System.Collections.Generic;

namespace Tartisians.Core.Events
{
    /// <summary>
    /// 타입 안전·저할당 이벤트 버스. 이벤트 타입마다 독립된 정적 바인딩 목록을 가진다.
    /// Raise는 역순 순회라 핸들러 안에서의 Deregister에 안전하다.
    /// </summary>
    public static class EventBus<T> where T : IEvent
    {
        static readonly List<IEventBinding<T>> Bindings = new();

        public static void Register(EventBinding<T> binding)
        {
            if (!Bindings.Contains(binding))
            {
                Bindings.Add(binding);
            }
        }

        public static void Deregister(EventBinding<T> binding) => Bindings.Remove(binding);

        public static void Raise(T @event)
        {
            for (int i = Bindings.Count - 1; i >= 0; i--)
            {
                IEventBinding<T> binding = Bindings[i];
                binding.OnEvent.Invoke(@event);
                binding.OnEventNoArgs.Invoke();
            }
        }

        public static void Clear() => Bindings.Clear();
    }
}
