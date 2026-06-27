using Tartisians.Core.Events;
using UnityEngine;

namespace Tartisians.Gameplay.Events
{
    /// <summary>적이 사망한 순간 발행. XP 젬 스폰·점수·통계가 구독한다.</summary>
    public struct EnemyDiedEvent : IEvent
    {
        public Vector3 Position;
        public int XpReward;
    }

    /// <summary>적이 데미지를 받은 순간 발행. 데미지 숫자·임팩트 VFX가 구독한다(손맛).</summary>
    public struct EnemyHitEvent : IEvent
    {
        public Vector3 Position;
        public float Damage;
        public bool Lethal; // 이 타격으로 사망했는가(치명타는 사망 VFX가 처리)
    }

    /// <summary>플레이어가 경험치를 획득했을 때 발행.</summary>
    public struct XpCollectedEvent : IEvent
    {
        public int Amount;
    }

    /// <summary>플레이어 레벨업 시 발행.</summary>
    public struct LevelUpEvent : IEvent
    {
        public int NewLevel;
    }
}
