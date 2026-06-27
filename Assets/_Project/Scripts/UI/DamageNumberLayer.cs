using System.Collections.Generic;
using Tartisians.Core.Events;
using Tartisians.Core.Feedback;
using Tartisians.Gameplay.Events;
using UnityEngine;
using UnityEngine.UIElements;

namespace Tartisians.UI
{
    /// <summary>
    /// 적 피격 위치에 떠오르는 데미지 숫자를 그린다(손맛). HUD의 UIDocument 패널을 재사용하므로
    /// 별도 PanelSettings/폰트가 필요 없다. Label을 풀링하고 카메라 투영으로 매 프레임 배치한다.
    /// EnemyHitEvent를 구독한다.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class DamageNumberLayer : MonoBehaviour
    {
        [SerializeField] int _maxNumbers = 64;
        [SerializeField] float _lifetime = 0.7f;
        [SerializeField] float _riseWorld = 1.6f;   // 월드 위로 떠오르는 거리
        [SerializeField] float _fadeStart = 0.45f;  // 진행도 이후 페이드
        [SerializeField] int _baseFontSize = 18;
        [SerializeField] float _popScale = 1.6f;
        [SerializeField] float _popTime = 0.18f;

        static readonly Color NormalColor = new Color(1f, 0.95f, 0.6f);
        static readonly Color LethalColor = new Color(1f, 0.55f, 0.2f);

        sealed class Item
        {
            public Label Label;
            public Vector3 World;
            public Color Color;
            public float Age;
            public bool InUse;
        }

        UIDocument _doc;
        VisualElement _layer;
        Camera _cam;
        EventBinding<EnemyHitEvent> _binding;
        readonly List<Item> _items = new();

        void Awake() => _doc = GetComponent<UIDocument>();

        void OnEnable()
        {
            _binding = new EventBinding<EnemyHitEvent>(OnEnemyHit);
            EventBus<EnemyHitEvent>.Register(_binding);
        }

        void OnDisable()
        {
            if (_binding != null)
            {
                EventBus<EnemyHitEvent>.Deregister(_binding);
            }
        }

        bool EnsureLayer()
        {
            if (_layer != null)
            {
                return true;
            }

            VisualElement root = _doc != null ? _doc.rootVisualElement : null;
            if (root == null)
            {
                return false;
            }

            _layer = new VisualElement();
            _layer.style.position = Position.Absolute;
            _layer.style.left = 0;
            _layer.style.right = 0;
            _layer.style.top = 0;
            _layer.style.bottom = 0;
            _layer.pickingMode = PickingMode.Ignore; // 카드/버튼 입력 가로채지 않음
            root.Add(_layer);
            return true;
        }

        void OnEnemyHit(EnemyHitEvent e)
        {
            if (!EnsureLayer())
            {
                return;
            }

            Item item = Acquire();
            if (item == null)
            {
                return;
            }

            item.World = e.Position + Vector3.up * 1.1f;
            item.Age = 0f;
            item.Color = e.Lethal ? LethalColor : NormalColor;
            item.InUse = true;
            item.Label.text = Mathf.Max(1, Mathf.RoundToInt(e.Damage)).ToString();
            item.Label.style.display = DisplayStyle.Flex;
        }

        Item Acquire()
        {
            for (int i = 0; i < _items.Count; i++)
            {
                if (!_items[i].InUse)
                {
                    return _items[i];
                }
            }

            if (_items.Count < _maxNumbers)
            {
                var label = NewLabel();
                _layer.Add(label);
                var created = new Item { Label = label };
                _items.Add(created);
                return created;
            }

            // 포화: 가장 오래된 것 재활용.
            Item oldest = _items[0];
            for (int i = 1; i < _items.Count; i++)
            {
                if (_items[i].Age > oldest.Age)
                {
                    oldest = _items[i];
                }
            }

            return oldest;
        }

        Label NewLabel()
        {
            var l = new Label();
            l.style.position = Position.Absolute;
            l.style.unityFontStyleAndWeight = FontStyle.Bold;
            l.style.unityTextAlign = TextAnchor.MiddleCenter;
            l.style.translate = new Translate(Length.Percent(-50f), Length.Percent(-50f));
            l.pickingMode = PickingMode.Ignore;
            l.style.display = DisplayStyle.None;
            return l;
        }

        void LateUpdate()
        {
            if (_layer == null || _items.Count == 0)
            {
                return;
            }

            if (_cam == null)
            {
                _cam = Camera.main;
                if (_cam == null)
                {
                    return;
                }
            }

            IPanel panel = _layer.panel;
            float dt = Time.deltaTime;

            for (int i = 0; i < _items.Count; i++)
            {
                Item it = _items[i];
                if (!it.InUse)
                {
                    continue;
                }

                it.Age += dt;
                float progress = FloatingTextAnim.Progress(it.Age, _lifetime);
                if (progress >= 1f)
                {
                    it.InUse = false;
                    it.Label.style.display = DisplayStyle.None;
                    continue;
                }

                Vector3 world = it.World + Vector3.up * FloatingTextAnim.RiseOffset(progress, _riseWorld);
                Vector3 vp = _cam.WorldToViewportPoint(world);
                if (vp.z <= 0f)
                {
                    it.Label.style.display = DisplayStyle.None; // 카메라 뒤: 숨김(계속 나이 먹음)
                    continue;
                }

                Vector2 p = panel != null
                    ? RuntimePanelUtils.CameraTransformWorldToPanel(panel, world, _cam)
                    : new Vector2(vp.x * Screen.width, (1f - vp.y) * Screen.height);

                Label label = it.Label;
                label.style.display = DisplayStyle.Flex;
                label.style.left = p.x;
                label.style.top = p.y;

                float pop = FloatingTextAnim.PopScale(progress, _popScale, _popTime);
                label.style.fontSize = Mathf.RoundToInt(_baseFontSize * pop);

                Color c = it.Color;
                c.a = FloatingTextAnim.Alpha(progress, _fadeStart);
                label.style.color = c;
            }
        }
    }
}
