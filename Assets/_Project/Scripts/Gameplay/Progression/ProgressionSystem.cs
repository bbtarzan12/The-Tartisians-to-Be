using System.Collections.Generic;
using Tartisians.Core.Events;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Events;
using Tartisians.Gameplay.Weapons;
using UnityEngine;

namespace Tartisians.Gameplay.Progression
{
    /// <summary>
    /// 한 판의 진행을 관리한다: RunStats/BuildState 초기화·등록, XP 수집→레벨업 알림,
    /// 레벨업 후보(동적 풀) 생성·적용. 후보는 무기 신규/레벨업·패시브 신규/레벨업·진화를
    /// 혼합해 매번 다른 빌드가 나오게 한다(M8).
    /// </summary>
    public sealed class ProgressionSystem : MonoBehaviour
    {
        [SerializeField] PlayerDefinition _player;
        [SerializeField] WeaponDefinition _startingWeapon;
        [SerializeField] WeaponDefinition[] _weaponCatalog;
        [SerializeField] PassiveItemDefinition[] _passiveCatalog;
        [SerializeField] int _choiceCount = 3;
        [SerializeField] int _maxWeapons = 6;
        [SerializeField] int _maxPassives = 6;

        ExperienceState _xp;
        RunStats _stats;
        BuildState _build;
        EventBinding<XpCollectedEvent> _xpBinding;
        readonly List<OptionDescriptor> _descBuffer = new();
        readonly List<int> _pickBuffer = new();

        public ExperienceState Experience => _xp;
        public RunStats Stats => _stats;
        public BuildState Build => _build;

        void Awake()
        {
            _xp = new ExperienceState();
            _stats = new RunStats();
            _stats.InitFrom(_player);

            _build = new BuildState { MaxWeapons = _maxWeapons, MaxPassives = _maxPassives };
            if (_startingWeapon != null)
            {
                _build.AddWeapon(_startingWeapon);
            }

            ServiceLocator.Register(_stats);
            ServiceLocator.Register(_build);
            RecomputePlayerStats();
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
                EventBus<LevelUpEvent>.Raise(new LevelUpEvent { NewLevel = _xp.Level });
            }
        }

        /// <summary>이번 레벨업의 카드 후보를 동적 풀에서 중복 없이 뽑아 반환한다.</summary>
        public List<UpgradeOption> RollChoices()
        {
            var result = new List<UpgradeOption>();
            if (_build == null)
            {
                return result;
            }

            UpgradePool.Generate(_build, _weaponCatalog, _passiveCatalog, _descBuffer);
            if (_descBuffer.Count == 0)
            {
                return result;
            }

            UpgradePicker.PickDistinct(_descBuffer.Count, _choiceCount, max => Random.Range(0, max), _pickBuffer);
            for (int i = 0; i < _pickBuffer.Count; i++)
            {
                result.Add(ToOption(_descBuffer[_pickBuffer[i]]));
            }

            return result;
        }

        public void ApplyUpgrade(UpgradeOption option) => option?.Apply?.Invoke();

        UpgradeOption ToOption(OptionDescriptor d)
        {
            switch (d.Kind)
            {
                case OptionKind.NewWeapon:
                {
                    WeaponDefinition def = d.Weapon;
                    return new UpgradeOption(def.DisplayName, "새 무기", () => _build.AddWeapon(def));
                }
                case OptionKind.LevelWeapon:
                {
                    WeaponInstance target = d.WeaponTarget;
                    return new UpgradeOption(d.Weapon.DisplayName, $"Lv {d.ResultLevel}", () => target.LevelUp());
                }
                case OptionKind.NewPassive:
                {
                    PassiveItemDefinition def = d.Passive;
                    return new UpgradeOption(def.DisplayName, "새 패시브", () =>
                    {
                        _build.AddPassive(def);
                        RecomputePlayerStats();
                    });
                }
                case OptionKind.LevelPassive:
                {
                    PassiveItemDefinition def = d.Passive;
                    return new UpgradeOption(def.DisplayName, $"Lv {d.ResultLevel}", () =>
                    {
                        _build.FindPassive(def)?.LevelUp();
                        RecomputePlayerStats();
                    });
                }
                case OptionKind.Evolution:
                {
                    WeaponInstance target = d.WeaponTarget;
                    return new UpgradeOption($"진화: {d.Weapon.DisplayName}", "진화", () => _build.Evolve(target));
                }
            }

            return new UpgradeOption("?", "", null);
        }

        // 패시브로 인한 플레이어 스탯(이동/체력/자석)을 기본값 위에 재계산해 RunStats에 반영.
        void RecomputePlayerStats()
        {
            if (_player == null || _stats == null)
            {
                return;
            }

            float move = _player.MoveSpeed;
            float maxHp = _player.MaxHealth;
            float magnetPct = 0f;

            List<PassiveOwned> passives = _build.Passives;
            for (int i = 0; i < passives.Count; i++)
            {
                PassiveOwned p = passives[i];
                float v = p.Def.ValueAtLevel(p.Level);
                switch (p.Def.Kind)
                {
                    case PassiveKind.MoveSpeed: move += v; break;
                    case PassiveKind.MaxHealth: maxHp += v; break;
                    case PassiveKind.Magnet: magnetPct += v; break;
                }
            }

            _stats.MoveSpeed = move;
            _stats.MaxHealth = maxHp;
            _stats.PickupRadius = _player.PickupRadius * (1f + magnetPct);
        }
    }
}
