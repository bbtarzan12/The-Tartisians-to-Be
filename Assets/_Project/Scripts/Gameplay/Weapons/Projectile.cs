using Tartisians.Core.Services;
using Tartisians.Gameplay.Enemies;
using Tartisians.Systems.Combat;
using Tartisians.Systems.Crowd;
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
        const float Radius = 0.2f; // 벽 충돌 판정 반경
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Rigidbody _rb;
        PrefabPool<Projectile> _pool;
        ObstacleField _obstacles;
        Renderer _renderer;
        TrailRenderer _trail;
        MaterialPropertyBlock _mpb;
        Vector3 _baseScale = Vector3.one;
        float _baseTrailWidth;
        Vector3 _direction;
        float _speed;
        float _damage;
        float _life;
        int _pierceLeft;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<Renderer>();
            _trail = GetComponentInChildren<TrailRenderer>();
            _mpb = new MaterialPropertyBlock();
            _baseScale = transform.localScale;
            if (_trail != null)
            {
                _baseTrailWidth = _trail.widthMultiplier;
            }
        }

        public void Launch(Vector3 direction, float speed, float damage, int pierce, float lifetime, PrefabPool<Projectile> pool)
            => Launch(direction, speed, damage, pierce, lifetime, pool, Color.white, 1f);

        public void Launch(Vector3 direction, float speed, float damage, int pierce, float lifetime, PrefabPool<Projectile> pool, Color color, float scale)
        {
            _direction = direction;
            _pool = pool;
            _speed = speed;
            _damage = damage;
            _pierceLeft = pierce;
            _life = lifetime;
            transform.forward = direction;
            transform.localScale = _baseScale * scale;

            // 무기별 색(인스턴싱 깨지 않게 MaterialPropertyBlock).
            if (_renderer != null)
            {
                _renderer.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, color);
                _renderer.SetPropertyBlock(_mpb);
            }

            // 트레일: 풀 재사용 시 이전 위치 잔상 제거 + 색/폭 설정.
            if (_trail != null)
            {
                _trail.Clear();
                Color tail = color; tail.a = 0f;
                _trail.startColor = color;
                _trail.endColor = tail;
                _trail.widthMultiplier = _baseTrailWidth * scale;
            }
        }

        void FixedUpdate()
        {
            Vector3 pos = _rb.position + _direction * (_speed * Time.fixedDeltaTime);
            _rb.MovePosition(pos);

            _life -= Time.fixedDeltaTime;
            if (_life <= 0f)
            {
                Release();
                return;
            }

            // 벽·장애물에 부딪히면 소멸(적 충돌과 별개, 해석적 ObstacleField).
            if (_obstacles == null)
            {
                ServiceLocator.TryGet(out _obstacles);
            }

            if (_obstacles != null && _obstacles.Distance(pos) < Radius)
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
