using System.Collections.Generic;
using Tartisians.Core.Events;
using Tartisians.Gameplay.Events;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tartisians.Gameplay.Vfx
{
    /// <summary>
    /// 적 피격 지점에 가산 발광 임팩트 스파크를 띄운다(손맛). 치명타는 기존 사망 VFX가 담당하므로
    /// 비치명타에만, 프레임당 개수를 제한해 스폰한다. 프리미티브 구체를 자체 풀로 재사용하고
    /// LateUpdate에서 팝+페이드한다. EnemyHitEvent를 구독한다.
    /// </summary>
    public sealed class ImpactVfx : MonoBehaviour
    {
        [SerializeField] Material _additiveMat;
        [SerializeField] int _maxSparks = 48;
        [SerializeField] float _lifetime = 0.16f;
        [SerializeField] float _startScale = 0.25f;
        [SerializeField] float _endScale = 0.95f;
        [SerializeField] int _maxPerFrame = 6;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color SparkColor = new Color(1f, 0.9f, 0.5f);

        sealed class Spark
        {
            public Transform T;
            public MeshRenderer R;
            public float Age;
            public bool InUse;
        }

        EventBinding<EnemyHitEvent> _binding;
        MaterialPropertyBlock _mpb;
        readonly List<Spark> _sparks = new();
        int _spawnedThisFrame;

        void Awake() => _mpb = new MaterialPropertyBlock();

        void OnEnable()
        {
            _binding = new EventBinding<EnemyHitEvent>(OnEnemyHit);
            EventBus<EnemyHitEvent>.Register(_binding);
        }

        void OnDisable()
        {
            if (_binding != null)
            {
                EventBus<EnemyHitEvent>.Deregister(_binding);
            }
        }

        void OnEnemyHit(EnemyHitEvent e)
        {
            if (e.Lethal || _additiveMat == null || _spawnedThisFrame >= _maxPerFrame)
            {
                return;
            }

            Spark s = Acquire();
            if (s == null)
            {
                return;
            }

            _spawnedThisFrame++;
            s.InUse = true;
            s.Age = 0f;
            s.T.position = e.Position + Vector3.up * 0.6f;
            s.T.localScale = Vector3.one * _startScale;
            s.T.gameObject.SetActive(true);
            ApplyColor(s.R, SparkColor);
        }

        Spark Acquire()
        {
            for (int i = 0; i < _sparks.Count; i++)
            {
                if (!_sparks[i].InUse)
                {
                    return _sparks[i];
                }
            }

            if (_sparks.Count < _maxSparks)
            {
                return Create();
            }

            // 포화: 가장 오래된 것 재활용.
            Spark oldest = _sparks[0];
            for (int i = 1; i < _sparks.Count; i++)
            {
                if (_sparks[i].Age > oldest.Age)
                {
                    oldest = _sparks[i];
                }
            }

            return oldest;
        }

        Spark Create()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Vfx_Impact";
            go.transform.SetParent(transform, false);
            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                Destroy(col);
            }

            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sharedMaterial = _additiveMat;
            go.SetActive(false);

            var s = new Spark { T = go.transform, R = r };
            _sparks.Add(s);
            return s;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < _sparks.Count; i++)
            {
                Spark s = _sparks[i];
                if (!s.InUse)
                {
                    continue;
                }

                s.Age += dt;
                float t = _lifetime > 0f ? Mathf.Clamp01(s.Age / _lifetime) : 1f;
                if (t >= 1f)
                {
                    s.InUse = false;
                    s.T.gameObject.SetActive(false);
                    continue;
                }

                s.T.localScale = Vector3.one * Mathf.Lerp(_startScale, _endScale, t);
                float k = 1f - t; // 가산 발광은 RGB 크기로 페이드
                ApplyColor(s.R, new Color(SparkColor.r * k, SparkColor.g * k, SparkColor.b * k, 1f));
            }

            _spawnedThisFrame = 0;
        }

        void ApplyColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }
}
