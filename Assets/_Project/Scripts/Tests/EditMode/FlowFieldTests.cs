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

        // 윈도우 BFS: 목표 셀(20,20) 주변 반경 5 창만 계산 → 창 안은 목표로 향하고 창 밖은 zero.
        [Test]
        public void ComputeWindow_OnlyComputesAroundGoal()
        {
            var ff = new FlowField(Vector3.zero, 1f, 40, 40);
            ff.ComputeWindow(new Vector3(20.5f, 0f, 20.5f), 5); // 목표 셀 (20,20), 창 [15..25]

            Assert.AreEqual(0, ff.GetCost(20, 20), "목표 셀 비용 0.");
            Assert.AreEqual(5, ff.GetCost(25, 20), "창 가장자리(거리 5)까지 BFS 도달.");
            Assert.AreEqual(5, ff.GetCost(15, 20), "반대 가장자리도 거리 5.");

            // 창 안: 목표(-x) 방향
            Vector3 inDir = ff.SampleDirection(new Vector3(23.5f, 0f, 20.5f)); // 셀 (23,20)
            Assert.Less(inDir.x, 0f, "창 안 흐름은 목표(-x)를 향해야 한다.");

            // 창 밖: zero(→ 직선 추적 폴백)
            Assert.AreEqual(Vector3.zero, ff.SampleDirection(new Vector3(30.5f, 0f, 20.5f)), "창 밖은 흐름 없음(zero).");
        }

        // 윈도우 안에서도 벽을 우회해야 한다.
        [Test]
        public void ComputeWindow_RoutesAroundWallInsideWindow()
        {
            var ff = new FlowField(Vector3.zero, 1f, 40, 40);
            for (int y = 16; y <= 24; y++)
            {
                ff.SetBlocked(22, y, true); // 목표(20,20)와 (24,20) 사이 세로 벽(통로는 위아래)
            }

            ff.ComputeWindow(new Vector3(20.5f, 0f, 20.5f), 8);

            ushort behind = ff.GetCost(24, 20); // 벽 뒤
            Assert.AreNotEqual(ushort.MaxValue, behind, "창 안에 통로가 있으면 우회 도달 가능.");
            Assert.Greater(behind, 4, "우회 경로는 직선 거리(4)보다 길어야 한다.");
        }
    }
}
