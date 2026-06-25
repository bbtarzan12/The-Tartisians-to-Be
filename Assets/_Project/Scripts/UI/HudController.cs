using System.Collections.Generic;
using Tartisians.Data;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Flow;
using Tartisians.Gameplay.Progression;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Tartisians.UI
{
    /// <summary>
    /// UI Toolkit HUD를 코드로 구성한다(HP/XP/타이머/킬/레벨). 레벨업 시 3택 카드 오버레이,
    /// 종료 시 승리/패배 패널을 표시한다. 값은 GameDirector·ProgressionSystem·Health에서 읽는다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        UIDocument _doc;
        GameDirector _director;
        ProgressionSystem _progression;
        Health _playerHealth;

        Label _levelLabel;
        Label _timerLabel;
        Label _killsLabel;
        ProgressBar _hpBar;
        ProgressBar _xpBar;
        VisualElement _overlay;
        Label _overlayTitle;
        VisualElement _cardRow;

        bool _built;
        GameDirector.Phase _lastPhase = GameDirector.Phase.Playing;

        void Awake()
        {
            _doc = GetComponent<UIDocument>();
            _director = FindAnyObjectByType<GameDirector>();
            _progression = FindAnyObjectByType<ProgressionSystem>();

            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null)
            {
                p.TryGetComponent(out _playerHealth);
            }
        }

        void Update()
        {
            if (!_built)
            {
                if (_doc == null || _doc.rootVisualElement == null)
                {
                    return;
                }

                Build(_doc.rootVisualElement);
                _built = true;
            }

            RefreshValues();
            RefreshPhase();
        }

        void Build(VisualElement root)
        {
            root.Clear();

            var topBar = new VisualElement();
            topBar.style.flexDirection = FlexDirection.Row;
            topBar.style.justifyContent = Justify.SpaceBetween;
            topBar.style.paddingTop = 8;
            topBar.style.paddingLeft = 12;
            topBar.style.paddingRight = 12;

            _levelLabel = MakeLabel("Lv 1");
            _timerLabel = MakeLabel("05:00");
            _killsLabel = MakeLabel("Kills 0");
            topBar.Add(_levelLabel);
            topBar.Add(_timerLabel);
            topBar.Add(_killsLabel);
            root.Add(topBar);

            _hpBar = new ProgressBar { title = "HP", lowValue = 0f, highValue = 100f, value = 100f };
            _hpBar.style.marginLeft = 12;
            _hpBar.style.marginRight = 12;
            _hpBar.style.marginTop = 4;
            root.Add(_hpBar);

            _xpBar = new ProgressBar { title = "XP", lowValue = 0f, highValue = 1f, value = 0f };
            _xpBar.style.marginLeft = 12;
            _xpBar.style.marginRight = 12;
            root.Add(_xpBar);

            _overlay = new VisualElement();
            _overlay.style.position = Position.Absolute;
            _overlay.style.left = 0;
            _overlay.style.right = 0;
            _overlay.style.top = 0;
            _overlay.style.bottom = 0;
            _overlay.style.alignItems = Align.Center;
            _overlay.style.justifyContent = Justify.Center;
            _overlay.style.backgroundColor = new Color(0f, 0f, 0f, 0.7f);
            _overlay.style.display = DisplayStyle.None;

            _overlayTitle = MakeLabel("");
            _overlayTitle.style.fontSize = 36;
            _overlayTitle.style.marginBottom = 16;
            _overlay.Add(_overlayTitle);

            _cardRow = new VisualElement();
            _cardRow.style.flexDirection = FlexDirection.Row;
            _overlay.Add(_cardRow);

            root.Add(_overlay);
        }

        static Label MakeLabel(string text)
        {
            var l = new Label(text);
            l.style.color = Color.white;
            l.style.fontSize = 20;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            return l;
        }

        void RefreshValues()
        {
            if (_playerHealth != null)
            {
                _hpBar.highValue = Mathf.Max(1f, _playerHealth.Max);
                _hpBar.value = _playerHealth.Current;
                _hpBar.title = $"HP {Mathf.CeilToInt(_playerHealth.Current)}/{Mathf.CeilToInt(_playerHealth.Max)}";
            }

            if (_progression?.Experience != null)
            {
                ExperienceState xp = _progression.Experience;
                _levelLabel.text = $"Lv {xp.Level}";
                _xpBar.highValue = Mathf.Max(1f, xp.XpToNext);
                _xpBar.value = xp.CurrentXp;
                _xpBar.title = $"XP {xp.CurrentXp}/{xp.XpToNext}";
            }

            if (_director != null)
            {
                float t = _director.TimeRemaining;
                int m = Mathf.FloorToInt(t / 60f);
                int s = Mathf.FloorToInt(t % 60f);
                _timerLabel.text = $"{m:00}:{s:00}";
                _killsLabel.text = $"Kills {_director.Kills}";
            }
        }

        void RefreshPhase()
        {
            if (_director == null)
            {
                return;
            }

            GameDirector.Phase phase = _director.Current;
            if (phase == _lastPhase)
            {
                return;
            }

            _lastPhase = phase;
            switch (phase)
            {
                case GameDirector.Phase.Playing:
                    _overlay.style.display = DisplayStyle.None;
                    break;
                case GameDirector.Phase.LevelUp:
                    ShowLevelUp();
                    break;
                case GameDirector.Phase.GameOver:
                    ShowEnd("게임 오버");
                    break;
                case GameDirector.Phase.Victory:
                    ShowEnd("생존 성공!");
                    break;
            }
        }

        void ShowLevelUp()
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlayTitle.text = "레벨 업! — 업그레이드 선택";
            _cardRow.Clear();

            IReadOnlyList<UpgradeDefinition> choices = _director.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                UpgradeDefinition u = choices[i];
                var btn = new Button(() => _director.SelectUpgrade(index))
                {
                    text = $"{u.Title}\n(+{u.Value})"
                };
                btn.style.width = 160;
                btn.style.height = 90;
                btn.style.marginLeft = 8;
                btn.style.marginRight = 8;
                btn.style.whiteSpace = WhiteSpace.Normal;
                _cardRow.Add(btn);
            }
        }

        void ShowEnd(string message)
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlayTitle.text = message;
            _cardRow.Clear();

            var restart = new Button(Restart) { text = "다시 시작" };
            restart.style.width = 160;
            restart.style.height = 50;
            _cardRow.Add(restart);
        }

        static void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
