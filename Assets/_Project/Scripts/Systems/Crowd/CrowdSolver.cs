using System;
using System.Collections.Generic;
using Tartisians.Systems.Spatial;
using UnityEngine;

namespace Tartisians.Systems.Crowd
{
    /// <summary>
    /// Position-Based Multi-Agent Dynamics(Weiss/Karamouzas, MiG 2017 Best Paper) 기반 군중 솔버.
    /// 적-적 비침투와 적-벽 비침투를 '하나의 위치 제약 투영'으로 통일한다.
    ///
    /// 흐름(논문 Algorithm 1, XZ 평면 적용):
    ///   1) 속도 블렌딩  v = lerp(v_prev, v_preferred, α)
    ///   2) 예측 위치    x* = x + v·dt
    ///   3) 이웃 탐색(예측 위치 기준, 공간 해시)
    ///   4) 안정화 반복  기존 침투를 마찰 접촉으로 해소(x_n·x*에 함께 적용)
    ///   5) 주 반복      단거리 마찰 접촉 + 장거리(예측) 회피 + 벽(무한 질량)  — Jacobi delta-averaging
    ///   6) 속도 복원    v = (x* - x)/dt → XSPH 점성 → 최대 가속/속도 클램프
    ///
    /// '밀지 않음 / 공간 없으면 정지'는 하드 비침투 제약에서 자연히 나온다(별도 휴리스틱 불필요).
    /// 순수 로직이라 EditMode에서 단위 테스트한다.
    /// </summary>
    [Serializable]
    public sealed class CrowdSolver
    {
        const float Eps = 1e-5f;

        // ── 파라미터(논문 기반, 게임 튜닝용 인스펙터 노출) ──
        [Header("Integration")]
        [Tooltip("관성↔선호속도 블렌딩 계수 α. 논문 0.0385(현실 군중). 게임 반응성 위해 상향.")]
        [Range(0f, 1f)] public float VelocityBlend = 0.25f;
        [Tooltip("기존 침투 해소용 안정화 반복 횟수(논문 1).")]
        public int StabilityIterations = 1;
        [Tooltip("주 제약 반복 횟수(논문 6).")]
        public int MainIterations = 6;
        [Tooltip("delta-averaging 과완화(SOR) 계수(논문 1.2).")]
        public float Sor = 1.2f;

        [Header("Friction")]
        [Range(0f, 1f)] public float StaticFriction = 0.7f;
        [Range(0f, 1f)] public float KineticFriction = 0.4f;

        [Header("Long-range avoidance")]
        [Tooltip("장거리 예측 회피 활성화(미래 충돌을 미리 비켜감).")]
        public bool EnableLongRange = true;
        [Range(0f, 1f)] public float LongRangeStiffness = 0.5f;
        [Tooltip("이 시간(초) 안에 충돌하는 쌍만 예측 회피 대상.")]
        public float LongRangeHorizon = 0.5f;
        [Tooltip("장거리 강성의 시간 감쇠 상수 τ0(초). exp(-(τ/τ0)²).")]
        public float Tau0 = 0.3f;

        [Header("Cohesion / smoothing")]
        [Tooltip("XSPH 점성: 이웃 평균 속도로 블렌딩해 군중 흐름을 매끄럽게.")]
        public bool EnableViscosity = true;
        [Range(0f, 1f)] public float ViscosityC = 0.1f;
        public float ViscosityRadius = 1.6f;

        [Header("Post-process clamp")]
        [Tooltip("최대 가속(유닛/초²) — 제약 보정으로 속도가 튀는 것을 방지(논문 후처리).")]
        public float MaxAccel = 40f;

        // ── 스크래치 버퍼(프레임 간 재사용, 무할당) ──
        Vector3[] _xn, _xstar, _vn, _dx, _vNew, _vVisc;
        float[] _radius, _maxSpeed;
        int[] _ccount;
        int[] _nbrStart;
        readonly List<Vector3> _predicted = new(256);
        readonly List<int> _query = new(64);
        readonly List<int> _nbrFlat = new(2048);

        void Ensure(int n)
        {
            if (_xn != null && _xn.Length >= n)
            {
                return;
            }

            int cap = Mathf.NextPowerOfTwo(Mathf.Max(n, 64));
            _xn = new Vector3[cap];
            _xstar = new Vector3[cap];
            _vn = new Vector3[cap];
            _dx = new Vector3[cap];
            _vNew = new Vector3[cap];
            _vVisc = new Vector3[cap];
            _radius = new float[cap];
            _maxSpeed = new float[cap];
            _ccount = new int[cap];
            _nbrStart = new int[cap + 1];
        }

