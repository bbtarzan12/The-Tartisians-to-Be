using System;
using System.Collections.Generic;
using Tartisians.Core.Events;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Events;
using Tartisians.Gameplay.Progression;
using UnityEngine;

namespace Tartisians.Gameplay.Flow
{
    /// <summary>
    /// 한 판의 흐름을 관리한다: 생존 타이머, 승/패, 레벨업 시 시간정지 + 업그레이드 3택.
    /// HUD는 이 디렉터의 상태/값을 읽어 표시하고, 선택을 SelectUpgrade로 되돌린다.
    /// </summary>
    public sealed class GameDirector : MonoBehaviour
    {
        public enum Phase { Playing, LevelUp, GameOver, Victory }

        [SerializeField] float _survivalDuration = 300f;

        SurvivalClock _clock;
        ProgressionSystem _progression;
        Health _playerHealth;

        int _pendingLevelUps;
        readonly List<UpgradeOption> _choices = new();

        EventBinding<LevelUpEvent> _levelUpBinding;
        EventBinding<EnemyDiedEvent> _enemyDiedBinding;

        public Phase Current { get; private set; } = Phase.Playing;
        public int Kills { get; private set; }
        public float TimeRemaining => _clock != null ? _clock.Remaining : _survivalDuration;
        public float SurvivalDuration => _survivalDuration;
        public IReadOnlyList<UpgradeOption> Choices => _choices;

        /// <summary>상태/표시값이 바뀔 때 발생(HUD 갱신용).</summary>
        public event Action Changed;

        void Awake()
        {
            _clock = new SurvivalClock(_survivalDuration);
            _progression = FindAnyObjectByType<ProgressionSystem>();

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                p.TryGetComponent(out _playerHealth);
            }
        }

        void OnEnable()
        {
            _levelUpBinding = new EventBinding<LevelUpEvent>(OnLevelUp);
            EventBus<LevelUpEvent>.Register(_levelUpBinding);

            _enemyDiedBinding = new EventBinding<EnemyDiedEvent>(OnEnemyDied);
            EventBus<EnemyDiedEvent>.Register(_enemyDiedBinding);

            if (_playerHealth != null)
            {
                _playerHealth.Died += OnPlayerDied;
            }
        }

        void OnDisable()
        {
            if (_levelUpBinding != null)
            {
                EventBus<LevelUpEvent>.Deregister(_levelUpBinding);
            }

            if (_enemyDiedBinding != null)
            {
                EventBus<EnemyDiedEvent>.Deregister(_enemyDiedBinding);
            }

            if (_playerHealth != null)
            {
                _playerHealth.Died -= OnPlayerDied;
            }
        }

        void Update()
        {
            if (Current != Phase.Playing)
            {
                return;
            }

            _clock.Tick(Time.deltaTime);
            Changed?.Invoke();

            if (_clock.IsComplete)
            {
                SetPhase(Phase.Victory);
            }
        }

        void OnEnemyDied(EnemyDiedEvent e)
        {
            Kills++;
            Changed?.Invoke();
        }

        void OnLevelUp(LevelUpEvent e)
        {
            _pendingLevelUps++;
            if (Current == Phase.Playing)
            {
                EnterLevelUp();
            }
        }

        void OnPlayerDied()
        {
            if (Current == Phase.Playing || Current == Phase.LevelUp)
            {
                SetPhase(Phase.GameOver);
            }
        }

        void EnterLevelUp()
        {
            _choices.Clear();
            if (_progression != null)
            {
                _choices.AddRange(_progression.RollChoices());
            }

            if (_choices.Count == 0)
            {
                _pendingLevelUps = 0;
                return;
            }

            SetPhase(Phase.LevelUp);
        }

        /// <summary>HUD가 카드 클릭 시 호출. 선택 적용 후 남은 레벨업이 있으면 다음 3택, 없으면 재개.</summary>
        public void SelectUpgrade(int index)
        {
            if (Current != Phase.LevelUp)
            {
                return;
            }

            if (_progression != null && index >= 0 && index < _choices.Count)
            {
                _progression.ApplyUpgrade(_choices[index]);
            }

            _pendingLevelUps = Mathf.Max(0, _pendingLevelUps - 1);
            if (_pendingLevelUps > 0)
            {
                EnterLevelUp();
            }
            else
            {
                SetPhase(Phase.Playing);
            }
        }

        void SetPhase(Phase phase)
        {
            Current = phase;
            Time.timeScale = phase == Phase.Playing ? 1f : 0f;
            Changed?.Invoke();
        }
    }
}
