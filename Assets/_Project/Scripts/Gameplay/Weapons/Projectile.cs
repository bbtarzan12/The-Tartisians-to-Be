using System.Collections.Generic;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Enemies;
using Tartisians.Systems.Combat;
using Tartisians.Systems.Crowd;
using Tartisians.Systems.Pooling;
using UnityEngine;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>투사체 이동 행동 파라미터(WeaponController가 def에서 빌드해 전달).</summary>
    public struct ProjectileMotionConfig
    {
        public ProjectileMotion Motion;
        public float HomingTurnRateDeg; // 호밍 조향 속도(도/초)
        public float HomingArmTime;     // 호밍: 초기 사출(직진) 시간(이후 호밍 시작)
        public float RicochetRange;     // 도탄 다음 표적 탐색 반경
        public float OutDuration;       // 부메랑: 나가는 구간 시간(이후 복귀)

        public static ProjectileMotionConfig Straight => default; // Motion=Straight(0)
    }

    /// <summary>
    /// 풀링되는 투사체. 이동 행동(직선/호밍/부메랑/도탄)에 따라 매 물리 스텝 이동하고,
    /// 적 트리거 충돌 시 데미지·관통/도탄 처리. 수명/벽 충돌 시 풀 반환.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class Projectile : MonoBehaviour, IPoolable
    {
        const float Radius = 0.2f; // 벽 충돌 판정 반경
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Rigidbody _rb;
        PrefabPool<Projectile> _pool;
        ObstacleField _obstacles;
        EnemyRegistry _registry;
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

        // 이동 행동 상태
        ProjectileMotion _motion;
        float _homingTurnDeg;   // deg/s
        float _homingArmTime;   // 초기 사출(직진) 시간
        float _ricochetRange;
        float _outDuration;
        float _age;
        bool _returning;
        Enemy _homingTarget;    // 호밍: 고정 표적(매 프레임 재조준 스파이럴 방지)
        Transform _player;      // 부메랑 복귀 대상
        readonly List<Enemy> _hitEnemies = new(); // 도탄: 같은 적 재타격 방지

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

            GameObject pl = GameObject.FindGameObjectWithTag("Player");
            if (pl != null)
            {
                _player = pl.transform;
            }
        }

        public void Launch(Vector3 direction, float speed, float damage, int pierce, float lifetime, PrefabPool<Projectile> pool)
            => Launch(direction, speed, damage, pierce, lifetime, pool, Color.white, 1f, ProjectileMotionConfig.Straight);

        public void Launch(Vector3 direction, float speed, float damage, int pierce, float lifetime, PrefabPool<Projectile> pool, Color color, float scale)
            => Launch(direction, speed, damage, pierce, lifetime, pool, color, scale, ProjectileMotionConfig.Straight);

        public void Launch(Vector3 direction, float speed, float damage, int pierce, float lifetime,
            PrefabPool<Projectile> pool, Color color, float scale, in ProjectileMotionConfig motion)
        {
            _direction = direction;
            _pool = pool;
            _speed = speed;
            _damage = damage;
            _pierceLeft = pierce;
            transform.forward = direction;
            transform.localScale = _baseScale * scale;

            // 이동 행동 초기화
            _motion = motion.Motion;
            _homingTurnDeg = motion.HomingTurnRateDeg;
            _homingArmTime = motion.HomingArmTime;
            _ricochetRange = motion.RicochetRange;
            _outDuration = motion.OutDuration > 0f ? motion.OutDuration : lifetime * 0.5f;
            _age = 0f;
            _returning = false;
            _homingTarget = null;
            _hitEnemies.Clear();

            // 부메랑은 "플레이어 회수" 시 소멸하지만, 못 돌아와도 빨리 재활용되도록 수명을
            // 왕복에 충분한 짧은 값(나가는 시간×3)으로 제한한다 → 필드에 쌓이지 않음(군집 버그 방지).
            _life = _motion == ProjectileMotion.Boomerang ? _outDuration * 3f : lifetime;

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
            float dt = Time.fixedDeltaTime;
            _age += dt;

            _life -= dt;
            if (_life <= 0f)
            {
                Release();
                return;
            }

            switch (_motion)
            {
                case ProjectileMotion.Homing: SteerHoming(dt); break;
                case ProjectileMotion.Boomerang: StepBoomerang(dt); break;
            }

            Vector3 pos = _rb.position + _direction * (_speed * dt);
            _rb.MovePosition(pos);

            // 벽 충돌 소멸(부메랑은 통과 — 마법 궤적).
            if (_motion != ProjectileMotion.Boomerang)
            {
                if (_obstacles == null)
                {
                    ServiceLocator.TryGet(out _obstacles);
                }

                if (_obstacles != null && _obstacles.Distance(pos) < Radius)
                {
                    Release();
                }
            }
        }

        // 호밍(정석): ① 표적 고정(lock-on) — 죽기 전까지 같은 적을 추적해 매 프레임 재조준 스파이럴 방지.
        // ② 선회 원 이탈 — 표적이 미사일의 선회 원(반경 R=speed/turnRate) 안이면 곡선으로 못 닿아 공전하므로
        //    직진해 원 밖으로 빠져나간 뒤 호밍 재개. (게임 호밍 미사일의 표준 'orbit problem' 해법)
        void SteerHoming(float dt)
        {
            // 초기 사출 단계: arm 시간 동안은 발사된 부채꼴 방향으로 직진해 서로 흩어진다(겹침 방지).
            if (_age < _homingArmTime)
            {
                return;
            }

            if (_homingTarget == null || _homingTarget.IsDead)
            {
                _homingTarget = NearestEnemy(_rb.position, float.MaxValue, null);
            }

            if (_homingTarget == null)
            {
                return; // 적이 없으면 직진
            }

            Vector3 pos = _rb.position;
            Vector3 desired = _homingTarget.Position - pos;
            desired.y = 0f;
            if (desired.sqrMagnitude < 1e-4f)
            {
                return;
            }

            desired.Normalize();
            float signed = Vector3.SignedAngle(_direction, desired, Vector3.up); // 진행→표적 회전각(부호)

            // 선회 원 안에 표적이 있으면 직진(조향 정지)으로 빠져나간다.
            float turnRateRad = _homingTurnDeg * Mathf.Deg2Rad;
            if (turnRateRad > 1e-4f)
            {
                float radius = _speed / turnRateRad;
                Vector3 centerDir = Quaternion.AngleAxis(Mathf.Sign(signed) * 90f, Vector3.up) * _direction;
                Vector3 center = pos + centerDir * radius;
                Vector3 toCenter = _homingTarget.Position - center;
                toCenter.y = 0f;
                if (toCenter.sqrMagnitude < radius * radius)
                {
                    return; // 표적이 선회 원 안 → 직진
                }
            }

            // 선회율 한도 내에서 표적 쪽으로 회전(up축 부호각 — 180° 특이점 안전).
            float maxDeg = _homingTurnDeg * dt;
            float turn = Mathf.Clamp(signed, -maxDeg, maxDeg);
            _direction = Quaternion.AngleAxis(turn, Vector3.up) * _direction;
            _direction.y = 0f;
            if (_direction.sqrMagnitude < 1e-4f)
            {
                _direction = desired;
            }

            _direction.Normalize();
            // 회전은 쓰지 않는다(구 메시라 무의미). transform 직접 쓰기 + MovePosition 혼용은 키네마틱 이동을 깨뜨림.
        }

        // 부메랑: 나가는 구간이 끝나면 플레이어를 향해 부드럽게 선회해 복귀, 도달 시 소멸.
        void StepBoomerang(float dt)
        {
            if (!_returning && _age >= _outDuration)
            {
                _returning = true;
                _hitEnemies.Clear(); // 복귀 경로에서 다시 타격(왕복 2회)
            }

            if (!_returning)
            {
                return;
            }

            if (_player == null)
            {
                GameObject p = GameObject.FindGameObjectWithTag("Player");
                if (p != null)
                {
                    _player = p.transform;
                }
            }

            if (_player == null)
            {
                return;
            }

            Vector3 toPlayer = _player.position - _rb.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.6f)
            {
                Release(); // 플레이어에게 회수됨
                return;
            }

            // 급반전 대신 부드럽게 선회(보기 좋게). 복귀라 선회율은 넉넉히.
            Vector3 desired = toPlayer.normalized;
            float maxDeg = 540f * dt;
            float signed = Vector3.SignedAngle(_direction, desired, Vector3.up);
            float turn = Mathf.Clamp(signed, -maxDeg, maxDeg);
            _direction = Quaternion.AngleAxis(turn, Vector3.up) * _direction;
            _direction.y = 0f;
            if (_direction.sqrMagnitude < 1e-4f)
            {
                _direction = desired;
            }

            _direction.Normalize();
            // 회전 생략(구 메시). transform 직접 쓰기 + MovePosition 혼용 금지.
        }

        void OnTriggerEnter(Collider other)
        {
            if (_pool == null)
            {
                return;
            }

            if (!other.TryGetComponent(out Enemy enemy) || enemy.IsDead)
            {
                return;
            }

            // 부메랑/도탄은 같은 적을 한 통과에 두 번 때리지 않도록 추적.
            if (_motion == ProjectileMotion.Boomerang || _motion == ProjectileMotion.Ricochet)
            {
                if (_hitEnemies.Contains(enemy))
                {
                    return;
                }

                _hitEnemies.Add(enemy);
            }

            DamageSystem.Apply(enemy, _damage);

            switch (_motion)
            {
                case ProjectileMotion.Boomerang:
                    // 통과(소멸하지 않음). 복귀하며 다시 맞히면 _hitEnemies로 1회만.
                    break;

                case ProjectileMotion.Ricochet:
                    if (_pierceLeft <= 0)
                    {
                        Release();
                    }
                    else
                    {
                        _pierceLeft--;
                        Enemy next = NearestEnemy(_rb.position, _ricochetRange, _hitEnemies);
                        if (next == null)
                        {
                            Release();
                        }
                        else
                        {
                            Vector3 d = next.Position - _rb.position;
                            d.y = 0f;
                            if (d.sqrMagnitude > 1e-4f)
                            {
                                _direction = d.normalized;
                            }
                        }
                    }

                    break;

                default: // Straight / Homing
                    if (_pierceLeft <= 0)
                    {
                        Release();
                    }
                    else
                    {
                        _pierceLeft--;
                    }

                    break;
            }
        }

        // 반경 maxRange 내(또는 전체) 가장 가까운 살아있는 적. exclude에 든 적은 제외.
        Enemy NearestEnemy(Vector3 from, float maxRange, List<Enemy> exclude)
        {
            if (_registry == null)
            {
                ServiceLocator.TryGet(out _registry);
                if (_registry == null)
                {
                    return null;
                }
            }

            Enemy best = null;
            float bestSq = maxRange >= float.MaxValue ? float.MaxValue : maxRange * maxRange;
            IReadOnlyList<Enemy> active = _registry.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy e = active[i];
                if (e.IsDead || (exclude != null && exclude.Contains(e)))
                {
                    continue;
                }

                float sq = (e.Position - from).sqrMagnitude;
                if (sq < bestSq)
                {
                    bestSq = sq;
                    best = e;
                }
            }

            return best;
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
