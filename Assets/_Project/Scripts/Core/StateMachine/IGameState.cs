namespace Tartisians.Core.StateMachine
{
    /// <summary>게임 상위 상태(Menu/Play/LevelUp/GameOver 등)의 공통 인터페이스.</summary>
    public interface IGameState
    {
        void Enter();
        void Tick(float deltaTime);
        void Exit();
    }
}
