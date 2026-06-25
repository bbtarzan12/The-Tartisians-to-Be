using System.Collections.Generic;
using Tartisians.Core.Events;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Events;
using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 한 판의 진행을 관리한다: RunStats 초기화·등록, XP 수집→레벨업→업그레이드.
    /// M5에서는 레벨업 시 후보 중 첫 번째를 자동 적용한다(M6에서 UI 선택으로 대체).
    /// </summary>
    public sealed class ProgressionSystem : MonoBehaviour
    {
        [SerializeField] PlayerDefinition _player;
        [SerializeField] WeaponDefinition _weapon;
        [SerializeField] UpgradeDefinition[] _upgrades;
        [SerializeField] int _choiceCount = 3;

        ExperienceState _xp;
        RunStats _stats;
        EventBinding<XpCollectedEvent> _xpBinding;
        readonly List<int> _pickBuffer = new();

        public ExperienceState Experience => _xp;
        public RunStats Stats => _stats;

        void Awake()
        {
            _xp = new ExperienceState();
            _stats = new RunStats();
            _stats.InitFrom(_player, _weapon);
            ServiceLocator.Register(_stats);
        }

        void OnEnable()
        {
            _xpBinding = new EventBinding<XpCollectedEvent>(OnXpCollected);
            EventBus<XpCollectedEvent>.Register(_xpBinding);
        }

        void OnDisable()
        {
            if (_xpBinding != null)
            {
                EventBus<XpCollectedEvent>.Deregister(_xpBinding);
            }
        }

        void OnXpCollected(XpCollectedEvent e)
        {
            int levelUps = _xp.AddXp(e.Amount);
            for (int i = 0; i < levelUps; i++)
            {
                HandleLevelUp();
            }
        }

        void HandleLevelUp()
        {
            List<UpgradeDefinition> choices = RollChoices();
            if (choices.Count > 0)
            {
                ApplyUpgrade(choices[0]); // M5: 자동 선택. M6에서 UI 3택으로 대체.
            }

            EventBus<LevelUpEvent>.Raise(new LevelUpEvent { NewLevel = _xp.Level });
        }

        public List<UpgradeDefinition> RollChoices()
        {
            var result = new List<UpgradeDefinition>();
            if (_upgrades == null || _upgrades.Length == 0)
            {
                return result;
            }

            UpgradePicker.PickDistinct(_upgrades.Length, _choiceCount, max => Random.Range(0, max), _pickBuffer);
            for (int i = 0; i < _pickBuffer.Count; i++)
            {
                result.Add(_upgrades[_pickBuffer[i]]);
            }

            return result;
        }

        public void ApplyUpgrade(UpgradeDefinition upgrade) => _stats.Apply(upgrade);
    }
}
