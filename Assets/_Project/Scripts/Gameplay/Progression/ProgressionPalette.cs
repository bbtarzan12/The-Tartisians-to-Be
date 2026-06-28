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
                case PassiveKind.Magnet: return new Color(0.6f, 0.85f, 1f);
                case PassiveKind.MaxHealth: return new Color(1f, 0.55f, 0.7f);
                case PassiveKind.MoveSpeed: return new Color(0.8f, 1f, 0.55f);
                default: return Color.gray;
            }
        }
    }
}
