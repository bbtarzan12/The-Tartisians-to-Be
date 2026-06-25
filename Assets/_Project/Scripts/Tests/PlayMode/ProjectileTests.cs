using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Combat;
using Tartisians.Gameplay.Enemies;
using Tartisians.Gameplay.Weapons;
using Tartisians.Systems.Pooling;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tartisians.Tests.PlayMode
{
    public class ProjectileTests
    {
        static void SetPrivate(object obj, string field, object value)
        {
            obj.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
        }

        [UnityTest]
        public IEnumerator Projectile_HitsEnemy_AppliesLethalDamage()
        {
            // 적 (트리거 콜라이더 + 키네마틱 RB + Health + Enemy)
            var enemyGo = new GameObject("Enemy");
            enemyGo.AddComponent<CapsuleCollider>().isTrigger = true;
            var erb = enemyGo.AddComponent<Rigidbody>();
            erb.isKinematic = true;
            enemyGo.AddComponent<Health>();
            var enemy = enemyGo.AddComponent<Enemy>();
            var enemyDef = ScriptableObject.CreateInstance<EnemyDefinition>(); // hp 10
            enemy.Initialize(enemyDef);
            enemyGo.transform.position = new Vector3(0f, 1f, 0f);

            // 투사체 템플릿 + 풀
            var projTemplate = new GameObject("ProjTemplate");
            projTemplate.AddComponent<SphereCollider>().isTrigger = true;
            var prb = projTemplate.AddComponent<Rigidbody>();
            prb.isKinematic = true;
            projTemplate.AddComponent<Projectile>();
            var pool = new PrefabPool<Projectile>(projTemplate.GetComponent<Projectile>());

            var weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            SetPrivate(weapon, "_damage", 50f);
            SetPrivate(weapon, "_projectileSpeed", 20f);
            SetPrivate(weapon, "_lifetime", 3f);

            Projectile proj = pool.Get();
            proj.transform.position = new Vector3(-1.5f, 1f, 0f);
            proj.Launch(Vector3.right, weapon, pool);

            for (int i = 0; i < 40 && !enemy.IsDead; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(enemy.IsDead, "투사체가 적을 관통하며 치명상을 입혀야 한다.");

            Object.Destroy(enemyGo);
            Object.Destroy(projTemplate);
            Object.Destroy(enemyDef);
            Object.Destroy(weapon);
            pool.Clear();
        }
    }
}
