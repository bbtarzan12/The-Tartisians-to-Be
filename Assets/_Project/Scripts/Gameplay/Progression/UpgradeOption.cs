using System;
using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 레벨업 카드 1장의 런타임 표현. 무기/패시브/진화/스탯을 동일 인터페이스로 다룬다 —
    /// HUD는 표시 메타데이터(종류·강조색·레벨)로 카드를 꾸미고 선택 시 Apply를 호출한다.
    /// </summary>
    public sealed class UpgradeOption
    {
        public string Title { get; }
        public string Detail { get; }
        public Action Apply { get; }

        public OptionKind Kind { get; }
        public Color Accent { get; }      // 아이콘/테두리 강조색(무기색 또는 패시브 종류색)
        public int Level { get; }         // 적용 후 레벨(>1이면 카드에 "Lv n" 표시)
        public string KindLabel { get; }  // "무기 · 신규" / "패시브 · Lv 업" / "진화" 등
        public bool IsWeapon { get; }     // 아이콘 모양 구분(무기=사각, 패시브=원)

        public UpgradeOption(
            string title, string detail, Action apply,
            OptionKind kind, Color accent, int level, string kindLabel, bool isWeapon)
        {
            Title = title;
            Detail = detail;
            Apply = apply;
            Kind = kind;
            Accent = accent;
            Level = level;
            KindLabel = kindLabel;
            IsWeapon = isWeapon;
        }

        public bool IsEvolution => Kind == OptionKind.Evolution;
    }
}
