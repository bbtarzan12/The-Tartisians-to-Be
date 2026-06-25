using System.Collections.Generic;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>활성 적 목록. 시뮬레이션·타게팅·스포너가 공유한다.</summary>
    public sealed class EnemyRegistry
    {
        readonly List<Enemy> _active = new(256);

        public IReadOnlyList<Enemy> Active => _active;
        public int Count => _active.Count;

        public void Add(Enemy enemy)
        {
            if (!_active.Contains(enemy))
            {
                _active.Add(enemy);
            }
        }

        public void Remove(Enemy enemy) => _active.Remove(enemy);

        public void Clear() => _active.Clear();
    }
}
