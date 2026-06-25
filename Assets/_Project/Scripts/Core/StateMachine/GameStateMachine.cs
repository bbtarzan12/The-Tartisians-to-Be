namespace Tartisians.Core.StateMachine
{
    /// <summary>
    /// 단순 상위 상태 기계. 한 번에 하나의 상태만 활성이며 전이 시
    /// 이전 상태 Exit → 현재 상태 갱신 → 새 상태 Enter 순으로 호출한다.
    /// </summary>
    public sealed class GameStateMachine
    {
        public IGameState Current { get; private set; }

        public void ChangeState(IGameState next)
        {
            if (ReferenceEquals(Current, next))
            {
                return;
            }

            Current?.Exit();
            Current = next;
            Current?.Enter();
        }

        public void Tick(float deltaTime) => Current?.Tick(deltaTime);
    }
}
