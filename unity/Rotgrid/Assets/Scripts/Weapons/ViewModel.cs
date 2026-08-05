using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// First-person weapon rendering. Lives on a dedicated overlay camera that
    /// draws after the world with a cleared depth buffer, so the gun never clips
    /// into geometry.
    /// </summary>
    public class ViewModel
    {
        // Poses, tuned for the narrower view-model field of view below.
        static readonly Vector3 Hip = new Vector3(0.2f, -0.185f, 0.44f);
        static readonly Vector3 Sprint = new Vector3(0.26f, -0.26f, 0.4f);
        static readonly Vector3 Reload = new Vector3(0.23f, -0.33f, 0.42f);
        static readonly Vector3 Down = new Vector3(0.22f, -0.46f, 0.4f);

        const float VmFov = 65f;
        // Guns read better slightly under life size in a 65-degree view.
        const float VmScale = 0.66f;

        public readonly Camera camera;
        public readonly Transform holder;
        readonly GameObject _root;
        readonly GameObject _knife;
        readonly GameObject _flash;
        readonly Light _flashLight;

        GameObject _model;
        WeaponModelInfo _info;
        Vector3 _adsOffset = new Vector3(0f, -0.085f, 0.3f);
        Vector3 _muzzleLocal;

        float _adsT, _bobT, _meleeT, _swapT, _flashT;
        float _recoilPos, _recoilVel;
        Vector2 _sway;

        public float AdsT { get { return _adsT; } }

        public ViewModel(Camera worldCamera, int layer)
        {
            _root = new GameObject("ViewModel");
            _root.transform.SetParent(worldCamera.transform, false);

            var camGo = new GameObject("vmCamera");
            camGo.transform.SetParent(_root.transform, false);
            camera = camGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Depth;
            camera.fieldOfView = VmFov;
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 6f;
            camera.depth = worldCamera.depth + 1f;
            camera.cullingMask = 1 << layer;

            var holderGo = new GameObject("holder");
            holderGo.transform.SetParent(camGo.transform, false);
            holder = holderGo.transform;

            // Dedicated lighting so the gun reads in an unlit facility.
            AddLight(camGo.transform, new Vector3(-0.6f, 1f, -0.8f), Util.Hex(0xfff2e0), 1.6f);
            AddLight(camGo.transform, new Vector3(0.8f, -0.3f, 0.6f), Util.Hex(0x9ab6d0), 0.7f);

            _knife = Props.KnifeModel(VmScale, holder);
            _knife.SetActive(false);

            _flash = new GameObject("flash");
            _flash.transform.SetParent(holder, false);
            var flashMat = Art.Emissive("em_muzzle", Util.Hex(0xffdca8), 3f, Art.Particle());
            for (int i = 0; i < 2; i++)
            {
                var q = Props.Quad(0.32f, 0.32f, flashMat, Vector3.zero, _flash.transform);
                q.transform.localRotation = Quaternion.Euler(0, 0, i * 90f);
            }
            _flash.SetActive(false);

            var lightGo = new GameObject("muzzleLight");
            lightGo.transform.SetParent(holder, false);
            _flashLight = lightGo.AddComponent<Light>();
            _flashLight.type = LightType.Point;
            _flashLight.color = Util.Hex(0xffd0a0);
            _flashLight.range = 3f;
            _flashLight.intensity = 0f;
            _flashLight.shadows = LightShadows.None;

            SetLayerRecursive(_root, layer);
        }

        void AddLight(Transform parent, Vector3 dir, Color color, float intensity)
        {
            var go = new GameObject("vmLight");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.LookRotation(dir.normalized);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.color = color;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            // Only lights the view-model layer.
            l.cullingMask = parent.gameObject.layer == 0 ? -1 : (1 << parent.gameObject.layer);
        }

        public static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            for (int i = 0; i < go.transform.childCount; i++)
                SetLayerRecursive(go.transform.GetChild(i).gameObject, layer);
        }

        public void SetWeapon(WeaponDef def, int layer)
        {
            if (_model != null) Object.Destroy(_model);
            _model = Props.WeaponModel(def, VmScale, holder);
            _info = _model.GetComponent<WeaponModelInfo>();
            SetLayerRecursive(_model, layer);

            float sh = (_info != null ? _info.sightHeight : 0.09f) * VmScale;
            _adsOffset = new Vector3(0f, -sh - 0.015f, 0.34f);
            _muzzleLocal = (_info != null ? _info.muzzle : new Vector3(0, 0.012f, 0.5f)) * VmScale;
            _flash.transform.localPosition = _muzzleLocal;
            _flashLight.transform.localPosition = _muzzleLocal;
            _swapT = 1f;
        }

        public void ShowKnife(bool on)
        {
            _knife.SetActive(on);
            if (_model != null) _model.SetActive(!on);
        }

        public void FireKick(WeaponDef def)
        {
            float heavy = Mathf.Clamp(def.damage / 200f + def.recoilV / 6f, 0.25f, 1.5f);
            _recoilVel += 6.5f * heavy;
            _flashT = 0.055f;
            _flash.SetActive(true);
            _flash.transform.localRotation = Quaternion.Euler(0, 0, Util.Rand(0f, 180f));
            _flash.transform.localScale = Vector3.one * (0.7f + Random.value * 0.7f + heavy * 0.35f);
            _flashLight.intensity = 14f * heavy;
        }

        public void StartMelee() { _meleeT = 0.42f; }

        public void Update(float dt, bool ads, float moving, bool sprinting, bool reloading,
                           Vector2 look, bool downed, Weapon weapon)
        {
            // ---- sway from look input
            _sway.x = Util.Damp(_sway.x, Mathf.Clamp(-look.x * 2.4f, -0.06f, 0.06f), 9f, dt);
            _sway.y = Util.Damp(_sway.y, Mathf.Clamp(look.y * 2.4f, -0.06f, 0.06f), 9f, dt);

            float adsLambda = weapon != null ? 1f / Mathf.Max(0.06f, weapon.def.adsTime * 0.42f) : 12f;
            _adsT = Util.Damp(_adsT, ads ? 1f : 0f, adsLambda, dt);

            Vector3 target = Hip;
            if (reloading) target = Reload;
            else if (sprinting && !ads) target = Sprint;
            if (downed) target = Down;

            Vector3 basePos = target;
            if (_adsT > 0.001f && !reloading) basePos = Vector3.Lerp(target, _adsOffset, _adsT);

            _bobT += dt * (sprinting ? 12f : 8.5f) * Mathf.Clamp(moving, 0f, 1.4f);
            float bobScale = (1f - _adsT * 0.82f) * Mathf.Clamp01(moving);
            float bx = Mathf.Cos(_bobT) * 0.016f * bobScale;
            float by = Mathf.Abs(Mathf.Sin(_bobT)) * 0.014f * bobScale;

            // ---- recoil spring
            _recoilVel -= _recoilPos * 210f * dt;
            _recoilVel *= Mathf.Exp(-13f * dt);
            _recoilPos += _recoilVel * dt;
            float rec = _recoilPos * (1f - _adsT * 0.35f);

            float meleeX = 0f, meleeRot = 0f, meleeZ = 0f;
            if (_meleeT > 0f)
            {
                _meleeT = Mathf.Max(0f, _meleeT - dt);
                float swing = Mathf.Sin(Mathf.Clamp01(1f - _meleeT / 0.42f) * Mathf.PI);
                meleeX = -swing * 0.22f;
                meleeZ = -swing * 0.2f;
                meleeRot = swing * 1.15f;
            }

            if (_swapT > 0f) _swapT = Mathf.Max(0f, _swapT - dt * 2.6f);
            float swapDip = _swapT * _swapT * 0.22f;

            holder.localPosition = new Vector3(
                basePos.x + bx + _sway.x + meleeX,
                basePos.y + by + _sway.y - swapDip - rec * 0.05f,
                basePos.z + rec * 0.09f + meleeZ);

            float reloadTilt = reloading ? Mathf.Sin(Time.time * 12f) * 0.05f : 0f;
            holder.localRotation = Quaternion.Euler(
                (-rec * 0.5f + _sway.y * 1.6f + reloadTilt - meleeRot * 0.4f) * Mathf.Rad2Deg,
                (-_sway.x * 2.6f + (reloading ? 0.45f : 0f) + meleeRot * 0.5f) * Mathf.Rad2Deg,
                (_sway.x * 1.4f + (sprinting && !ads ? 0.28f : 0f) + (downed ? 0.4f : 0f) + meleeRot * 0.6f) * Mathf.Rad2Deg);

            if (_flashT > 0f)
            {
                _flashT -= dt;
                _flashLight.intensity *= Mathf.Exp(-26f * dt);
                if (_flashT <= 0f)
                {
                    _flash.SetActive(false);
                    _flashLight.intensity = 0f;
                }
            }

            // ---- magazine drops out during a reload
            if (_info != null && _info.magazine != null)
            {
                float p = (reloading && weapon != null) ? weapon.ReloadProgress : 1f;
                float drop = reloading ? (p < 0.5f ? p * 2f : (1f - p) * 2f) : 0f;
                _info.magazine.localPosition = _info.magHome - new Vector3(0, drop * 0.22f, 0);
                _info.magazine.gameObject.SetActive(!(reloading && p > 0.15f && p < 0.55f));
            }

            // ---- energy weapons pulse
            if (_info != null && _info.energy != null)
            {
                float k = 1.6f + Mathf.Sin(Time.time * 6f) * 0.7f;
                foreach (var e in _info.energy)
                    if (e != null) Art.SetEmission(e.material, Util.Hex(0x66d9ff) * k);
            }

            camera.fieldOfView = VmFov - _adsT * 8f;
        }

        /// <summary>World-space muzzle position, for tracers and audio.</summary>
        public Vector3 MuzzleWorld()
        {
            return holder.TransformPoint(_muzzleLocal);
        }
    }
}