        /// <summary>
        /// 한 스텝 시뮬레이션. positions/velocities는 in/out(XZ; y는 0으로 다룬다).
        /// grid는 솔버가 예측 위치로 재구성하므로 호출자가 미리 채울 필요 없다.
        /// </summary>
        public void Step(
            int count,
            IList<Vector3> positions,
            IList<Vector3> velocities,
            IReadOnlyList<Vector3> preferredVelocities,
            IReadOnlyList<float> radii,
            IReadOnlyList<float> maxSpeeds,
            SpatialHashGrid grid,
            IObstacleField obstacles,
            float dt)
        {
            if (count <= 0 || dt <= 0f)
            {
                return;
            }

            Ensure(count);

            // 1) 블렌딩 + 예측
            float maxR = 0f;
            float maxSpeedGlobal = 0f;
            for (int i = 0; i < count; i++)
            {
                Vector3 xn = positions[i];
                xn.y = 0f;
                Vector3 vN = velocities[i];
                vN.y = 0f;
                Vector3 vP = preferredVelocities[i];
                vP.y = 0f;

                _xn[i] = xn;
                _vn[i] = vN;
                _radius[i] = radii[i];
                _maxSpeed[i] = maxSpeeds[i];
                if (radii[i] > maxR) maxR = radii[i];
                if (maxSpeeds[i] > maxSpeedGlobal) maxSpeedGlobal = maxSpeeds[i];

                Vector3 vb = Vector3.Lerp(vN, vP, VelocityBlend);
                _xstar[i] = xn + vb * dt;
            }

            // 2) 이웃 탐색(예측 위치 기준) → CSR 평면 리스트(무할당)
            _predicted.Clear();
            for (int i = 0; i < count; i++)
            {
                _predicted.Add(_xstar[i]);
            }

            grid.Rebuild(_predicted);

            float pad = EnableLongRange ? maxSpeedGlobal * LongRangeHorizon : 0f;
            _nbrFlat.Clear();
            for (int i = 0; i < count; i++)
            {
                _nbrStart[i] = _nbrFlat.Count;
                grid.Query(_xstar[i], _radius[i] + maxR + pad, _query);
                for (int k = 0; k < _query.Count; k++)
                {
                    int j = _query[k];
                    if (j != i)
                    {
                        _nbrFlat.Add(j);
                    }
                }
            }

            _nbrStart[count] = _nbrFlat.Count;

            // 3) 안정화 반복: 기존 침투 해소(x_n·x* 함께 보정)
            for (int it = 0; it < StabilityIterations; it++)
            {
                ClearDeltas(count);
                ResolveConstraints(count, _xn, obstacles, dt, longRange: false);
                ApplyDeltas(count, alsoBase: true);
            }

            // 4) 주 반복: 단거리 마찰 접촉 + 장거리 회피 + 벽 (Jacobi)
            for (int it = 0; it < MainIterations; it++)
            {
                ClearDeltas(count);
                ResolveConstraints(count, _xstar, obstacles, dt, longRange: EnableLongRange);
                ApplyDeltas(count, alsoBase: false);
            }

            // 5) 속도 복원
            for (int i = 0; i < count; i++)
            {
                Vector3 v = (_xstar[i] - _xn[i]) / dt;
                v.y = 0f;
                _vNew[i] = v;
            }

            // 5b) XSPH 점성(이웃 평균 속도로 매끄럽게)
            if (EnableViscosity && ViscosityC > 0f && ViscosityRadius > Eps)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector3 vi = _vNew[i];
                    Vector3 acc = Vector3.zero;
                    float wsum = 0f;
                    for (int k = _nbrStart[i]; k < _nbrStart[i + 1]; k++)
                    {
                        int j = _nbrFlat[k];
                        Vector3 r = _xstar[i] - _xstar[j];
                        r.y = 0f;
                        float q = r.magnitude;
                        if (q < ViscosityRadius && q > Eps)
                        {
                            float w = 1f - q / ViscosityRadius;
                            acc += (_vNew[j] - vi) * w;
                            wsum += w;
                        }
                    }

                    _vVisc[i] = wsum > 0f ? vi + ViscosityC * acc / wsum : vi;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    _vVisc[i] = _vNew[i];
                }
            }

