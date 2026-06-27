using System.Collections.Generic;
using Tartisians.Core.Services;
using Tartisians.Data;
using Tartisians.Gameplay.Progression;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tartisians.Gameplay.Weapons
{
    /// <summary>
    /// 플레이어에 부착되어 무기 비주얼을 생성/동기화한다(M8 VFX). 발사 형태별로 다른 시각 언어:
    /// 오라=반투명 디스크, 궤도=회전 발광 오브(트레일), 관통=빔 플래시(LineRenderer).
    /// 투사체 색/트레일은 Projectile 자체가 처리. 색/크기는 WeaponDefinition(Color·VfxScale)에서.
    /// </summary>
    public sealed class WeaponVfx : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] Material _discMat;     // 반투명(알파) — 오라
        [SerializeField] Material _additiveMat; // 가산 발광 — 궤도 오브/빔

        BuildState _build;
        MaterialPropertyBlock _mpb;

        readonly Dictionary<WeaponInstance, Transform> _auras = new();
        readonly Dictionary<WeaponInstance, List<Transform>> _orbits = new();
        readonly List<WeaponInstance> _seen = new();
        readonly List<WeaponInstance> _toRemove = new();
        readonly List<Beam> _beams = new();

        sealed class Beam
        {
            public LineRenderer Lr;
            public float Life;
            public float MaxLife;
            public Color Color;
        }

        void Awake()
        {
            _mpb = new MaterialPropertyBlock();
            ServiceLocator.TryGet(out _build);
        }

        void LateUpdate()
        {
            if (_build == null)
            {
                ServiceLocator.TryGet(out _build);
                if (_build == null)
                {
                    return;
                }
            }

            PassiveModifiers mods = _build.ComputeModifiers();
            Vector3 self = transform.position;

            _seen.Clear();
            List<WeaponInstance> weapons = _build.Weapons;
            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponInstance w = weapons[i];
                EffectiveWeaponStats eff = w.Compute(mods);
                switch (w.Def.FireMode)
                {
                    case WeaponFireMode.AuraField: SyncAura(w, eff, self); _seen.Add(w); break;
                    case WeaponFireMode.Orbital: SyncOrbit(w, eff, self); _seen.Add(w); break;
                }
            }

            Sweep();
            UpdateBeams();
        }

        // ── 오라: 반투명 디스크 ──
        void SyncAura(WeaponInstance w, in EffectiveWeaponStats eff, Vector3 self)
        {
            if (!_auras.TryGetValue(w, out Transform disc) || disc == null)
            {
                disc = MakeDisc(w.Def.Color);
                _auras[w] = disc;
            }

            float d = Mathf.Max(0.3f, eff.Area) * 2f;
            disc.position = new Vector3(self.x, 0.06f, self.z);
            disc.localScale = new Vector3(d, 0.02f, d);
        }

        Transform MakeDisc(Color c)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Vfx_AuraDisc";
            Strip(go);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _discMat;
            c.a = 0.22f;
            ApplyColor(r, c);
            return go.transform;
        }

        // ── 궤도: 회전 발광 오브(+트레일) ──
        void SyncOrbit(WeaponInstance w, in EffectiveWeaponStats eff, Vector3 self)
        {
            if (!_orbits.TryGetValue(w, out List<Transform> orbs))
            {
                orbs = new List<Transform>();
                _orbits[w] = orbs;
            }

            int n = Mathf.Max(1, eff.Amount);
            while (orbs.Count < n)
            {
                orbs.Add(MakeOrb(w.Def.Color, w.Def.VfxScale));
            }

            float radius = Mathf.Max(0.5f, eff.Area);
            float spin = Time.time * (90f * Mathf.Deg2Rad);
            for (int k = 0; k < orbs.Count; k++)
            {
                bool active = k < n;
                if (orbs[k].gameObject.activeSelf != active)
                {
                    orbs[k].gameObject.SetActive(active);
                }

                if (!active)
                {
                    continue;
                }

                float a = spin + k * (Mathf.PI * 2f / n);
                orbs[k].position = self + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius + Vector3.up * 0.6f;
            }
        }

        Transform MakeOrb(Color c, float scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Vfx_Orb";
            Strip(go);
            go.transform.localScale = Vector3.one * (0.45f * scale);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = _additiveMat;
            ApplyColor(r, c);

            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.25f;
            trail.startWidth = 0.35f * scale;
            trail.endWidth = 0f;
            trail.material = _additiveMat;
            trail.numCapVertices = 2;
            Color tail = c; tail.a = 0f;
            trail.startColor = c;
            trail.endColor = tail;
            return go.transform;
        }

        // ── 관통: 빔 플래시(WeaponController가 호출) ──
        public void FlashLance(Vector3 origin, Vector3 dir, float length, Color color)
        {
            if (_additiveMat == null)
            {
                return;
            }

            var go = new GameObject("Vfx_Beam");
            var lr = go.AddComponent<LineRenderer>();
            lr.material = _additiveMat;
            lr.numCapVertices = 3;
            lr.widthMultiplier = 0.5f;
            lr.positionCount = 2;
            Vector3 a = origin + Vector3.up * 0.6f;
            lr.SetPosition(0, a);
            lr.SetPosition(1, a + dir * length);
            ApplyColorLine(lr, color);
            _beams.Add(new Beam { Lr = lr, Life = 0.12f, MaxLife = 0.12f, Color = color });
        }

        void UpdateBeams()
        {
            for (int i = _beams.Count - 1; i >= 0; i--)
            {
                Beam b = _beams[i];
                b.Life -= Time.deltaTime;
                if (b.Life <= 0f || b.Lr == null)
                {
                    if (b.Lr != null) Destroy(b.Lr.gameObject);
                    _beams.RemoveAt(i);
                    continue;
                }

                float f = b.Life / b.MaxLife;
                b.Lr.widthMultiplier = 0.5f * f;
                Color c = b.Color; c.a *= f;
                ApplyColorLine(b.Lr, c);
            }
        }

        // ── 정리: 더 이상 보유하지 않는 무기(진화/제거)의 비주얼 파괴 ──
        void Sweep()
        {
            _toRemove.Clear();
            foreach (KeyValuePair<WeaponInstance, Transform> kv in _auras)
            {
                if (!_seen.Contains(kv.Key)) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                if (_auras[_toRemove[i]] != null) Destroy(_auras[_toRemove[i]].gameObject);
                _auras.Remove(_toRemove[i]);
            }

            _toRemove.Clear();
            foreach (KeyValuePair<WeaponInstance, List<Transform>> kv in _orbits)
            {
                if (!_seen.Contains(kv.Key)) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                List<Transform> orbs = _orbits[_toRemove[i]];
                for (int k = 0; k < orbs.Count; k++)
                {
                    if (orbs[k] != null) Destroy(orbs[k].gameObject);
                }
                _orbits.Remove(_toRemove[i]);
            }
        }

        void ApplyColor(Renderer r, Color c)
        {
            r.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            r.SetPropertyBlock(_mpb);
        }

        void ApplyColorLine(LineRenderer lr, Color c)
        {
            lr.startColor = c;
            lr.endColor = c;
            lr.GetPropertyBlock(_mpb);
            _mpb.SetColor(BaseColorId, c);
            lr.SetPropertyBlock(_mpb);
        }

        static void Strip(GameObject go)
        {
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var r = go.GetComponent<MeshRenderer>();
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
