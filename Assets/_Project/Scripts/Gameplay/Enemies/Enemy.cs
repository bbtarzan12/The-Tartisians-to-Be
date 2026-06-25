using System;
using Tartisians.Core.Combat;
using Tartisians.Data;
using Tartisians.Gameplay.Combat;
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
        [SerializeField] EnemyDefinition _definition;

        Rigidbody _rb;
        Health _health;
        MeshRenderer _renderer;

        public EnemyDefinition Definition => _definition;
        public Vector3 Position => _rb != null ? _rb.position : transform.position;
        public bool IsDead => _health != null && _health.IsDead;

        /// <summary>사망/제거 시 스포너가 풀로 반환하도록 구독한다.</summary>
        public event Action<Enemy> Despawned;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            TryGetComponent(out _health);
            _renderer = GetComponentInChildren<MeshRenderer>();
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
        }

        public void Move(Vector3 delta)
        {
            if (delta != Vector3.zero)
            {
                _rb.MovePosition(_rb.position + delta);
            }
        }

        public void TakeDamage(float amount)
        {
            if (_health == null)
            {
                return;
            }

            _health.TakeDamage(amount);
            if (_health.IsDead)
            {
                Despawned?.Invoke(this);
            }
        }

        public void OnSpawned()
        {
            if (_definition != null)
            {
                _health?.Configure(_definition.MaxHealth);
            }
        }

        public void OnDespawned()
        {
        }
    }
}
