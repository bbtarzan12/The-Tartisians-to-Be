using Tartisians.Core.Services;
using Tartisians.Core.StateMachine;
using UnityEngine;

namespace Tartisians.Core.Bootstrap
{
    /// <summary>
    /// 명시적 게임 진입점. 씬 로드 전에 핵심 서비스를 초기화한다.
    /// 싱글톤 MonoBehaviour 대신 RuntimeInitializeOnLoadMethod로 부팅한다.
    /// </summary>
    public static class GameBootstrap
    {
        public static GameStateMachine StateMachine { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            ServiceLocator.Clear();

            StateMachine = new GameStateMachine();
            ServiceLocator.Register(StateMachine);

            Debug.Log("[GameBootstrap] 초기화 완료 — 상태 기계 등록됨.");
        }
    }
}
