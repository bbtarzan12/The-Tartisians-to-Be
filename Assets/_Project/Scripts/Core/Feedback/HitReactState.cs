using UnityEngine;

namespace Tartisians.Core.Feedback
{
    /// <summary>
    /// 피격 반응(흰색 플래시 + 스케일 펀치)의 순수 상태. MonoBehaviour와 분리해 단위 테스트한다.
    /// Trigger로 타이머를 채우고 Tick(dt)으로 감쇠시킨다. 적 등에 값 필드로 두고 제자리 변경한다.
    /// </summary>
    public struct HitReactState
    {
        float _flash;
        float _flashDuration;
        float _punch;
        float _punchDuration;

        /// <summary>플래시 또는 펀치가 진행 중인가(끝나면 시각 복원만 한 번 하면 된다).</summary>
        public bool IsActive => _flash > 0f || _punch > 0f;

        /// <summary>0(영향 없음)~1(피격 직후)로 감쇠하는 플래시 양.</summary>
        public float FlashAmount => _flashDuration > 0f ? Mathf.Clamp01(_flash / _flashDuration) : 0f;

        public void Trigger(float flashDuration, float punchDuration)
        {
            _flashDuration = Mathf.Max(0.0001f, flashDuration);
            _punchDuration = Mathf.Max(0.0001f, punchDuration);
            _flash = _flashDuration;
            _punch = _punchDuration;
        }

        public void Tick(float dt)
        {
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - dt);
            }

            if (_punch > 0f)
            {
                _punch = Mathf.Max(0f, _punch - dt);
            }
        }

        /// <summary>스케일 배수 = 1 + strength·sin(π·진행도). 시작·끝은 1, 중간이 최대(부드러운 팝).</summary>
        public float ScaleMultiplier(float strength)
        {
            if (_punch <= 0f || _punchDuration <= 0f)
            {
                return 1f;
            }

            float progress = 1f - (_punch / _punchDuration); // 0 → 1
            return 1f + strength * Mathf.Sin(progress * Mathf.PI);
        }

        public void Reset()
        {
            _flash = 0f;
            _punch = 0f;
        }
    }
}