            // 6) 최대 가속/속도 클램프 → 위치를 클램프된 속도와 일관되게 기록
            float maxDv = MaxAccel * dt;
            for (int i = 0; i < count; i++)
            {
                Vector3 v = _vVisc[i];

                Vector3 dv = v - _vn[i];
                if (dv.sqrMagnitude > maxDv * maxDv)
                {
                    v = _vn[i] + dv.normalized * maxDv;
                }

                float ms = _maxSpeed[i];
                if (ms > 0f && v.sqrMagnitude > ms * ms)
                {
                    v = v.normalized * ms;
                }

                v.y = 0f;
                velocities[i] = v;

                Vector3 np = _xn[i] + v * dt;
                np.y = 0f;
                positions[i] = np;
            }
        }

        void ClearDeltas(int count)
        {
            Array.Clear(_dx, 0, count);
            Array.Clear(_ccount, 0, count);
        }

        void ApplyDeltas(int count, bool alsoBase)
        {
            for (int i = 0; i < count; i++)
            {
                if (_ccount[i] <= 0)
                {
                    continue;
                }

                Vector3 delta = _dx[i] / _ccount[i] * Sor;
                _xstar[i] += delta;
                if (alsoBase)
                {
                    _xn[i] += delta;
                }
            }
        }

        /// <summary>
        /// 하나의 제약 투영 패스(Jacobi): 벽(무한 질량) + 적-적 단거리 마찰 접촉 + (옵션)장거리 예측 회피.
        /// X(=현재 기준 위치 배열)에서 위반을 읽어 _dx에 누적하고 _ccount를 센다.
        /// </summary>
        void ResolveConstraints(int count, Vector3[] x, IObstacleField obstacles, float dt, bool longRange)
        {
            for (int i = 0; i < count; i++)
            {
                // 벽: 가장 가까운 장애물 점과의 충돌(무한 질량 → 적만 밀려남)
                if (obstacles != null)
                {
                    float dWall = obstacles.Distance(x[i]);
                    if (dWall < _radius[i])
                    {
                        Vector3 nrm = obstacles.Normal(x[i]);
                        if (nrm != Vector3.zero)
                        {
                            _dx[i] += nrm * (_radius[i] - dWall);
                            _ccount[i]++;
                        }
                    }
                }

                // 적-적: 각 쌍을 j>i로 한 번만 처리해 양쪽에 적용
                for (int k = _nbrStart[i]; k < _nbrStart[i + 1]; k++)
                {
                    int j = _nbrFlat[k];
                    if (j <= i)
                    {
                        continue;
                    }

                    float minD = _radius[i] + _radius[j];
                    Vector3 d = x[i] - x[j];
                    d.y = 0f;
                    float dist = d.magnitude;

                    if (dist < minD && dist > Eps)
                    {
                        // 단거리 비침투(법선) + 마찰(접선)
                        Vector3 n = d / dist;
                        float pen = minD - dist;

                        Vector3 nc = n * (pen * 0.5f);
                        _dx[i] += nc;
                        _dx[j] -= nc;
                        _ccount[i]++;
                        _ccount[j]++;

                        // 마찰: 이번 스텝의 접선 상대 변위를 억제(Macklin granular)
                        Vector3 rel = (_xstar[i] - _xn[i]) - (_xstar[j] - _xn[j]);
                        rel.y = 0f;
                        Vector3 relT = rel - Vector3.Dot(rel, n) * n;
                        float rtm = relT.magnitude;
                        if (rtm > Eps)
                        {
                            float maxT = StaticFriction * pen;
                            Vector3 fr = rtm < maxT ? relT : relT * (KineticFriction * pen / rtm);
                            Vector3 fc = fr * 0.5f;
                            _dx[i] -= fc;
                            _dx[j] += fc;
                        }
                    }
                    else if (longRange && dist >= minD)
                    {
                        // 장거리 예측: 충돌까지 시간 τ를 구해 미래 충돌 위치에서 미리 밀어냄
                        Vector3 p0 = _xn[i] - _xn[j];
                        p0.y = 0f;
                        Vector3 ps = _xstar[i] - _xstar[j];
                        ps.y = 0f;

                        float a = ps.sqrMagnitude / (dt * dt);
                        float b = -Vector3.Dot(p0, ps) / dt;
                        float c = p0.sqrMagnitude - minD * minD;
                        float disc = b * b - a * c;

                        if (a > Eps && disc > 0f)
                        {
                            float tau = (b - Mathf.Sqrt(disc)) / a;
                            if (tau > 0f && tau < LongRangeHorizon)
                            {
                                float tauHat = dt * Mathf.Floor(tau / dt);
                                Vector3 vi = (_xstar[i] - _xn[i]) / dt;
                                Vector3 vj = (_xstar[j] - _xn[j]) / dt;
                                Vector3 ci = _xstar[i] + vi * tauHat;
                                Vector3 cj = _xstar[j] + vj * tauHat;
                                Vector3 dc = ci - cj;
                                dc.y = 0f;
                                float dd = dc.magnitude;
                                if (dd < minD && dd > Eps)
                                {
                                    Vector3 nn = dc / dd;
                                    float pen2 = minD - dd;
                                    float kk = LongRangeStiffness * Mathf.Exp(-(tauHat * tauHat) / (Tau0 * Tau0));
                                    Vector3 cc = nn * (pen2 * 0.5f * kk);
                                    _dx[i] += cc;
                                    _dx[j] -= cc;
                                    _ccount[i]++;
                                    _ccount[j]++;
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
