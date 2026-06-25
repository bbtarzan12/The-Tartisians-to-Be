using System.Threading;
using Tartisians.Core.Services;
using Tartisians.Systems.Pooling;
using UnityEngine;
using UnityEngine.VFX;

namespace Tartisians.Gameplay.Vfx
{
    /// <summary>
    /// 풀링된 VFX Graph(GPU 파티클) 재생 서비스. 대량 사망에도 Instantiate/Destroy 없이
    /// 재사용한다. 수명 후 Awaitable로 풀에 반환(코루틴 대체, GC↓).
    /// ServiceLocator에 등록되어 사망 처리 등에서 Play(position)으로 호출된다.
    /// </summary>
    public sealed class VfxService : MonoBehaviour
    {
        [SerializeField] VisualEffect _deathVfxPrefab;
        [SerializeField] float _lifetime = 1f;

        PrefabPool<VisualEffect> _pool;

        void Awake()
        {
            if (_deathVfxPrefab != null)
            {
                _pool = new PrefabPool<VisualEffect>(_deathVfxPrefab, transform, defaultCapacity: 32, maxSize: 500);
            }

            ServiceLocator.Register(this);
        }

        void OnDestroy()
        {
            if (ServiceLocator.TryGet(out VfxService current) && ReferenceEquals(current, this))
            {
                ServiceLocator.Unregister<VfxService>();
            }
        }

        public void PlayDeath(Vector3 position)
        {
            if (_pool == null)
            {
                return;
            }

            VisualEffect fx = _pool.Get();
            fx.transform.position = position;
            fx.Play();
            ReleaseAfter(fx, destroyCancellationToken);
        }

        async void ReleaseAfter(VisualEffect fx, CancellationToken token)
        {
            try
            {
                await Awaitable.WaitForSecondsAsync(_lifetime, token);
            }
            catch (System.OperationCanceledException)
            {
                return;
            }

            if (_pool != null && fx != null)
            {
                _pool.Release(fx);
            }
        }
    }
}
