using System;
using UnityEngine;
using UnityEngine.Pool;

namespace Tartisians.Systems.Pooling
{
    /// <summary>
    /// UnityEngine.Pool.ObjectPool&lt;T&gt; 위에 얹은 프리팹/컴포넌트 풀.
    /// 직접 풀을 구현하지 않고 빌트인을 래핑한다(docs/04). 적·투사체·젬·VFX 공용.
    /// Get/Release 시 GameObject 활성 토글 + IPoolable 콜백을 자동 처리한다.
    /// </summary>
    public sealed class PrefabPool<T> where T : Component
    {
        readonly T _prefab;
        readonly Transform _parent;
        readonly ObjectPool<T> _pool;

        public PrefabPool(
            T prefab,
            Transform parent = null,
            int defaultCapacity = 32,
            int maxSize = 10000,
            bool collectionCheck = false)
        {
            _prefab = prefab != null ? prefab : throw new ArgumentNullException(nameof(prefab));
            _parent = parent;
            _pool = new ObjectPool<T>(
                createFunc: Create,
                actionOnGet: OnGet,
                actionOnRelease: OnRelease,
                actionOnDestroy: OnDestroyInstance,
                collectionCheck: collectionCheck,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize);
        }

        public int CountActive => _pool.CountActive;
        public int CountInactive => _pool.CountInactive;

        public T Get() => _pool.Get();

        public void Release(T instance) => _pool.Release(instance);

        public void Clear() => _pool.Clear();

        /// <summary>풀을 미리 채워 런타임 스폰 스파이크를 제거한다.</summary>
        public void Prewarm(int count)
        {
            if (count <= 0)
            {
                return;
            }

            var buffer = new T[count];
            for (int i = 0; i < count; i++)
            {
                buffer[i] = _pool.Get();
            }

            for (int i = 0; i < count; i++)
            {
                _pool.Release(buffer[i]);
            }
        }

        T Create() => UnityEngine.Object.Instantiate(_prefab, _parent);

        static void OnGet(T instance)
        {
            instance.gameObject.SetActive(true);
            if (instance is IPoolable poolable)
            {
                poolable.OnSpawned();
            }
        }

        static void OnRelease(T instance)
        {
            if (instance is IPoolable poolable)
            {
                poolable.OnDespawned();
            }

            instance.gameObject.SetActive(false);
        }

        static void OnDestroyInstance(T instance)
        {
            if (instance != null)
            {
                UnityEngine.Object.Destroy(instance.gameObject);
            }
        }
    }
}
