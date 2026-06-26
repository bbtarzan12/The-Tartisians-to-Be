using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Input;
using Tartisians.Gameplay.Player;
using Tartisians.Systems.Crowd;
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

        // 회귀: 플레이어가 내부 장애물(ObstacleField)을 뚫지 않아야 한다.
        // (버그: 아레나 박스 클램프만 있어 둘레 밖은 막아도 내부 벽은 통과했음)
        [UnityTest]
        public IEnumerator Movement_PushedOutOfObstacle()
        {
            var obstacles = new ObstacleField();
            obstacles.Add(new Vector3(2f, 0f, -10f), new Vector3(4f, 0f, 10f)); // x[2,4] 벽
            ServiceLocator.Register(obstacles);

            var def = ScriptableObject.CreateInstance<PlayerDefinition>(); // moveSpeed 6
            var go = new GameObject("PlayerObstacle");
            go.transform.position = Vector3.zero;
            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            var stub = go.AddComponent<StubInput>();
            stub.Value = new Vector2(1f, 0f); // +X(벽으로) 이동

            var pc = go.AddComponent<PlayerController>();
            typeof(PlayerController)
                .GetField("_definition", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(pc, def); // _collisionRadius 기본 0.5

            for (int i = 0; i < 60; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            // 벽 면(x=2)에 캡슐 반경(0.5)만큼 못 미친 ~1.5에서 멈춰야 한다(절대 박스 안으로 못 들어감).
            Assert.Less(go.transform.position.x, 2f, "장애물을 뚫으면 안 된다(박스 밖).");
            Assert.That(go.transform.position.x, Is.EqualTo(1.5f).Within(0.1f), "벽 앞 반경 거리에서 멈춰야 한다.");

            ServiceLocator.Unregister<ObstacleField>();
            Object.Destroy(go);
            Object.Destroy(def);
        }
    }
}
