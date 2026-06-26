using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Enemies;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tartisians.Tests.PlayMode
{
    public class EnemySpawnerTests
    {
        static void SetPrivate(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }

        static Enemy BuildEnemyTemplate()
        {
            var go = new GameObject("EnemyTemplate");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            go.AddComponent<Health>();
            return go.AddComponent<Enemy>();
        }

        [UnityTest]
        public IEnumerator SpawnOne_AddsToRegistry_DeathReturnsToPool()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>(); // maxHealth 기본 10
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            SetPrivate(wave, "_enemies", new[] { def });

            Enemy template = BuildEnemyTemplate();

            var go = new GameObject("Spawner");
            go.SetActive(false); // Awake 전에 참조 주입 (풀 생성이 프리팹을 요구)
            var spawner = go.AddComponent<EnemySpawner>();
            SetPrivate(spawner, "_enemyPrefab", template);
            SetPrivate(spawner, "_wave", wave);
            go.SetActive(true);

            Enemy spawned = spawner.SpawnOne();
            Assert.IsNotNull(spawned);
            Assert.AreEqual(1, spawner.Registry.Count, "스폰 후 레지스트리에 1개.");
            Assert.IsTrue(spawned.gameObject.activeSelf);

            spawned.TakeDamage(9999f); // 사망 → 풀 반환
            yield return null;

            Assert.AreEqual(0, spawner.Registry.Count, "사망 시 레지스트리에서 제거돼야 한다.");
            Assert.IsFalse(spawned.gameObject.activeSelf, "사망 시 풀로 반환(비활성)돼야 한다.");

            // 재스폰 시 같은 인스턴스 재사용(풀링)
            Enemy reused = spawner.SpawnOne();
            Assert.AreSame(spawned, reused, "반환된 인스턴스가 재사용돼야 한다.");

            Object.Destroy(go);
            Object.Destroy(template.gameObject);
            Object.Destroy(def);
            Object.Destroy(wave);
        }

        // 회귀: 스폰 위치가 원점(스폰 지점)이 아니고, 아레나(±18) 안이어야 한다.
        // (스폰은 화면 밖+아레나 안 거부 샘플링. 카메라 없으면 화면 밖 판정이라 거리만 보장.)
        [UnityTest]
        public IEnumerator SpawnOne_PlacesEnemyInsideArena_NotAtOrigin()
        {
            var def = ScriptableObject.CreateInstance<EnemyDefinition>();
            var wave = ScriptableObject.CreateInstance<WaveDefinition>(); // spawnRadius 기본 18
            SetPrivate(wave, "_enemies", new[] { def });

            Enemy template = BuildEnemyTemplate();

            var go = new GameObject("Spawner");
            go.SetActive(false);
            var spawner = go.AddComponent<EnemySpawner>();
            SetPrivate(spawner, "_enemyPrefab", template);
            SetPrivate(spawner, "_wave", wave);
            go.SetActive(true); // _target 미지정 → 중심 원점

            Enemy spawned = spawner.SpawnOne();
            Vector3 p = spawned.Position;
            p.y = 0f;

            Assert.Greater(p.magnitude, 1f, "적이 원점(스폰 지점)에 생기면 안 된다.");
            Assert.LessOrEqual(Mathf.Abs(p.x), 18.01f, "아레나(±18) 안에 생성돼야 한다.");
            Assert.LessOrEqual(Mathf.Abs(p.z), 18.01f, "아레나(±18) 안에 생성돼야 한다.");

            yield return null;

            Object.Destroy(go);
            Object.Destroy(template.gameObject);
            Object.Destroy(def);
            Object.Destroy(wave);
        }
    }
}
