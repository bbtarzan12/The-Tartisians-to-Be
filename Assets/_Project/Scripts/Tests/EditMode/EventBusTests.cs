using NUnit.Framework;
using Tartisians.Core.Events;

namespace Tartisians.Tests.EditMode
{
    public class EventBusTests
    {
        struct TestEvent : IEvent
        {
            public int Value;
        }

        [TearDown]
        public void Cleanup() => EventBus<TestEvent>.Clear();

        [Test]
        public void Raise_InvokesRegisteredBinding_WithArgs()
        {
            int received = -1;
            var binding = new EventBinding<TestEvent>(e => received = e.Value);
            EventBus<TestEvent>.Register(binding);

            EventBus<TestEvent>.Raise(new TestEvent { Value = 42 });

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Raise_InvokesNoArgBinding()
        {
            int count = 0;
            var binding = new EventBinding<TestEvent>(() => count++);
            EventBus<TestEvent>.Register(binding);

            EventBus<TestEvent>.Raise(new TestEvent());

            Assert.AreEqual(1, count);
        }

        [Test]
        public void Deregister_StopsReceiving()
        {
            int count = 0;
            var binding = new EventBinding<TestEvent>(() => count++);
            EventBus<TestEvent>.Register(binding);
            EventBus<TestEvent>.Deregister(binding);

            EventBus<TestEvent>.Raise(new TestEvent());

            Assert.AreEqual(0, count);
        }

        [Test]
        public void Register_IsIdempotent()
        {
            int count = 0;
            var binding = new EventBinding<TestEvent>(() => count++);
            EventBus<TestEvent>.Register(binding);
            EventBus<TestEvent>.Register(binding);

            EventBus<TestEvent>.Raise(new TestEvent());

            Assert.AreEqual(1, count, "같은 바인딩을 두 번 등록해도 한 번만 호출돼야 한다.");
        }
    }
}
