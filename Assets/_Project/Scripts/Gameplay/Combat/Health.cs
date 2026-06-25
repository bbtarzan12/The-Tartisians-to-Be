using System;
using Tartisians.Core.Combat;
using UnityEngine;

namespace Tartisians.Gameplay.Combat
{
    /// <summary>
    /// 공용 체력 컴포넌트(플레이어·적 재사용). 순수 HealthState를 감싸고
    /// 로컬 이벤트(Damaged/Died)를 노출한다. IDamageable로 데미지를 받는다.
    /// </summary>
    public sealed class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] float _maxHealth = 100f;

        HealthState _state;

        public float Current => _state?.Current ?? 0f;
        public float Max => _state?.Max ?? _maxHealth;
        public bool IsDead => _state?.IsDead ?? false;

        public event Action<float> Damaged;
        public event Action Died;

        void Awake() => _state ??= new HealthState(_maxHealth);

        /// <summary>정의(SO)에서 받은 최대 체력으로 초기화한다.</summary>
        public void Configure(float max)
        {
            _maxHealth = max;
            _state = new HealthState(max);
        }

        public void TakeDamage(float amount)
        {
            _state ??= new HealthState(_maxHealth);

            float applied = _state.TakeDamage(amount);
            if (applied <= 0f)
            {
                return;
            }

            Damaged?.Invoke(applied);
            if (_state.IsDead)
            {
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            _state ??= new HealthState(_maxHealth);
            _state.Heal(amount);
        }
    }
}
