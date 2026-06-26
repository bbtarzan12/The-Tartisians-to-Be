using NUnit.Framework;
using Tartisians.Systems.Navigation;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class FlowFieldTests
    {
        [Test]
        public void Compute_OpenGrid_CostGrowsWithDistance_FlowPointsToGoal()
        {
            var ff = new FlowField(Vector3.zero, 1f, 10, 10);
            ff.Compute(new Vector3(0.5f, 0f, 0.5f)); // 목표 셀 (0,0)

            Assert.AreEqual(0, ff.GetCost(0, 0));
            Assert.AreEqual(5, ff.GetCost(5, 0));
            Assert.AreEqual(9, ff.GetCost(9, 0));

            Vector3 dir = ff.SampleDirection(new Vector3(9.5f, 0f, 0.5f));
            Assert.Less(dir.x, 0f, "먼 셀의 흐름은 목표(-x) 방향을 향해야 한다.");
        }

        [Test]
        public void Compute_WallWithGap_RoutesAround_StillReachable()
        {
            var ff = new FlowField(Vector3.zero, 1f, 5, 5);
            // x=2 열을 y=0..2까지 막고 y=3,4는 통로로 둠
            ff.SetBlocked(2, 0, true);
            ff.SetBlocked(2, 1, true);
            ff.SetBlocked(2, 2, true);

            ff.Compute(new Vector3(0.5f, 0f, 0.5f)); // 목표 (0,0)

            ushort costFar = ff.GetCost(4, 0);
            Assert.AreNotEqual(ushort.MaxValue, costFar, "통로가 있으면 우회해 도달 가능해야 한다.");
            Assert.Greater(costFar, 4, "우회 경로는 직선 거리(4)보다 길어야 한다.");
        }

        [Test]
        public void Compute_FullWall_BehindIsUnreachable_FlowZero()
        {
            var ff = new FlowField(Vector3.zero, 1f, 5, 5);
            for (int y = 0; y < 5; y++)
            {
                ff.SetBlocked(2, y, true); // 완전 차단 벽
            }

            ff.Compute(new Vector3(0.5f, 0f, 0.5f));

            Assert.AreEqual(ushort.MaxValue, ff.GetCost(4, 0), "벽 뒤는 도달 불가여야 한다.");
            Assert.AreEqual(Vector3.zero, ff.SampleDirection(new Vector3(4.5f, 0f, 0.5f)));
        }

        [Test]
        public void SampleDirection_OutOfBounds_ReturnsZero()
        {
            var ff = new FlowField(Vector3.zero, 1f, 5, 5);
            ff.Compute(new Vector3(0.5f, 0f, 0.5f));
            Assert.AreEqual(Vector3.zero, ff.SampleDirection(new Vector3(-10f, 0f, 0f)));
        }

        // ── 거리장(SDF) + 그래디언트 ──

        static FlowField WallAtX5()
        {
            var ff = new FlowField(Vector3.zero, 1f, 10, 10);
            for (int y = 0; y < 10; y++)
            {
                ff.SetBlocked(5, y, true); // x=5 세로 벽
            }

            ff.Compute(new Vector3(0.5f, 0f, 0.5f)); // 거리장도 Compute에서 계산됨
            return ff;
        }

        [Test]
        public void DistanceToObstacle_ZeroAtWall_GrowsAway()
        {
            var ff = WallAtX5();
            Assert.AreEqual(0f, ff.DistanceToObstacle(new Vector3(5.5f, 0f, 5.5f)), 1e-4f); // 벽 셀
            Assert.AreEqual(1f, ff.DistanceToObstacle(new Vector3(4.5f, 0f, 5.5f)), 1e-4f); // 인접
            Assert.AreEqual(3f, ff.DistanceToObstacle(new Vector3(2.5f, 0f, 5.5f)), 1e-4f); // 3칸
        }

        [Test]
        public void ObstacleGradient_PointsAwayFromWall()
        {
            var ff = WallAtX5();
            Vector3 g = ff.ObstacleGradient(new Vector3(4.5f, 0f, 5.5f)); // 벽(x=5) 왼쪽
            Assert.Less(g.x, 0f, "벽 반대(-x)로 향해야 한다.");
        }

        [Test]
        public void PushOut_ConvergesToClearWall()
        {
            var ff = WallAtX5();
            const float radius = 1.2f;
            Vector3 pos = new Vector3(4.5f, 0f, 5.5f); // 벽에 너무 가까움(dist 1 < 1.2)

            for (int i = 0; i < 20; i++)
            {
                float d = ff.DistanceToObstacle(pos);
                if (d >= radius)
                {
                    break;
                }

                Vector3 grad = ff.ObstacleGradient(pos);
                pos += grad * (radius - d);
            }

            Assert.GreaterOrEqual(ff.DistanceToObstacle(pos), radius, "밀어내기 후 벽에서 반경 이상 떨어져야 한다.");
            Assert.Less(pos.x, 4.5f, "벽에서 멀어지는 방향으로 이동해야 한다.");
        }
    }
}
