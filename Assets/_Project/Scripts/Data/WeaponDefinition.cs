using System;
using System.Collections.Generic;
using UnityEngine;

namespace Tartisians.Data
{
    /// <summary>
    /// 무기 한 종류의 데이터 정의. 기본 스탯(Lv1) + 발사 형태 + 업그레이드 가능한 특성 목록.
    /// 단일 '레벨'은 없다 — 무기는 자신이 선언한 특성을 각각 독립적으로 강화한다.
    /// 런타임 <c>WeaponInstance</c>가 (기본값 + 특성 Step × 특성 레벨)로 유효 스탯을 계산한다.
    /// </summary>
    [CreateAssetMenu(menuName = "Tartisians/Weapon Definition", fileName = "WeaponDefinition")]
    public sealed class WeaponDefinition : ScriptableObject
    {
        /// <summary>이 무기가 지원하는 업그레이드 특성 1개(종류 + 상한 + 증가폭).</summary>
        [Serializable]
        public struct WeaponTrait
        {
            public TraitKind Kind;
            [Tooltip("이 무기에서 이 특성의 상한(이 횟수까지 강화 가능).")]
            public int MaxLevel;
            [Tooltip("레벨당 증가폭. 종류별 의미: 공격력/탄속/범위=가산, 쿨다운=간격 감소 비율, 다발/관통=정수 가산.")]
            public float Step;
        }

        [Header("식별/표시")]
        [SerializeField] string _id;
        [SerializeField] string _displayName = "Weapon";
        [SerializeField, TextArea] string _description = "";
        [SerializeField] Sprite _icon;
        [SerializeField] WeaponFireMode _fireMode = WeaponFireMode.NearestProjectile;
        [SerializeField] WeaponAimMode _aimMode = WeaponAimMode.Nearest;       // 표적/방향 선택
        [SerializeField] ProjectileMotion _motion = ProjectileMotion.Straight; // 투사체 이동 행동
        [SerializeField] Color _color = Color.white; // VFX 색(투사체/오라/궤도/빔 공통)
        [SerializeField] float _vfxScale = 1f;       // VFX 크기 배율(진화형은 크게)

        [Header("이동 행동 파라미터")]
        [SerializeField] float _homingTurnRate = 360f; // 호밍 조향 속도(도/초)
        [SerializeField] float _ricochetRange = 6f;    // 도탄 시 다음 표적 탐색 반경

        [Header("기본 스탯 (Lv1)")]
        [SerializeField] float _fireInterval = 0.6f;
        [SerializeField] float _damage = 5f;
        [SerializeField] float _projectileSpeed = 14f;
        [SerializeField] int _pierce = 0;
        [SerializeField] float _range = 12f;
        [SerializeField] float _lifetime = 2f;
        [SerializeField] int _amount = 1;   // 동시 투사체/위성/부채꼴 발수
        [SerializeField] float _area = 1f;   // 오라 반경/창 길이/효과 크기 기준

        [Header("업그레이드 특성(무기별 선언)")]
        [SerializeField] WeaponTrait[] _traits;

        public string Id => string.IsNullOrEmpty(_id) ? name : _id;
        public string DisplayName => _displayName;
        public string Description => _description;
        public Sprite Icon => _icon;
        public WeaponFireMode FireMode => _fireMode;
        public WeaponAimMode AimMode => _aimMode;
        public ProjectileMotion Motion => _motion;
        public float HomingTurnRate => Mathf.Max(0f, _homingTurnRate);
        public float RicochetRange => Mathf.Max(0.5f, _ricochetRange);
        public Color Color => _color;
        public float VfxScale => Mathf.Max(0.05f, _vfxScale);

        public float FireInterval => Mathf.Max(0.02f, _fireInterval);
        public float Damage => _damage;
        public float ProjectileSpeed => _projectileSpeed;
        public int Pierce => _pierce;
        public float Range => _range;
        public float Lifetime => _lifetime;
        public int Amount => Mathf.Max(1, _amount);
        public float Area => _area;

        public IReadOnlyList<WeaponTrait> Traits => _traits;

        public bool SupportsTrait(TraitKind kind)
        {
            if (_traits == null)
            {
                return false;
            }

            for (int i = 0; i < _traits.Length; i++)
            {
                if (_traits[i].Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>해당 특성의 상한(미지원이면 0).</summary>
        public int TraitMax(TraitKind kind)
        {
            if (_traits == null)
            {
                return 0;
            }

            for (int i = 0; i < _traits.Length; i++)
            {
                if (_traits[i].Kind == kind)
                {
                    return Mathf.Max(1, _traits[i].MaxLevel);
                }
            }

            return 0;
        }

        /// <summary>해당 특성의 레벨당 증가폭(미지원이면 0).</summary>
        public float TraitStep(TraitKind kind)
        {
            if (_traits == null)
            {
                return 0f;
            }

            for (int i = 0; i < _traits.Length; i++)
            {
                if (_traits[i].Kind == kind)
                {
                    return _traits[i].Step;
                }
            }

            return 0f;
        }
    }
}
