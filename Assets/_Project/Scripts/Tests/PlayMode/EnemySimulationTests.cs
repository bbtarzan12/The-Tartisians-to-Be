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
    public class EnemySimulationTests
    {
        static void SetPrivate(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }

        // 회귀: 인스펙터로 _target이 미리 연결돼 있어도 접촉 데미지가 적용돼야 한다.
        // (버그: _playerHealth를 _target==null 블록 안에서만 해석해 적이 공격을 못 했음)
        [UnityTest]
        public IEnumerator Enemy_TouchingPlayer_DealsContactDamage()
        {
            var playerGo = new GameObject("Player") { tag = "Player" };
            var playerHp = playerGo.AddComponent<Health>();
            playerHp.Configure(100f);
            playerGo.transform.position = Vector3.zero;

            var enemyTemplate = new GameObject("EnemyTemplate");
            enemyTemplate.AddComponent<Rigidbody>().isKinematic = true;
            enemyTemplate.AddComponent<Health>();
            enemyTemplate.AddComponent<Enemy>();

            var enemyDef = ScriptableObject.CreateInstance<EnemyDefinition>(); // 접촉 DPS 기본 5
            var wave = ScriptableObject.CreateInstance<WaveDefinition>();
            SetPrivate(wave, "_enemies", new[] { enemyDef });

            var spawnerGo = new GameObject("Spawner");
            spawnerGo.SetActive(false);
            var spawner = spawnerGo.AddComponent<EnemySpawner>();
            SetPrivate(spawner, "_enemyPrefab", enemyTemplate.GetComponent<Enemy>());
            SetPrivate(spawner, "_wave", wave);
            SetPrivate(spawner, "_target", playerGo.transform);
            spawnerGo.SetActive(true);

            Enemy e = spawner.SpawnOne();
            e.transform.position = Vector3.zero; // 플레이어와 접촉

            // _target을 미리 주입(인스펙터 연결 상황 재현) → 버그가 있으면 _playerHealth가 null로 남는다
            var simGo = new GameObject("Sim");
            simGo.SetActive(false);
            var sim = simGo.AddComponent<EnemySimulation>();
            SetPrivate(sim, "_spawner", spawner);
            SetPrivate(sim, "_target", playerGo.transform);
            simGo.SetActive(true);

            float before = playerHp.Current;
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Less(playerHp.Current, before, "접촉한 적이 플레이어 체력을 깎아야 한다.");

            Object.Destroy(simGo);
            Object.Destroy(spawnerGo);
            Object.Destroy(enemyTemplate);
            Object.Destroy(playerGo);
            Object.Destroy(enemyDef);
            Object.Destroy(wave);
        }
    }
}
