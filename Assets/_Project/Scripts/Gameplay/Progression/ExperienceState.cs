using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 경험치/레벨의 순수 상태. EditMode에서 단위 테스트한다.
    /// 다음 레벨 요구량은 (base + (level-1)*step)로 선형 증가한다.
    /// </summary>
    public sealed class ExperienceState
    {
        readonly int _baseXp;
        readonly int _step;

        public int Level { get; private set; } = 1;
        public int CurrentXp { get; private set; }
        public int XpToNext { get; private set; }

        public ExperienceState(int baseXp = 5, int step = 3)
        {
            _baseXp = Mathf.Max(1, baseXp);
            _step = Mathf.Max(0, step);
            XpToNext = RequiredFor(Level);
        }

        int RequiredFor(int level) => _baseXp + (level - 1) * _step;

        /// <summary>XP를 더하고 발생한 레벨업 횟수를 반환한다.</summary>
        public int AddXp(int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            CurrentXp += amount;
            int levelUps = 0;
            while (CurrentXp >= XpToNext)
            {
                CurrentXp -= XpToNext;
                Level++;
                levelUps++;
                XpToNext = RequiredFor(Level);
            }

            return levelUps;
        }
    }
}
