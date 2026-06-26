using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Systems.Crowd
{
    /// <summary>
    /// 축정렬 박스(AABB) 집합에 대한 해석적 SDF. PBD 군중 솔버의 벽 제약에 사용한다.
    /// 격자 거리장과 달리 거리·법선이 연속이라(계단/스냅 없음) 벽 근처 떨림이 없다.
    /// 모두 XZ 평면 기준(y 무시). 논문의 "장애물의 가장 가까운 점, 무한 질량" 처리에 해당.
    /// </summary>
    public sealed class ObstacleField : IObstacleField
    {
        readonly List<Rect> _boxes = new(); // Rect(xMin, zMin, width, depth)

        public void Clear() => _boxes.Clear();

        /// <summary>월드 AABB(min/max)를 XZ 박스로 추가한다.</summary>
        public void Add(Vector3 min, Vector3 max)
        {
            _boxes.Add(new Rect(min.x, min.z, max.x - min.x, max.z - min.z));
        }

        /// <summary>가장 가까운 박스까지의 부호거리(밖=양수, 안=음수). 박스 없으면 큰 값.</summary>
        public float Distance(Vector3 world)
        {
            float best = float.MaxValue;
            Vector2 p = new(world.x, world.z);
            for (int i = 0; i < _boxes.Count; i++)
            {
                float d = SignedDistance(_boxes[i], p);
                if (d < best)
                {
                    best = d;
                }
            }

            return best;
        }

        /// <summary>가장 가까운 박스에서 멀어지는 단위 방향(XZ). 박스 없으면 zero.</summary>
        public Vector3 Normal(Vector3 world)
        {
            Vector2 p = new(world.x, world.z);
            int nearest = -1;
            float best = float.MaxValue;
            for (int i = 0; i < _boxes.Count; i++)
            {
                float d = SignedDistance(_boxes[i], p);
                if (d < best)
                {
                    best = d;
                    nearest = i;
                }
            }

            if (nearest < 0)
            {
                return Vector3.zero;
            }

            Vector2 n = OutwardNormal(_boxes[nearest], p);
            return new Vector3(n.x, 0f, n.y);
        }

        /// <summary>선분 a→b(XZ)가 어떤 박스 내부를 관통하면 true(시야 차단·투사체 충돌 판정용).</summary>
        public bool Blocks(Vector3 a, Vector3 b)
        {
            Vector2 p = new(a.x, a.z);
            Vector2 q = new(b.x, b.z);
            for (int i = 0; i < _boxes.Count; i++)
            {
                if (SegmentIntersectsRect(p, q, _boxes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        // Liang-Barsky 선분-사각형 교차(XZ). 선분이 사각형 내부와 겹치면 true.
        static bool SegmentIntersectsRect(Vector2 p, Vector2 q, Rect r)
        {
            float dx = q.x - p.x;
            float dy = q.y - p.y;
            float t0 = 0f;
            float t1 = 1f;
            if (!Clip(-dx, p.x - r.xMin, ref t0, ref t1)) return false;
            if (!Clip(dx, r.xMax - p.x, ref t0, ref t1)) return false;
            if (!Clip(-dy, p.y - r.yMin, ref t0, ref t1)) return false;
            if (!Clip(dy, r.yMax - p.y, ref t0, ref t1)) return false;
            return t1 >= t0;
        }

        static bool Clip(float p, float q, ref float t0, ref float t1)
        {
            if (Mathf.Abs(p) < 1e-6f)
            {
                return q >= 0f; // 슬랩에 평행: 슬랩 안이면 통과, 밖이면 교차 없음
            }

            float t = q / p;
            if (p < 0f)
            {
                if (t > t1) return false;
                if (t > t0) t0 = t;
            }
            else
            {
                if (t < t0) return false;
                if (t < t1) t1 = t;
            }

            return true;
        }

        static float SignedDistance(Rect b, Vector2 p)
        {
            float cx = Mathf.Clamp(p.x, b.xMin, b.xMax);
            float cy = Mathf.Clamp(p.y, b.yMin, b.yMax);
            float ox = p.x - cx;
            float oy = p.y - cy;
            float outside = Mathf.Sqrt(ox * ox + oy * oy);
            if (outside > 1e-5f)
            {
                return outside; // 박스 밖: 표면까지 유클리드 거리
            }

            // 박스 안: 가장 가까운 면까지의 침투 깊이(음수)
            float inX = Mathf.Min(p.x - b.xMin, b.xMax - p.x);
            float inY = Mathf.Min(p.y - b.yMin, b.yMax - p.y);
            return -Mathf.Min(inX, inY);
        }

        static Vector2 OutwardNormal(Rect b, Vector2 p)
        {
            float cx = Mathf.Clamp(p.x, b.xMin, b.xMax);
            float cy = Mathf.Clamp(p.y, b.yMin, b.yMax);
            Vector2 d = new(p.x - cx, p.y - cy);
            if (d.sqrMagnitude > 1e-10f)
            {
                return d.normalized; // 밖: 표면점→점 방향
            }

            // 안: 최소 침투 축으로 밀어냄
            float left = p.x - b.xMin;
            float right = b.xMax - p.x;
            float down = p.y - b.yMin;
            float up = b.yMax - p.y;
            float m = Mathf.Min(Mathf.Min(left, right), Mathf.Min(down, up));
            if (m == left) return Vector2.left;
            if (m == right) return Vector2.right;
            if (m == down) return Vector2.down;
            return Vector2.up;
        }
    }
}
