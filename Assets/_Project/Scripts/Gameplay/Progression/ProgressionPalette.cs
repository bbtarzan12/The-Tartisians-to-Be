using Tartisians.Data;
using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>패시브 종류별 강조색(아트 아이콘이 없어 색으로 구분). 카드·보유현황 HUD 공용.</summary>
    public static class ProgressionPalette
    {
        public static Color PassiveColor(PassiveKind kind)
        {
            switch (kind)
            {
                case PassiveKind.Might: return new Color(1f, 0.45f, 0.45f);
                case PassiveKind.Cooldown: return new Color(0.55f, 0.8f, 1f);
                case PassiveKind.Area: return new Color(0.7f, 0.55f, 1f);
                case PassiveKind.Amount: return new Color(1f, 0.8f, 0.4f);
                case PassiveKind.ProjectileSpeed: return new Color(0.5f, 1f, 0.7f);
                case PassiveKind.Magnet: return new Color(0.6f, 0.85f, 1f);
                case PassiveKind.MaxHealth: return new Color(1f, 0.55f, 0.7f);
                case PassiveKind.MoveSpeed: return new Color(0.8f, 1f, 0.55f);
                default: return Color.gray;
            }
        }
    }
}
