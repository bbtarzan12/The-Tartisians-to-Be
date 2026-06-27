using System;
using Tartisians.Core.Combat;
using Tartisians.Core.Events;
using Tartisians.Core.Feedback;
using Tartisians.Data;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Events;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Enemies
{
    /// <summary>
    /// 풀링되는 적. 이동은 EnemySimulation이 중앙에서 계산해 Move()로 적용한다.
    /// 데이터(스탯)는 EnemyDefinition에서 받는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Enemy : MonoBehaviour, IPoolable, IDamageable
    {
        const float FlashDuration = 0.08f;  // 흰색 번쩍 지속
        const float PunchDuration = 0.13f;  // 스케일 펀치 지속
        const float PunchStrength = 0.28f;  // 펀치 최대 확대율
        static readonly Color FlashColor = Color.white;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] EnemyDefinition _definition;

        Rigidbody _rb;
        Health _health;
        MeshRenderer _renderer;
        MaterialPropertyBlock _mpb;
        Color _baseColor = Color.white;
        HitReactState _react;

        public EnemyDefinition Definition => _definition;
        public Vector3 Position => _rb != null ? _rb.position : transform.position;
        public bool IsDead => _health != null && _health.IsDead;

        /// <summary>직전 프레임 이동으로 추정한 현재 속도(예측 사격 등에 사용).</summary>
        public Vector3 Velocity { get; private set; }

        /// <summary>사망/제거 시 스포너가 풀로 반환하도록 구독한다.</summary>
        public event Action<Enemy> Despawned;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            TryGetComponent(out _health);
            _renderer = GetComponentInChildren<MeshRenderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public void Initialize(EnemyDefinition definition)
        {
            _definition = definition;
            ApplyDefinition();
        }

        void ApplyDefinition()
        {
            if (_definition == null)
            {
                return;
            }

            _health?.Configure(_definition.MaxHealth);
            transform.localScale = _definition.Scale;

            // 종류별 머티리얼은 공유 인스턴스를 사용해 GPU 인스턴싱 배칭을 유지한다.
            if (_renderer != null && _definition.Material != null)
            {
                _renderer.sharedMaterial = _definition.Material;
            }

            // 피격 플래시의 복원 기준색을 캐시하고 시각 상태를 초기화한다.
            _baseColor = _definition.Material != null && _definition.Material.HasProperty(BaseColorId)
                ? _definition.Material.GetColor(BaseColorId)
                : Color.white;
            _react.Reset();
            ApplyFx();
        }

        public void Move(Vector3 delta)
        {
            float dt = Time.fixedDeltaTime;
            Velocity = dt > 0f ? delta / dt : Vector3.zero;

            if (delta != Vector3.zero)
            {
                _rb.MovePosition(_rb.position + delta);
            }
        }

        /// <summary>
        /// 즉시 순간이동. transform뿐 아니라 Rigidbody.position도 함께 설정해야
        /// 다음 물리 스텝에서 시뮬레이션이 옛 위치(보통 원점)로 끌어당기지 않는다.
        /// </summary>
        public void SetPosition(Vector3 position)
        {
            transform.position = position;
            if (_rb != null)
            {
                _rb.position = position;
            }
        }

        public void TakeDamage(float amount)
        {
            if (_health == null)
            {
                return;
            }

            float before = _health.Current;
            _health.TakeDamage(amount);
            float applied = before - _health.Current;
            bool dead = _health.IsDead;

            // 손맛: 피격 반응 + 데미지 숫자/임팩트 VFX용 이벤트(실제 적용량 기준).
            if (applied > 0f)
            {
                _react.Trigger(FlashDuration, PunchDuration);
                ApplyFx(); // 같은 프레임에 즉시 번쩍
                EventBus<EnemyHitEvent>.Raise(new EnemyHitEvent
                {
                    Position = Position,
                    Damage = applied,
                    Lethal = dead,
                });
            }

            if (dead)
            {
                Despawned?.Invoke(this);
            }
        }

        /// <summary>피격 반응 감쇠를 EnemySimulation의 중앙 루프에서 틱한다(적별 Update 없음).</summary>
        public void TickFx(float dt)
        {
            if (!_react.IsActive)
            {
                return;
            }

            _react.Tick(dt);
            ApplyFx();
        }

        void ApplyFx()
        {
            if (_renderer != null)
            {
                Color c = Color.Lerp(_baseColor, FlashColor, _react.FlashAmount);
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, c);
                _renderer.SetPropertyBlock(_mpb);
            }

            Vector3 baseScale = _definition != null ? _definition.Scale : Vector3.one;
            transform.localScale = baseScale * _react.ScaleMultiplier(PunchStrength);
        }

        public void OnSpawned()
        {
            if (_definition != null)
            {
                _health?.Configure(_definition.MaxHealth);
            }

            // 풀 재사용 시 직전 인스턴스의 플래시/펀치 잔상 제거.
            _react.Reset();
            ApplyFx();
        }

        public void OnDespawned()
        {
        }
    }
}
