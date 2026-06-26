using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tartisians.Data;
using Tartisians.Gameplay.Input;
using Tartisians.Gameplay.Player;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tartisians.Tests.PlayMode
{
    public class PlayerControllerTests
    {
        sealed class StubInput : MonoBehaviour, IMoveInputSource
        {
            public Vector2 Value;
            public Vector2 MoveInput => Value;
        }

        [UnityTest]
        public IEnumerator FixedUpdate_MovesAlongInputDirection()
        {
            var def = ScriptableObject.CreateInstance<PlayerDefinition>(); // moveSpeed 기본 6
            var go = new GameObject("PlayerTest");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var stub = go.AddComponent<StubInput>();
            stub.Value = new Vector2(1f, 0f); // +X로 이동

            var pc = go.AddComponent<PlayerController>();
            typeof(PlayerController)
                .GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pc, def);

            float startX = go.transform.position.x;

            for (int i = 0; i < 10; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.Greater(go.transform.position.x, startX + 0.01f, "+X 입력 시 X 위치가 증가해야 한다.");
            Assert.AreEqual(0f, go.transform.position.z, 0.001f, "Z는 변하지 않아야 한다.");

            Object.Destroy(go);
            Object.Destroy(def);
        }

        [UnityTest]
        public IEnumerator ZeroInput_DoesNotMove()
        {
            var def = ScriptableObject.CreateInstance<PlayerDefinition>();
            var go = new GameObject("PlayerTestIdle");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var stub = go.AddComponent<StubInput>();
            stub.Value = Vector2.zero;

            var pc = go.AddComponent<PlayerController>();
            typeof(PlayerController)
                .GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pc, def);

            Vector3 start = go.transform.position;

            for (int i = 0; i < 5; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            Assert.AreEqual(start, go.transform.position);

            Object.Destroy(go);
            Object.Destroy(def);
        }

        [UnityTest]
        public IEnumerator Movement_ClampedToArenaBounds()
        {
            var def = ScriptableObject.CreateInstance<PlayerDefinition>();
            typeof(PlayerDefinition)
                .GetField("_moveSpeed", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(def, 50f); // 빠르게 경계까지 도달

            var go = new GameObject("PlayerTestClamp");
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var stub = go.AddComponent<StubInput>();
            stub.Value = new Vector2(1f, 0f); // +X로 계속 이동

            var pc = go.AddComponent<PlayerController>();
            typeof(PlayerController)
                .GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pc, def); // _arenaHalfExtent는 기본값 (19,19)

            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            // 경계(19)를 넘지 않아야 한다(미적용이면 ~36까지 갔을 것).
            Assert.LessOrEqual(go.transform.position.x, 19.01f, "아레나 경계로 제한돼야 한다.");
            Assert.Greater(go.transform.position.x, 18.5f, "경계 근처까지는 이동해야 한다.");

            Object.Destroy(go);
            Object.Destroy(def);
        }
    }
}
