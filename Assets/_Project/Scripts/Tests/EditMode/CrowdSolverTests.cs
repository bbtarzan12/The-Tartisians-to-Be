using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Systems.Crowd;
using Tartisians.Systems.Spatial;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    /// <summary>
    /// PBD 군중 솔버(<see cref="CrowdSolver"/>) 단위 테스트. 모두 XZ 평면, dt=0.02.
    /// 핵심 불변식: 선호속도 추종 · 적-적 비침투 · 벽 비침투 · "공간 없으면 정지" · 최대 속도 클램프.
    /// </summary>
    public class CrowdSolverTests
    {
        const float Dt = 0.02f;

        // x<=0을 벽으로 보는 평면 SDF. free 영역은 x>0, 거리=x, 바깥 방향=+X.
        sealed class PlaneWall : IObstacleField
        {
            public float Distance(Vector3 world) => world.x;
            public Vector3 Normal(Vector3 world) => Vector3.right;
        }

        static CrowdSolver MakeSolver(float maxAccel = 100000f)
        {
            // 제약 수학 자체를 검증할 땐 후처리 클램프가 방해되지 않도록 크게 둔다.
            return new CrowdSolver
            {
                VelocityBlend = 1f, // 선호속도를 즉시 반영(테스트 결정성)
                MaxAccel = maxAccel,
                EnableViscosity = false,
                EnableLongRange = false,
            };
        }

        static void Step(CrowdSolver solver, List<Vector3> pos, List<Vector3> vel,
            List<Vector3> pref, List<float> rad, List<float> spd, IObstacleField walls)
        {
            solver.Step(pos.Count, pos, vel, pref, rad, spd, new SpatialHashGrid(2f), walls, Dt);
        }

        [Test]
        public void SeekTowardGoal_NoNeighbors_MovesAlongPreferred()
        {
            var solver = MakeSolver();
            var pos = new List<Vector3> { Vector3.zero };
            var vel = new List<Vector3> { Vector3.zero };
            var pref = new List<Vector3> { new Vector3(3f, 0f, 0f) }; // +X, speed 3
            var rad = new List<float> { 0.5f };
            var spd = new List<float> { 3f };

            Step(solver, pos, vel, pref, rad, spd, null);

            Assert.Greater(pos[0].x, 0f, "선호속도(+X) 방향으로 전진해야 한다.");
            Assert.AreEqual(0f, pos[0].z, 1e-4f, "측면 이동은 없어야 한다.");
            Assert.LessOrEqual(vel[0].magnitude, 3f + 1e-3f, "속도는 최대속도(=speed)를 넘지 않는다.");
        }

        [Test]
        public void TwoOverlappingAgents_ArePushedApart()
        {
            var solver = MakeSolver();
            var pos = new List<Vector3> { new Vector3(-0.25f, 0f, 0f), new Vector3(0.25f, 0f, 0f) }; // dist 0.5
            var vel = new List<Vector3> { Vector3.zero, Vector3.zero };
            var pref = new List<Vector3> { Vector3.zero, Vector3.zero };
            var rad = new List<float> { 0.5f, 0.5f }; // minDist 1.0
            var spd = new List<float> { 50f, 50f };

            float before = (pos[0] - pos[1]).magnitude;
            Step(solver, pos, vel, pref, rad, spd, null);
            float after = (pos[0] - pos[1]).magnitude;

            Assert.Greater(after, before, "겹친 두 적은 서로 밀어 떨어져야 한다.");
            Assert.GreaterOrEqual(after, 0.95f, "거의 반경 합(1.0)까지 분리돼야 한다.");
        }

        [Test]
        public void AgentDrivenIntoWall_StaysOutside()
        {
            var solver = MakeSolver();
            var pos = new List<Vector3> { new Vector3(0.2f, 0f, 0f) }; // 벽(반경 0.5) 안으로 침투
            var vel = new List<Vector3> { Vector3.zero };
            var pref = new List<Vector3> { new Vector3(-20f, 0f, 0f) }; // 벽 쪽으로 강하게
            var rad = new List<float> { 0.5f };
            var spd = new List<float> { 50f };

            Step(solver, pos, vel, pref, rad, spd, new PlaneWall());

            Assert.GreaterOrEqual(pos[0].x, 0.5f - 1e-3f, "적은 벽 표면(반경) 밖에 머물러야 한다.");
        }

        [Test]
        public void NoSpace_AgentStopsAtWall()
        {
            var solver = MakeSolver();
            var pos = new List<Vector3> { new Vector3(0.5f, 0f, 0f) }; // 벽 표면에 접함
            var vel = new List<Vector3> { Vector3.zero };
            var pref = new List<Vector3> { new Vector3(-20f, 0f, 0f) }; // 계속 벽으로 밀어붙임
            var rad = new List<float> { 0.5f };
            var spd = new List<float> { 50f };

            // 여러 스텝 진행해도 벽을 파고들지 않고 그 자리에 멈춰 있어야 한다.
            for (int i = 0; i < 10; i++)
            {
                Step(solver, pos, vel, pref, rad, spd, new PlaneWall());
            }

            float settled = pos[0].x;
            float prev = settled;

            // 한 스텝 더 — 바깥으로 흘러가지 않고(정지) 같은 자리를 유지해야 한다.
            Step(solver, pos, vel, pref, rad, spd, new PlaneWall());

            Assert.GreaterOrEqual(settled, 0.5f - 1e-3f, "벽을 파고들면 안 된다(반경 밖).");
            // 단일 벽 접촉은 SOR(1.2) 보정상 표면에서 살짝 떨어진 곳(~0.57)에 정착한다.
            Assert.LessOrEqual(settled, 0.62f, "공간이 없으면 벽 앞에 멈춰야 한다(바깥으로 새지 않음).");
            Assert.That(pos[0].x, Is.EqualTo(prev).Within(2e-2f), "정착 후엔 더 이동하지 않아야 한다(정지).");
        }

        // 회귀: 예전 '벽 접선 투영(wallClearance 1.5)' 휴리스틱은 벽에 붙은 플레이어로의
        // 접근 속도를 통째로 죽여 적이 1.5 유닛 밖에서 미끄러지기만 했다(사용자 보고).
        // PBD에선 접선 밴드가 없으므로 적은 벽 표면의 목표까지 끝까지 접근해야 한다.
        [Test]
        public void AgentReachesGoalHuggingWall()
        {
            var solver = MakeSolver();
            var walls = new PlaneWall();
            var goal = new Vector3(0.5f, 0f, 0f); // 플레이어가 벽 표면에 바짝 붙어 있음
            const float speed = 5f;

            var pos = new List<Vector3> { new Vector3(5f, 0f, 0f) };
            var vel = new List<Vector3> { Vector3.zero };
            var pref = new List<Vector3> { Vector3.zero };
            var rad = new List<float> { 0.5f };
            var spd = new List<float> { speed };

            for (int i = 0; i < 120; i++)
            {
                Vector3 dir = goal - pos[0];
                dir.y = 0f;
                if (dir.sqrMagnitude > 1e-6f)
                {
                    dir.Normalize();
                }

                pref[0] = dir * speed;
                Step(solver, pos, vel, pref, rad, spd, walls);
            }

            Assert.LessOrEqual(pos[0].x, 1.0f, "적은 벽에 붙은 목표까지 접근해야 한다(접선 밴드에 막히지 않음).");
            Assert.GreaterOrEqual(pos[0].x, 0.4f, "그러면서도 벽을 파고들지는 않아야 한다.");
        }

        [Test]
        public void Velocity_IsClampedToMaxSpeed()
        {
            var solver = new CrowdSolver
            {
                VelocityBlend = 1f,
                MaxAccel = 100000f, // 가속은 자유, 속도만 검증
                EnableViscosity = false,
                EnableLongRange = false,
            };
            var pos = new List<Vector3> { Vector3.zero };
            var vel = new List<Vector3> { Vector3.zero };
            var pref = new List<Vector3> { new Vector3(1000f, 0f, 0f) }; // 비현실적으로 빠른 선호속도
            var rad = new List<float> { 0.5f };
            var spd = new List<float> { 5f }; // 최대 5

            Step(solver, pos, vel, pref, rad, spd, null);

            Assert.LessOrEqual(vel[0].magnitude, 5f + 1e-3f, "속도는 maxSpeed로 제한돼야 한다.");
            Assert.LessOrEqual(pos[0].x, 5f * Dt + 1e-3f, "한 프레임 변위도 maxSpeed*dt 이하여야 한다.");
        }
    }
}
