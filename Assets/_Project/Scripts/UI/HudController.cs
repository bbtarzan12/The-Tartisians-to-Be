using System.Collections.Generic;
using Tartisians.Core.Feedback;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Flow;
using Tartisians.Gameplay.Progression;
using Tartisians.Gameplay.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Tartisians.UI
{
    /// <summary>
    /// UI Toolkit HUD를 코드로 구성하고 USS(`Hud.uss`)로 스타일링한다. 상단 레벨/XP/스탯,
    /// 하단 보유현황/HP, 레벨업 3택 카드(아이콘·종류·진화 강조), 결과 요약 화면, 피격 비네트를 담당한다.
    /// 값은 GameDirector·ProgressionSystem·Health에서 읽는다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] StyleSheet _styles;

        UIDocument _doc;
        GameDirector _director;
        ProgressionSystem _progression;
        Health _playerHealth;
        BuildState _build;

        Label _lvBadge;
        Label _killsLabel;
        Label _timerLabel;
        VisualElement _xpFill;
        Label _xpText;
        VisualElement _hpFill;
        Label _hpText;
        VisualElement _loadout;
        VisualElement _vignette;
        VisualElement _overlay;
        Label _overlayTitle;
        VisualElement _overlayBody;

        DangerMeter _danger;
        bool _hitSubscribed;
        int _loadoutSig = -1;
        readonly List<WeaponInstance> _cdWeapons = new();   // 쿨다운 표시 대상(보유 무기)
        readonly List<RadialCooldown> _cdElements = new();  // 대응하는 방사형 오버레이
        const float DangerDecayRate = 6f;
        const float DangerHitScale = 0.5f;
        const float VignetteMaxAlpha = 0.85f;

        static readonly Color HpHigh = new Color(0.4f, 0.85f, 0.35f);
        static readonly Color HpMid = new Color(1f, 0.65f, 0.2f);
        static readonly Color HpLow = new Color(0.9f, 0.27f, 0.22f);

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

            EnsureHitSubscription();
            RefreshValues();
            RefreshPhase();
            RefreshVignette();
        }

        void OnDisable()
        {
            if (_hitSubscribed && _playerHealth != null)
            {
                _playerHealth.Damaged -= OnPlayerDamaged;
            }

            _hitSubscribed = false;
        }

        // ─────────────────────────── 구성 ───────────────────────────
        void Build(VisualElement root)
        {
            root.Clear();
            if (_styles != null)
            {
                root.styleSheets.Add(_styles);
            }

            root.AddToClassList("hud");

            // 피격 비네트
            _vignette = new VisualElement();
            _vignette.AddToClassList("vignette");
            _vignette.pickingMode = PickingMode.Ignore;
            root.Add(_vignette);

            // 상단: 레벨 + XP + 스탯
            var topbar = new VisualElement();
            topbar.AddToClassList("topbar");
            topbar.pickingMode = PickingMode.Ignore;

            var xprow = new VisualElement();
            xprow.AddToClassList("xprow");
            _lvBadge = new Label("Lv 1");
            _lvBadge.AddToClassList("lvbadge");
            xprow.Add(_lvBadge);
            xprow.Add(MakeBar("xp-fill", null, out _xpFill, out _xpText));
            topbar.Add(xprow);

            var statsrow = new VisualElement();
            statsrow.AddToClassList("statsrow");
            _killsLabel = new Label("0");
            _timerLabel = new Label("05:00");
            statsrow.Add(MakeChip(new Color(0.9f, 0.3f, 0.25f), _killsLabel, false));
            statsrow.Add(MakeChip(new Color(1f, 0.81f, 0.32f), _timerLabel, true));
            topbar.Add(statsrow);
            root.Add(topbar);

            // 하단: 보유현황 + HP
            var bottom = new VisualElement();
            bottom.AddToClassList("bottom");
            bottom.pickingMode = PickingMode.Ignore;

            _loadout = new VisualElement();
            _loadout.AddToClassList("loadout");
            bottom.Add(_loadout);

            var hprow = new VisualElement();
            hprow.AddToClassList("hprow");
            var heart = new Label("HP");
            heart.AddToClassList("heart");
            hprow.Add(heart);
            VisualElement hpBar = MakeBar(null, "hp", out _hpFill, out _hpText);
            _hpText.AddToClassList("hp-text");
            hprow.Add(hpBar);
            bottom.Add(hprow);
            root.Add(bottom);

            // 오버레이(레벨업/결과)
            _overlay = new VisualElement();
            _overlay.AddToClassList("overlay");
            _overlayTitle = new Label();
            _overlayTitle.AddToClassList("otitle");
            _overlayBody = new VisualElement();
            _overlayBody.style.alignItems = Align.Center;
            _overlay.Add(_overlayTitle);
            _overlay.Add(_overlayBody);
            root.Add(_overlay);
        }

        VisualElement MakeBar(string fillClass, string barClass, out VisualElement fill, out Label text)
        {
            // 텍스트는 바 안에 두되(채움 위 중앙), 바 높이를 숫자가 들어갈 만큼 확보해 클립을 피한다.
            var bar = new VisualElement();
            bar.AddToClassList("bar");
            if (barClass != null)
            {
                bar.AddToClassList(barClass);
            }

            fill = new VisualElement();
            fill.AddToClassList("bar-fill");
            if (fillClass != null)
            {
                fill.AddToClassList(fillClass);
            }

            text = new Label();
            text.AddToClassList("bar-text");
            bar.Add(fill);
            bar.Add(text);
            return bar;
        }

        static VisualElement MakeChip(Color pipColor, Label label, bool timer)
        {
            var chip = new VisualElement();
            chip.AddToClassList("chip");
            if (timer)
            {
                chip.AddToClassList("chip-timer");
            }

            var pip = new VisualElement();
            pip.AddToClassList("chip-pip");
            pip.style.backgroundColor = pipColor;
            chip.Add(pip);
            chip.Add(label);
            return chip;
        }

        // ─────────────────────────── 갱신 ───────────────────────────
        void RefreshValues()
        {
            if (_playerHealth != null)
            {
                float max = Mathf.Max(1f, _playerHealth.Max);
                float ratio = Mathf.Clamp01(_playerHealth.Current / max);
                _hpFill.style.width = Length.Percent(ratio * 100f);
                _hpFill.style.backgroundColor = HpColor(ratio);
                _hpText.text = $"{Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(max)}";
            }

            if (_progression?.Experience != null)
            {
                ExperienceState xp = _progression.Experience;
                _lvBadge.text = $"Lv {xp.Level}";
                float next = Mathf.Max(1f, xp.XpToNext);
                _xpFill.style.width = Length.Percent(Mathf.Clamp01(xp.CurrentXp / next) * 100f);
                _xpText.text = $"{xp.CurrentXp} / {xp.XpToNext}";
            }

            if (_director != null)
            {
                float t = _director.TimeRemaining;
                int m = Mathf.FloorToInt(t / 60f);
                int s = Mathf.FloorToInt(t % 60f);
                _timerLabel.text = $"{m:00}:{s:00}";
                _killsLabel.text = _director.Kills.ToString();
            }

            RefreshLoadout();
            RefreshCooldowns();
        }

        void RefreshLoadout()
        {
            if (_build == null)
            {
                _build = _progression != null ? _progression.Build : null;
                if (_build == null)
                {
                    return;
                }
            }

            int sig = LoadoutSignature();
            if (sig == _loadoutSig)
            {
                return;
            }

            _loadoutSig = sig;
            _loadout.Clear();
            _cdWeapons.Clear();
            _cdElements.Clear();
            for (int i = 0; i < _build.Weapons.Count; i++)
            {
                WeaponInstance w = _build.Weapons[i];
                var cd = new RadialCooldown();
                cd.AddToClassList("slot-cd");
                _loadout.Add(MakeSlot(w.Def.Color, w.Level, true, cd));
                _cdWeapons.Add(w);
                _cdElements.Add(cd);
            }

            if (_build.Passives.Count > 0)
            {
                var sep = new VisualElement();
                sep.AddToClassList("sep");
                _loadout.Add(sep);
            }

            for (int i = 0; i < _build.Passives.Count; i++)
            {
                PassiveOwned p = _build.Passives[i];
                _loadout.Add(MakeSlot(ProgressionPalette.PassiveColor(p.Def.Kind), p.Level, false));
            }
        }

        int LoadoutSignature()
        {
            int h = 17;
            h = h * 31 + _build.Weapons.Count;
            h = h * 31 + _build.Passives.Count;
            for (int i = 0; i < _build.Weapons.Count; i++)
            {
                h = h * 31 + _build.Weapons[i].Level;
                h = h * 31 + _build.Weapons[i].Def.GetHashCode();
            }

            for (int i = 0; i < _build.Passives.Count; i++)
            {
                h = h * 31 + _build.Passives[i].Level;
            }

            return h;
        }

        static VisualElement MakeSlot(Color accent, int level, bool weaponShape, VisualElement cooldown = null)
        {
            var slot = new VisualElement();
            slot.AddToClassList("slot");
            var pip = new VisualElement();
            pip.AddToClassList("pip");
            if (!weaponShape)
            {
                pip.AddToClassList("pip-round");
            }

            pip.style.backgroundColor = accent;
            slot.Add(pip);

            if (cooldown != null)
            {
                slot.Add(cooldown); // pip 위, 레벨 라벨 아래(쿨다운 오버레이)
            }

            if (level > 0)
            {
                var lv = new Label(level.ToString());
                lv.AddToClassList("slot-lv");
                slot.Add(lv);
            }

            return slot;
        }

        // 보유 무기의 발사 타이머를 읽어 방사형 쿨다운(잔여 = 1 - 진행도)을 매 프레임 갱신.
        void RefreshCooldowns()
        {
            if (_build == null || _cdWeapons.Count == 0)
            {
                return;
            }

            PassiveModifiers mods = _build.ComputeModifiers();
            for (int i = 0; i < _cdWeapons.Count; i++)
            {
                WeaponInstance w = _cdWeapons[i];
                float interval = w.Compute(mods).FireInterval;
                float ready = interval > 0.0001f ? Mathf.Clamp01(w.FireTimer / interval) : 1f;
                _cdElements[i].Remaining = 1f - ready;
            }
        }

        static Color HpColor(float r)
        {
            return r > 0.5f
                ? Color.Lerp(HpMid, HpHigh, (r - 0.5f) * 2f)
                : Color.Lerp(HpLow, HpMid, r * 2f);
        }

        // ─────────────────────────── 피격 비네트 ───────────────────────────
        void EnsureHitSubscription()
        {
            if (_hitSubscribed || _playerHealth == null)
            {
                return;
            }

            _playerHealth.Damaged += OnPlayerDamaged;
            _hitSubscribed = true;
        }

        void OnPlayerDamaged(float amount) => _danger.Hit(Mathf.Clamp01(amount * DangerHitScale));

        void RefreshVignette()
        {
            if (_vignette == null)
            {
                return;
            }

            _danger.Decay(Time.unscaledDeltaTime, DangerDecayRate);
            _vignette.style.opacity = _danger.Value * VignetteMaxAlpha;
        }

        // ─────────────────────────── 페이즈 ───────────────────────────
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
                    ShowEnd(false);
                    break;
                case GameDirector.Phase.Victory:
                    ShowEnd(true);
                    break;
            }
        }

        void ShowLevelUp()
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlayTitle.text = "레벨 업!  <color=#FFCF52>업그레이드 선택</color>";
            _overlayBody.Clear();

            var cards = new VisualElement();
            cards.AddToClassList("cards");

            IReadOnlyList<UpgradeOption> choices = _director.Choices;
            for (int i = 0; i < choices.Count; i++)
            {
                int index = i;
                cards.Add(MakeCard(choices[i], () => _director.SelectUpgrade(index)));
            }

            _overlayBody.Add(cards);
        }

        VisualElement MakeCard(UpgradeOption u, System.Action onClick)
        {
            var card = new VisualElement();
            card.AddToClassList("card");
            if (u.IsEvolution)
            {
                card.AddToClassList("card-evo");
                var ribbon = new Label("진화");
                ribbon.AddToClassList("ribbon");
                card.Add(ribbon);
            }
            else
            {
                card.style.borderTopColor = u.Accent;
            }

            var kind = new Label(u.KindLabel);
            kind.AddToClassList("kind");
            card.Add(kind);

            // 아이콘(무기=사각, 패시브=원) — 색으로 구분
            var icon = new VisualElement();
            icon.AddToClassList("card-icon");
            var pip = new VisualElement();
            pip.AddToClassList("card-pip");
            if (!u.IsWeapon)
            {
                pip.AddToClassList("card-pip-round");
            }

            pip.style.backgroundColor = u.Accent;
            icon.Add(pip);
            card.Add(icon);

            var nm = new Label(u.Title);
            nm.AddToClassList("nm");
            card.Add(nm);

            var lvtag = new Label(LevelTagText(u));
            lvtag.AddToClassList("lvtag");
            lvtag.AddToClassList(LevelTagClass(u));
            card.Add(lvtag);

            var desc = new Label(u.Detail);
            desc.AddToClassList("desc");
            card.Add(desc);

            card.RegisterCallback<ClickEvent>(_ => onClick());
            return card;
        }

        static string LevelTagText(UpgradeOption u)
        {
            if (u.IsEvolution)
            {
                return "진화!";
            }

            if (u.KindLabel != null && u.KindLabel.Contains("신규"))
            {
                return "신규 획득";
            }

            return $"Lv {Mathf.Max(2, u.Level)}";
        }

        static string LevelTagClass(UpgradeOption u)
        {
            if (u.IsEvolution)
            {
                return "lvtag-evo";
            }

            return u.KindLabel != null && u.KindLabel.Contains("신규") ? "lvtag-new" : "lvtag-up";
        }

        void ShowEnd(bool win)
        {
            _overlay.style.display = DisplayStyle.Flex;
            _overlayTitle.text = "";
            _overlayBody.Clear();

            var big = new Label(win ? "생존 성공!" : "게임 오버");
            big.AddToClassList("big");
            big.AddToClassList(win ? "big-win" : "big-lose");
            _overlayBody.Add(big);

            _overlayBody.Add(BuildSummary());

            var restart = new Label("다시 시작");
            restart.AddToClassList("btn");
            restart.RegisterCallback<ClickEvent>(_ => Restart());
            _overlayBody.Add(restart);
        }

        VisualElement BuildSummary()
        {
            var box = new VisualElement();
            box.AddToClassList("summary");

            float elapsed = _director != null ? _director.SurvivalDuration - _director.TimeRemaining : 0f;
            elapsed = Mathf.Max(0f, elapsed);
            int m = Mathf.FloorToInt(elapsed / 60f);
            int s = Mathf.FloorToInt(elapsed % 60f);
            int kills = _director != null ? _director.Kills : 0;
            int level = _progression?.Experience != null ? _progression.Experience.Level : 1;

            box.Add(MakeStatRow("생존 시간", $"{m:00}:{s:00}"));
            box.Add(MakeStatRow("처치", kills.ToString()));
            box.Add(MakeStatRow("도달 레벨", $"Lv {level}"));

            var lab = new Label("최종 빌드");
            lab.AddToClassList("build-lab");
            box.Add(lab);

            var icons = new VisualElement();
            icons.AddToClassList("build-icons");
            if (_build == null && _progression != null)
            {
                _build = _progression.Build;
            }

            if (_build != null)
            {
                for (int i = 0; i < _build.Weapons.Count; i++)
                {
                    WeaponInstance w = _build.Weapons[i];
                    icons.Add(MakeSlot(w.Def.Color, w.Level, true));
                }

                if (_build.Passives.Count > 0)
                {
                    var sep = new VisualElement();
                    sep.AddToClassList("sep");
                    icons.Add(sep);
                }

                for (int i = 0; i < _build.Passives.Count; i++)
                {
                    PassiveOwned p = _build.Passives[i];
                    icons.Add(MakeSlot(ProgressionPalette.PassiveColor(p.Def.Kind), p.Level, false));
                }
            }

            box.Add(icons);
            return box;
        }

        static VisualElement MakeStatRow(string k, string v)
        {
            var row = new VisualElement();
            row.AddToClassList("srow");
            var kl = new Label(k);
            kl.AddToClassList("srow-k");
            var vl = new Label(v);
            vl.AddToClassList("srow-v");
            row.Add(kl);
            row.Add(vl);
            return row;
        }

        static void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>
        /// 스킬 쿨다운형 방사형 오버레이. 잔여 쿨다운(0=발동가능, 1=막 발동)을 12시부터 시계방향으로
        /// 어둡게 덮는 파이를 직접 메시로 그린다. 슬롯의 둥근 모서리에 클립되도록 overflow:hidden(USS).
        /// </summary>
        sealed class RadialCooldown : VisualElement
        {
            static readonly Color32 OverlayColor = new Color32(0, 0, 0, 150);
            float _remaining;

            public RadialCooldown()
            {
                pickingMode = PickingMode.Ignore;
                generateVisualContent += OnGenerateVisualContent;
            }

            public float Remaining
            {
                set
                {
                    float v = Mathf.Clamp01(value);
                    if (Mathf.Abs(v - _remaining) < 0.002f)
                    {
                        return;
                    }

                    _remaining = v;
                    MarkDirtyRepaint();
                }
            }

            void OnGenerateVisualContent(MeshGenerationContext mgc)
            {
                if (_remaining <= 0.002f)
                {
                    return;
                }

                Rect rect = contentRect;
                if (rect.width <= 0f || rect.height <= 0f)
                {
                    return;
                }

                float cx = rect.width * 0.5f;
                float cy = rect.height * 0.5f;
                float radius = Mathf.Sqrt(rect.width * rect.width + rect.height * rect.height) * 0.5f + 1f;
                int segments = Mathf.Max(1, Mathf.CeilToInt(_remaining * 48f));
                float start = -Mathf.PI * 0.5f;            // 12시 방향
                float sweep = _remaining * Mathf.PI * 2f;  // 시계방향 잔여량

                MeshWriteData mwd = mgc.Allocate(segments + 2, segments * 3);
                mwd.SetNextVertex(new Vertex { position = new Vector3(cx, cy, Vertex.nearZ), tint = OverlayColor });
                for (int i = 0; i <= segments; i++)
                {
                    float a = start + sweep * (i / (float)segments);
                    float x = cx + Mathf.Cos(a) * radius;
                    float y = cy + Mathf.Sin(a) * radius;
                    mwd.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = OverlayColor });
                }

                for (int i = 0; i < segments; i++)
                {
                    mwd.SetNextIndex(0);
                    mwd.SetNextIndex((ushort)(1 + i));
                    mwd.SetNextIndex((ushort)(2 + i));
                }
            }
        }
    }
}
