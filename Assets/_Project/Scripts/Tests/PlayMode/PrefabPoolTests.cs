using System.Collections;
using NUnit.Framework;
using Tartisians.Systems.Pooling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tartisians.Tests.PlayMode
{
    public class PrefabPoolTests
    {
        sealed class PoolableProbe : MonoBehaviour, IPoolable
        {
            public int Spawned;
            public int Despawned;

            public void OnSpawned() => Spawned++;
            public void OnDespawned() => Despawned++;
        }

        [UnityTest]
        public IEnumerator Get_Release_ReusesSameInstance()
        {
            var prefabGo = new GameObject("Probe");
            var prefab = prefabGo.AddComponent<PoolableProbe>();
            var pool = new PrefabPool<PoolableProbe>(prefab);

            PoolableProbe a = pool.Get();
            Assert.AreEqual(1, a.Spawned, "Get 시 OnSpawned가 호출돼야 한다.");
            Assert.IsTrue(a.gameObject.activeSelf);

            pool.Release(a);
            Assert.AreEqual(1, a.Despawned, "Release 시 OnDespawned가 호출돼야 한다.");
            Assert.IsFalse(a.gameObject.activeSelf);

            PoolableProbe b = pool.Get();
            Assert.AreSame(a, b, "반환된 인스턴스가 재사용돼야 한다.");
            Assert.AreEqual(2, b.Spawned);

            yield return null;

            pool.Clear();
            Object.Destroy(prefabGo);
        }

        [UnityTest]
        public IEnumerator Prewarm_CreatesInactiveInstances()
        {
            var prefabGo = new GameObject("Probe");
            var prefab = prefabGo.AddComponent<PoolableProbe>();
            var pool = new PrefabPool<PoolableProbe>(prefab);

            pool.Prewarm(5);

            Assert.AreEqual(5, pool.CountInactive, "Prewarm 후 비활성 인스턴스 5개가 대기해야 한다.");
            Assert.AreEqual(0, pool.CountActive);

            yield return null;

            pool.Clear();
            Object.Destroy(prefabGo);
        }
    }
}
