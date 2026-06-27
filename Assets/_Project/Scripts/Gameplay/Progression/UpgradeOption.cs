using System;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 레벨업 카드 1장의 런타임 표현. 무기/패시브/진화/스탯을 동일 인터페이스로 다룬다 —
    /// HUD는 Title/Detail만 표시하고 선택 시 Apply를 호출한다(구체 종류와 분리).
    /// </summary>
    public sealed class UpgradeOption
    {
        public string Title { get; }
        public string Detail { get; }
        public Action Apply { get; }

        public UpgradeOption(string title, string detail, Action apply)
        {
            Title = title;
            Detail = detail;
            Apply = apply;
        }
    }
}
