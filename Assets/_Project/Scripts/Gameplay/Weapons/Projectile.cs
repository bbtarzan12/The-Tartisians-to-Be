using Tartisians.Gameplay.Enemies;
using Tartisians.Systems.Combat;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>
    /// 풀링되는 투사체. 직선 이동, 적 트리거 충돌 시 데미지, 관통 수만큼 통과 후 풀 반환.
    /// 수명 종료 시에도 반환한다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        Rigidbody _rb;
        PrefabPool<Projectile> _pool;
        Vector3 _direction;
        float _speed;
        float _damage;
        float _life;
        int _pierceLeft;

        void Awake() => _rb = GetComponent<Rigidbody>();

        public void Launch(Vector3 direction, float speed, float damage, int pierce, float lifetime, PrefabPool<Projectile> pool)
        {
            _direction = direction;
            _pool = pool;
            _speed = speed;
            _damage = damage;
            _pierceLeft = pierce;
            _life = lifetime;
            transform.forward = direction;
        }

        void FixedUpdate()
        {
            _rb.MovePosition(_rb.position + _direction * (_speed * Time.fixedDeltaTime));
            _life -= Time.fixedDeltaTime;
            if (_life <= 0f)
            {
                Release();
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (_pool == null)
            {
                return;
            }

            if (other.TryGetComponent(out Enemy enemy) && !enemy.IsDead)
            {
                DamageSystem.Apply(enemy, _damage);
                if (_pierceLeft <= 0)
                {
                    Release();
                }
                else
                {
                    _pierceLeft--;
                }
            }
        }

        void Release()
        {
            if (_pool == null)
            {
                return;
            }

            PrefabPool<Projectile> pool = _pool;
            _pool = null;
            pool.Release(this);
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned() => _pool = null;
    }
}
