using System.Collections.Generic;
using NUnit.Framework;
using Tartisians.Systems.Spatial;
using UnityEngine;

namespace Tartisians.Tests.EditMode
{
    public class SpatialHashGridTests
    {
        [Test]
        public void Query_FindsNeighborsWithinRadius_ExcludesFar()
        {
            var positions = new List<Vector3>
            {
                new Vector3(0f, 0f, 0f),   // 0: self
                new Vector3(0.5f, 0f, 0f), // 1: 가까움
                new Vector3(1.0f, 0f, 0f), // 2: 경계 내
                new Vector3(5.0f, 0f, 0f), // 3: 멈
            };

            var grid = new SpatialHashGrid(2f);
            grid.Rebuild(positions);

            var results = new List<int>();
            grid.Query(new Vector3(0f, 0f, 0f), 1.5f, results);

            CollectionAssert.Contains(results, 0);
            CollectionAssert.Contains(results, 1);
            CollectionAssert.Contains(results, 2);
            CollectionAssert.DoesNotContain(results, 3);
        }

        [Test]
        public void Query_AcrossCellBoundary_StillFinds()
        {
            // 셀 크기 1 → 인접 셀에 걸친 두 점을 반경이 포함해야 한다.
            var positions = new List<Vector3>
            {
                new Vector3(0.9f, 0f, 0f),
                new Vector3(1.1f, 0f, 0f),
            };

            var grid = new SpatialHashGrid(1f);
            grid.Rebuild(positions);

            var results = new List<int>();
            grid.Query(new Vector3(0.9f, 0f, 0f), 0.5f, results);

            CollectionAssert.Contains(results, 1);
        }

        [Test]
        public void Query_EmptyArea_ReturnsNothing()
        {
            var positions = new List<Vector3> { new Vector3(100f, 0f, 100f) };
            var grid = new SpatialHashGrid(2f);
            grid.Rebuild(positions);

            var results = new List<int>();
            grid.Query(Vector3.zero, 3f, results);

            Assert.AreEqual(0, results.Count);
        }
    }
}
