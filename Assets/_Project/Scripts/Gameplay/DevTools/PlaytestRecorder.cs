#if UNITY_EDITOR
using System.IO;
using Tartisians.Gameplay.Flow;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tartisians.Gameplay.DevTools
{
    /// <summary>
    /// 에디터 전용 플레이 영상 레코더. URP <see cref="RenderPipeline.SubmitRenderRequest"/>로
    /// 메인 카메라를 RenderTexture에 강제 렌더 → PNG 시퀀스 저장(에디터 포커스 throttle과 무관).
    /// Update에서 unscaled dt를 interval로 클램프해 페이싱(버스트 캡처 방지). 완료/게임오버 시 종료.
    /// </summary>
    public sealed class PlaytestRecorder : MonoBehaviour
    {
        AutoPlayDriver.Config _cfg;
        string _markerPath;
        Camera _cam;
        RenderTexture _rt;
        Texture2D _tex;
        int _frame;
        bool _done;
        float _interval;   // 캡처 간격(실시간 초)
        float _accum;
        GameDirector _director;
        int _endFrames;    // 게임오버 후 더 담을 프레임(여운)

        public void Configure(AutoPlayDriver.Config cfg, string markerPath)
        {
            _cfg = cfg;
            _markerPath = markerPath;
        }

        void Start()
        {
            // 게임시간(FixedUpdate 0.02) 기준 everySteps 간격을 실시간 캡처 간격으로 환산.
            _interval = Mathf.Max(0.001f, _cfg.everySteps * 0.02f);
            _cam = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            _rt = new RenderTexture(_cfg.width, _cfg.height, 24) { name = "PlaytestRT" };
            _rt.Create();
            _tex = new Texture2D(_cfg.width, _cfg.height, TextureFormat.RGB24, false);

            if (string.IsNullOrEmpty(_cfg.outDir))
            {
                _cfg.outDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Temp", "playtest_frames");
            }

            if (Directory.Exists(_cfg.outDir))
            {
                foreach (string f in Directory.GetFiles(_cfg.outDir, "*.png"))
                {
                    File.Delete(f);
                }
            }
            else
            {
                Directory.CreateDirectory(_cfg.outDir);
            }

            string donePath = Path.Combine(_cfg.outDir, "done.txt");
            if (File.Exists(donePath))
            {
                File.Delete(donePath);
            }
        }

        // Update에서 페이싱. unscaled dt를 interval로 클램프 → 한 Update에서 버스트(중복) 캡처 방지.
        // (timeScale=0에도 unscaled는 흐르므로 레벨업/게임오버 화면도 캡처되어 정상 종료된다.)
        void Update()
        {
            if (_done || _cam == null)
            {
                return;
            }

            // 적이 충분히 모일 때까지 대기 후 녹화 시작(초기 빈 화면 회피).
            if (Time.time < 2.5f)
            {
                return;
            }

            // 게임오버/승리(정지화면)면 짧은 여운만 담고 종료 → 정지 프레임 낭비 방지.
            if (_director == null)
            {
                _director = FindAnyObjectByType<GameDirector>();
            }

            if (_director != null &&
                (_director.Current == GameDirector.Phase.GameOver || _director.Current == GameDirector.Phase.Victory))
            {
                if (_endFrames++ > 30)
                {
                    Finish();
                    return;
                }
            }

            _accum += Mathf.Min(Time.unscaledDeltaTime, _interval);
            if (_accum >= _interval)
            {
                _accum -= _interval;
                Capture();
                if (_frame >= _cfg.frames)
                {
                    Finish();
                }
            }
        }

        void Capture()
        {
            var req = new RenderPipeline.StandardRequest { destination = _rt };
            if (!RenderPipeline.SupportsRenderRequest(_cam, req))
            {
                return;
            }

            RenderPipeline.SubmitRenderRequest(_cam, req);

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            _tex.ReadPixels(new Rect(0, 0, _cfg.width, _cfg.height), 0, 0);
            _tex.Apply(false);
            RenderTexture.active = prev;

            File.WriteAllBytes(Path.Combine(_cfg.outDir, $"frame_{_frame:D5}.png"), _tex.EncodeToPNG());
            _frame++;
        }

        void Finish()
        {
            _done = true;
            File.WriteAllText(Path.Combine(_cfg.outDir, "done.txt"), _frame.ToString());
            if (!string.IsNullOrEmpty(_markerPath) && File.Exists(_markerPath))
            {
                File.Delete(_markerPath); // 다음 일반 플레이엔 자동 플레이가 켜지지 않도록
            }

            Debug.Log($"[PlaytestRecorder] 완료: {_frame} 프레임 → {_cfg.outDir}");
            UnityEditor.EditorApplication.isPlaying = false;
        }

        void OnDestroy()
        {
            if (_rt != null)
            {
                _rt.Release();
            }
        }
    }
}
#endif
