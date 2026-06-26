using NUnit.Framework;
using Tartisians.Systems.Crowd;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    /// <summary>
    /// 해석적 AABB 충돌장(<see cref="ObstacleField"/>) 테스트. 핵심은 거리·법선의 **연속성**:
    /// 벽을 따라 미끄러져도 값이 매끈해 격자 SDF의 계단/스냅 떨림이 없어야 한다.
    /// </summary>
    public class ObstacleFieldTests
    {
        // +X 둘레 벽: 월드 x∈[19,20], z∈[-20,20].
        static ObstacleField MakeWall()
        {
            var f = new ObstacleField();
            f.Add(new Vector3(19f, 0f, -20f), new Vector3(20f, 1f, 20f));
            return f;
        }

        [Test]
        public void OutsidePoint_DistanceAndOutwardNormal()
        {
            var f = MakeWall();
            float d = f.Distance(new Vector3(18.5f, 0f, 0f));
            Vector3 n = f.Normal(new Vector3(18.5f, 0f, 0f));

            Assert.AreEqual(0.5f, d, 1e-4f, "벽 표면까지 0.5여야 한다.");
            Assert.AreEqual(-1f, n.x, 1e-4f, "법선은 벽 반대(-X)를 가리켜야 한다.");
            Assert.AreEqual(0f, n.z, 1e-4f);
        }

        [Test]
        public void InsidePoint_NegativeDistance_PushesOut()
        {
            var f = MakeWall();
            float d = f.Distance(new Vector3(19.4f, 0f, 3f)); // 벽 안으로 침투
            Vector3 n = f.Normal(new Vector3(19.4f, 0f, 3f));

            Assert.Less(d, 0f, "박스 안이면 부호거리는 음수여야 한다.");
            Assert.AreEqual(-1f, n.x, 1e-4f, "최소 침투 축(-X)으로 밀어내야 한다.");
        }

        // 떨림 회귀: 벽을 따라(z 변화) 미끄러질 때 거리·법선이 일정해야 한다(매끈함).
        [Test]
        public void SlidingAlongWall_DistanceAndNormalAreContinuous()
        {
            var f = MakeWall();
            float baseDist = f.Distance(new Vector3(18.6f, 0f, -8f));

            for (float z = -8f; z <= 8f; z += 0.37f)
            {
                Vector3 p = new(18.6f, 0f, z);
                float d = f.Distance(p);
                Vector3 n = f.Normal(p);

                Assert.AreEqual(baseDist, d, 1e-4f, $"z={z}: 벽 따라 거리가 일정해야 한다(계단 없음).");
                Assert.AreEqual(-1f, n.x, 1e-4f, $"z={z}: 법선 방향이 흔들리면 안 된다.");
                Assert.AreEqual(0f, n.z, 1e-4f, $"z={z}: 접선 성분 스냅이 없어야 한다.");
            }
        }

        [Test]
        public void NoBoxes_ReturnsLargeDistance_ZeroNormal()
        {
            var f = new ObstacleField();
            Assert.Greater(f.Distance(Vector3.zero), 1000f, "박스가 없으면 큰 거리.");
            Assert.AreEqual(Vector3.zero, f.Normal(Vector3.zero), "박스가 없으면 법선 zero.");
        }

        // 시야(LOS): 선분이 벽 박스를 관통하면 차단, 아니면 통과.
        [Test]
        public void Blocks_SegmentThroughWall_IsBlocked()
        {
            var f = MakeWall(); // x[19,20], z[-20,20]
            Assert.IsTrue(f.Blocks(new Vector3(10f, 0f, 0f), new Vector3(30f, 0f, 0f)), "벽을 가로지르면 차단.");
        }

        [Test]
        public void Blocks_SegmentBeforeWall_NotBlocked()
        {
            var f = MakeWall();
            Assert.IsFalse(f.Blocks(new Vector3(10f, 0f, 0f), new Vector3(15f, 0f, 0f)), "벽 앞에서 끝나면 통과.");
        }

        [Test]
        public void Blocks_SegmentPastWallZ_NotBlocked()
        {
            var f = MakeWall();
            Assert.IsFalse(f.Blocks(new Vector3(10f, 0f, 30f), new Vector3(30f, 0f, 30f)), "벽 z범위 밖(평행)이면 통과.");
        }

        [Test]
        public void Blocks_SameSideOfWall_NotBlocked()
        {
            var f = MakeWall();
            Assert.IsFalse(f.Blocks(new Vector3(5f, 0f, -5f), new Vector3(12f, 0f, 8f)), "벽 같은 쪽 두 점은 통과.");
        }
    }
}
