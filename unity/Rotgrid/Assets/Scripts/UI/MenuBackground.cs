using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// The animated backdrop behind the menus: a fogged corridor with a slow
    /// procession of zombies walking toward camera under a failing work lamp.
    /// </summary>
    public class MenuBackground
    {
        readonly GameObject _root;
        readonly Camera _camera;
        readonly Light _lamp;
        readonly Transform _lampBody;
        readonly Light _backLight;
        readonly List<Zombie> _zombies = new List<Zombie>();
        readonly List<float> _speeds = new List<float>();
        float _t;

        public Camera Camera { get { return _camera; } }

        public MenuBackground()
        {
            _root = new GameObject("MenuBackdrop");

            var camGo = new GameObject("menuCamera");
            camGo.transform.SetParent(_root.transform, false);
            camGo.transform.position = new Vector3(1.6f, 1.75f, -7.5f);
            _camera = camGo.AddComponent<Camera>();
            _camera.fieldOfView = 52f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 120f;
            _camera.clearFlags = CameraClearFlags.Color;
            _camera.backgroundColor = Util.Hex(0x05070a);

            // ---- corridor
            var floorMat = Art.Lit("mb_floor", Color.white, 0.05f, 0f, Art.ConcreteFloor(), 14f);
            var wallMat = Art.Lit("mb_wall", Color.white, 0.04f, 0f, Art.Concrete(), 5f);
            Props.Box(22f, 0.4f, 90f, floorMat, new Vector3(0, -0.2f, 30f), _root.transform);
            Props.Box(22f, 0.4f, 90f, Art.Lit("mb_ceil", Util.Hex(0x14181a), 0f), new Vector3(0, 5.2f, 30f), _root.transform);
            Props.Box(0.6f, 5.6f, 90f, wallMat, new Vector3(-7f, 2.6f, 30f), _root.transform);
            Props.Box(0.6f, 5.6f, 90f, wallMat, new Vector3(7f, 2.6f, 30f), _root.transform);

            var rng = new Util.Rng(4242);
            for (int i = 0; i < 8; i++)
            {
                float z = -2f + i * 9f;
                for (int s = -1; s <= 1; s += 2)
                {
                    Props.Box(0.35f, 3.4f, 0.35f, Props.Rust, new Vector3(s * 6.4f, 1.7f, z), _root.transform);
                    Props.Box(1.6f, 0.16f, 0.5f, Props.DarkMetal, new Vector3(s * 6.0f, 4.4f, z - 2f), _root.transform);
                }
                var prop = i % 2 == 0
                    ? Props.Crate(1.1f, i, _root.transform)
                    : Props.Barrel(i, _root.transform);
                prop.transform.localPosition = new Vector3(rng.Range(-5.5f, 5.5f), 0f, z + rng.Range(-2f, 2f));
                prop.transform.localRotation = Quaternion.Euler(0, rng.Next() * 360f, 0);
            }

            // ---- lighting: one swinging work lamp plus a cold backlight
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Util.Hex(0x2f3c4a);
            RenderSettings.ambientEquatorColor = Util.Hex(0x1a2026);
            RenderSettings.ambientGroundColor = Util.Hex(0x0a0d12);

            var lampGo = new GameObject("workLamp");
            lampGo.transform.SetParent(_root.transform, false);
            lampGo.transform.position = new Vector3(0, 4f, 2f);
            _lamp = lampGo.AddComponent<Light>();
            _lamp.type = LightType.Point;
            _lamp.color = Util.Hex(0xffc078);
            _lamp.range = 40f;
            _lamp.intensity = 4f;
            _lampBody = Props.Box(0.5f, 0.16f, 0.5f, Props.DarkMetal, new Vector3(0, 4.3f, 2f), _root.transform).transform;

            var backGo = new GameObject("backLight");
            backGo.transform.SetParent(_root.transform, false);
            backGo.transform.position = new Vector3(0, 3f, 26f);
            _backLight = backGo.AddComponent<Light>();
            _backLight.type = LightType.Point;
            _backLight.color = Util.Hex(0x66d9ff);
            _backLight.range = 44f;
            _backLight.intensity = 2.4f;

            // ---- the procession
            var holder = new GameObject("menuZombies");
            holder.transform.SetParent(_root.transform, false);
            for (int i = 0; i < 7; i++)
            {
                var z = new Zombie(holder.transform);
                string t = i == 3 ? "heavy" : (i == 5 ? "runner" : "normal");
                z.Spawn(t, new Vector3(Util.Rand(-4.5f, 4.5f), 0f, 12f + i * 8f), 100f, 0.3f, 1f, null);
                z.state = ZState.Chase;
                _zombies.Add(z);
                _speeds.Add(Util.Rand(0.5f, 1f));
            }

            SetActive(true);
        }

        public void SetActive(bool on)
        {
            _root.SetActive(on);
            _camera.enabled = on;
        }

        public void Update(float dt)
        {
            _t += dt;
            float t = _t;

            // Slow drifting camera with a subtle breathing motion.
            _camera.transform.position = new Vector3(
                1.6f + Mathf.Sin(t * 0.14f) * 1.2f,
                1.75f + Mathf.Sin(t * 0.31f) * 0.09f,
                -7.5f + Mathf.Sin(t * 0.09f) * 0.9f);
            _camera.transform.LookAt(new Vector3(Mathf.Sin(t * 0.11f) * 1.2f, 1.55f + Mathf.Sin(t * 0.2f) * 0.1f, 16f));

            // Swinging, failing work lamp.
            float swing = Mathf.Sin(t * 0.9f) * 0.9f;
            var lp = _lamp.transform.position;
            _lamp.transform.position = new Vector3(swing, lp.y, lp.z);
            _lampBody.position = new Vector3(swing, _lampBody.position.y, _lampBody.position.z);
            _lampBody.localRotation = Quaternion.Euler(0, 0, -swing * 7f);
            float flicker = Random.value < 0.02f ? Util.Rand(0.1f, 0.4f) : 1f;
            _lamp.intensity = (4.2f + Mathf.Sin(t * 6.2f) * 0.8f) * flicker;
            _backLight.intensity = 2.2f + Mathf.Sin(t * 1.7f) * 0.6f;

            for (int i = 0; i < _zombies.Count; i++)
            {
                var z = _zombies[i];
                z.pos.z -= _speeds[i] * dt;
                if (z.pos.z < -9f)
                {
                    z.pos.z = 62f + Util.Rand(0f, 8f);
                    z.pos.x = Util.Rand(-4.6f, 4.6f);
                    _speeds[i] = Util.Rand(0.5f, 1.05f);
                }
                z.MenuWalk(dt, t, _speeds[i], 180f);
            }
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
        }
    }
}
