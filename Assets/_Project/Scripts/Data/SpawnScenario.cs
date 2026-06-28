using System;
using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 한 판(기본 5분)의 시간축 스폰 시나리오.
    /// 시간대별 단계(Phase: 간격/최대생존/적 구성 가중치)와 일회성 버스트(swarm 스파이크)로 난도 곡선을 만든다.
    /// 경과 시간으로 EnemySpawner가 구동한다. 순수 선택 로직은 <see cref="Tartisians.Gameplay.Enemies.SpawnSchedule"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Spawn Scenario", fileName = "SpawnScenario")]
    public sealed class SpawnScenario : ScriptableObject
    {
        /// <summary>단계 내 한 적 종류의 가중치(상대 비율).</summary>
        [Serializable]
        public struct Weight
        {
            public EnemyDefinition Enemy;
            public float Value;
        }

        /// <summary>특정 시각부터 적용되는 정상 스폰 규칙.</summary>
        [Serializable]
        public sealed class Phase
        {
            [Tooltip("이 단계가 시작되는 경과 시간(초). 오름차순 정렬 가정.")]
            public float StartTime;
            [Tooltip("스폰 간격(초). 작을수록 밀도 높음.")]
            public float SpawnInterval = 0.25f;
            [Tooltip("동시 생존 적 상한.")]
            public int MaxAlive = 100;
            [Tooltip("플레이어 기준 스폰 링 안쪽 반경. 0 이하면 기본값 사용.")]
            public float SpawnRadius;
            [Tooltip("적 종류별 가중치. 비면 폴백으로 첫 항목.")]
            public Weight[] Weights;
            public string Label;
        }

        /// <summary>특정 시각에 한 번 터지는 무리 스폰(정상 상한 무시, 풀 한계까지).</summary>
        [Serializable]
        public struct Burst
        {
            public float Time;
            public EnemyDefinition Enemy;
            public int Count;
            public string Label;
        }

        [SerializeField] Phase[] _phases;
        [SerializeField] Burst[] _bursts;
        [SerializeField] float _defaultSpawnRadius = 18f;

        public int PhaseCount => _phases != null ? _phases.Length : 0;
        public int BurstCount => _bursts != null ? _bursts.Length : 0;
        public bool HasPhases => PhaseCount > 0;
        public float DefaultSpawnRadius => _defaultSpawnRadius;

        public Phase GetPhase(int index) => _phases[index];
        public Burst GetBurst(int index) => _bursts[index];

        /// <summary>경과 시간에 해당하는 단계. StartTime이 time 이하인 마지막 단계(없으면 첫 단계).</summary>
        public Phase PhaseAt(float time)
        {
            if (!HasPhases)
            {
                return null;
            }

            int idx = 0;
            for (int i = 0; i < _phases.Length; i++)
            {
                if (_phases[i].StartTime <= time)
                {
                    idx = i;
                }
                else
                {
                    break;
                }
            }

            return _phases[idx];
        }

        /// <summary>이 단계 반경(0 이하면 시나리오 기본값).</summary>
        public float RadiusOf(Phase phase)
        {
            if (phase != null && phase.SpawnRadius > 0f)
            {
                return phase.SpawnRadius;
            }

            return _defaultSpawnRadius;
        }
    }
}
