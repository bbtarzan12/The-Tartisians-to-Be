using NUnit.Framework;
using Tartisians.Core.StateMachine;

namespace Tartisians.Tests.EditMode
{
    public class GameStateMachineTests
    {
        sealed class SpyState : IGameState
        {
            public int Entered;
            public int Exited;
            public int Ticked;

            public void Enter() => Entered++;
            public void Tick(float deltaTime) => Ticked++;
            public void Exit() => Exited++;
        }

        [Test]
        public void ChangeState_EntersNewState()
        {
            var sm = new GameStateMachine();
            var a = new SpyState();

            sm.ChangeState(a);

            Assert.AreEqual(1, a.Entered);
            Assert.AreSame(a, sm.Current);
        }

        [Test]
        public void ChangeState_ExitsPrevious_EntersNext()
        {
            var sm = new GameStateMachine();
            var a = new SpyState();
            var b = new SpyState();

            sm.ChangeState(a);
            sm.ChangeState(b);

            Assert.AreEqual(1, a.Exited);
            Assert.AreEqual(1, b.Entered);
            Assert.AreSame(b, sm.Current);
        }

        [Test]
        public void ChangeState_ToSameState_DoesNothing()
        {
            var sm = new GameStateMachine();
            var a = new SpyState();

            sm.ChangeState(a);
            sm.ChangeState(a);

            Assert.AreEqual(1, a.Entered);
            Assert.AreEqual(0, a.Exited);
        }

        [Test]
        public void Tick_ForwardsToCurrentState()
        {
            var sm = new GameStateMachine();
            var a = new SpyState();
            sm.ChangeState(a);

            sm.Tick(0.016f);

            Assert.AreEqual(1, a.Ticked);
        }
    }
}
