using Tartisians.Data;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 한 판 동안의 플레이어 가변 스탯(이동/체력/자석). 무기 스탯은 더 이상 여기 없고
    /// <see cref="Tartisians.Gameplay.Weapons.WeaponInstance"/>가 보유한다(M8 빌드 다양성).
    /// 패시브로 인한 플레이어 스탯 변화는 ProgressionSystem이 재계산해 여기에 반영한다.
    /// </summary>
    public sealed class RunStats
    {
        public float MoveSpeed;
        public float MaxHealth;
        public float PickupRadius;

        public void InitFrom(PlayerDefinition player)
        {
            if (player != null)
            {
                MoveSpeed = player.MoveSpeed;
                MaxHealth = player.MaxHealth;
                PickupRadius = player.PickupRadius;
            }
        }
    }
}
