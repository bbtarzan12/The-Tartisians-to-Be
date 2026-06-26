#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using Tartisians.Gameplay.Enemies;
using Tartisians.Gameplay.Flow;
using Tartisians.Gameplay.Input;
using Tartisians.Gameplay.Player;
using UnityEngine;

namespace Tartisians.Gameplay.DevTools
{
    /// <summary>
    /// 에디터 전용 자동 플레이 드라이버(검증용 영상 촬영). 마커 파일이 있을 때만 활성화되며
    /// 빌드에는 포함되지 않는다(#if UNITY_EDITOR). 군중을 카이팅(원형 유도)하며 벽을 피하고,
    /// 레벨업 시 첫 카드를 자동 선택한다. PlayerController의 입력을 런타임에 가로챈다.
    /// </summary>
    public sealed class AutoPlayDriver : MonoBehaviour, IMoveInputSource
    {
        public Vector2 MoveInput { get; private set; }

        Transform _player;
        GameDirector _director;
        bool _hooked;

        [Serializable]
        public sealed class Config
        {
            public int frames = 600;
            public int everySteps = 2;
            public int width = 854;
            public int height = 480;
            public string outDir;
        }

        static string MarkerPath =>
            Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Temp", "tartisians_autoplay.json");

        /// <summary>마커 파일이 있으면 자동 플레이 드라이버 + 레코더를 씬에 주입한다.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (!File.Exists(MarkerPath))
            {
                return;
            }

            Config cfg;
            try
            {
                cfg = JsonUtility.FromJson<Config>(File.ReadAllText(MarkerPath));
            }
            catch (Exception e)
            {
                Debug.LogError($"[AutoPlay] 마커 파싱 실패: {e.Message}");
                return;
            }

            // 에디터 창이 비포커스여도 플레이 루프가 멈추지 않도록(녹화 안정성).
            Application.runInBackground = true;

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                player.AddComponent<AutoPlayDriver>();
            }

            var recGo = new GameObject("PlaytestRecorder");
            recGo.AddComponent<PlaytestRecorder>().Configure(cfg, MarkerPath);
            Debug.Log($"[AutoPlay] 활성화: frames={cfg.frames} every={cfg.everySteps} {cfg.width}x{cfg.height} → {cfg.outDir}");
        }

        void Start()
        {
            _player = transform;
            _director = FindAnyObjectByType<GameDirector>();
            HookInput();
        }

        void HookInput()
        {
            if (_hooked || _player == null)
            {
                return;
            }

            if (_player.TryGetComponent(out PlayerController pc))
            {
                FieldInfo f = typeof(PlayerController).GetField("_input", BindingFlags.NonPublic | BindingFlags.Instance);
                f?.SetValue(pc, this);
                _hooked = true;
            }
        }

        void Update()
        {
            HookInput();

            // 레벨업이면 첫 카드 자동 선택(시간정지 중에도 Update는 돈다).
            if (_director != null && _director.Current == GameDirector.Phase.LevelUp)
            {
                _director.SelectUpgrade(0);
            }

            MoveInput = ComputeMove();
        }

        Vector2 ComputeMove()
        {
            Vector3 me = _player.position;
            me.y = 0f;

            // 벽 근처(반경 14) 원을 선회 → 군중이 따라잡아 둘러싸고 바깥쪽은 벽에 밀착.
            const float orbitRadius = 14f;
            float ang = Time.time * 0.25f;
            Vector3 target = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * orbitRadius;
            Vector3 orbitDir = target - me;
            orbitDir.y = 0f;
            if (orbitDir.sqrMagnitude > 1e-4f)
            {
                orbitDir.Normalize();
            }

            // 근접 회피: 아주 가까운 적(≤3.5)에서 밀려나 클럼프를 피해 생존 연장.
            Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsInactive.Exclude);
            Vector3 avoid = Vector3.zero;
            for (int i = 0; i < enemies.Length; i++)
            {
                Vector3 p = enemies[i].Position;
                p.y = 0f;
                Vector3 d = me - p;
                float dist = d.magnitude;
                if (dist > 1e-3f && dist < 3.5f)
                {
                    avoid += d / dist * (1f - dist / 3.5f);
                }
            }

            if (avoid.sqrMagnitude > 1e-4f)
            {
                avoid.Normalize();
            }

            Vector3 dir = orbitDir * 0.7f + avoid * 1.1f; // 회피 우세 → 둘러싸이되 안 죽음
            dir.y = 0f;
            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            return new Vector2(dir.x, dir.z) * 0.85f;
        }
    }
}
#endif
