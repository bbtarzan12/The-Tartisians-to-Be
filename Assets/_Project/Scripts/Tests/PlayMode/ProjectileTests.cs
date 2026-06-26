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

            Projectile proj = pool.Get();
            proj.transform.position = new Vector3(-1.5f, 1f, 0f);
            proj.Launch(Vector3.right, speed: 20f, damage: 50f, pierce: 0, lifetime: 3f, pool);

            for (int i = 0; i < 40 && !enemy.IsDead; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(enemy.IsDead, "투사체가 적을 관통하며 치명상을 입혀야 한다.");

            Object.Destroy(enemyGo);
            Object.Destroy(projTemplate);
            Object.Destroy(enemyDef);
            pool.Clear();
        }

        // 키 작은 적(스케일 0.7) 회귀: 투사체를 적 중심 높이에서 쏘면 명중해야 한다.
        // (버그: 고정된 높은 발사 높이는 키 작은 적의 머리 위로 지나가 안 맞았음)
        [UnityTest]
        public IEnumerator Projectile_AtEnemyHeight_HitsShortEnemy()
        {
            var enemyGo = new GameObject("ShortEnemy");
            enemyGo.AddComponent<CapsuleCollider>().isTrigger = true;
            enemyGo.AddComponent<Rigidbody>().isKinematic = true;
            enemyGo.AddComponent<Health>();
            var enemy = enemyGo.AddComponent<Enemy>();

            var enemyDef = ScriptableObject.CreateInstance<EnemyDefinition>();
            SetPrivate(enemyDef, "_maxHealth", 5f);
            SetPrivate(enemyDef, "_scale", new Vector3(0.7f, 0.7f, 0.7f)); // Swift 크기(키 작음)
            enemy.Initialize(enemyDef);
            enemyGo.transform.position = new Vector3(0f, 1f, 0f);

            var projTemplate = new GameObject("ProjTemplate2");
            projTemplate.AddComponent<SphereCollider>().isTrigger = true;
            projTemplate.AddComponent<Rigidbody>().isKinematic = true;
            projTemplate.AddComponent<Projectile>();
            var pool = new PrefabPool<Projectile>(projTemplate.GetComponent<Projectile>());

            // 물리 한 스텝 진행 → kinematic rb.position 동기화(실제 게임에선 적이 매 프레임 이동해 동기화됨)
            yield return new WaitForFixedUpdate();

            Projectile proj = pool.Get();
            // WeaponController처럼 "적 중심 높이"에서 발사
            proj.transform.position = new Vector3(-1.5f, enemy.Position.y, 0f);
            proj.Launch(Vector3.right, speed: 20f, damage: 50f, pierce: 0, lifetime: 3f, pool);

            for (int i = 0; i < 40 && !enemy.IsDead; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.IsTrue(enemy.IsDead, "적 중심 높이에서 쏜 투사체는 키 작은 적도 명중해야 한다.");

            Object.Destroy(enemyGo);
            Object.Destroy(projTemplate);
            Object.Destroy(enemyDef);
            pool.Clear();
        }
    }
}
