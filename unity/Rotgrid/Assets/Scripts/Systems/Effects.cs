using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// All the transient visual noise: blood, sparks, wood splinters, tracers,
    /// explosions and floor decals. Uses a single pooled mesh of camera-facing
    /// quads rather than a ParticleSystem so behaviour matches the browser build
    /// exactly and nothing needs authoring in the editor.
    /// </summary>
    public class Effects
    {
        const int MaxParticles = 1400;

        struct P
        {
            public Vector3 pos, vel;
            public Color col;
            public float life, maxLife, size, gravity, drag;
            public bool additive;
        }

        readonly P[] _p = new P[MaxParticles];
        int _count;

        readonly GameObject _root;
        readonly Mesh _mesh;
        readonly Mesh _addMesh;
        readonly Vector3[] _verts;
        readonly Vector2[] _uvs;
        readonly int[] _tris;
        readonly Color[] _cols;
        readonly Vector3[] _averts;
        readonly Vector2[] _auvs;
        readonly int[] _atris;
        readonly Color[] _acols;

        readonly float _scale;
        Transform _camera;

        // Tracers
        class Tracer { public LineRenderer line; public float t; }
        readonly List<Tracer> _tracers = new List<Tracer>();

        // Decals
        class Decal { public GameObject go; public float t; public Material mat; }
        readonly List<Decal> _decals = new List<Decal>();
        int _decalIndex;

        // Explosion flashes
        class Flash { public Light light; public float t, max; }
        readonly List<Flash> _flashes = new List<Flash>();

        public Effects(Transform cameraTransform)
        {
            _camera = cameraTransform;
            _scale = GameSettings.Preset.particles;
            _root = new GameObject("Effects");

            _verts = new Vector3[MaxParticles * 4];
            _uvs = new Vector2[MaxParticles * 4];
            _tris = new int[MaxParticles * 6];
            _cols = new Color[MaxParticles * 4];
            _averts = new Vector3[MaxParticles * 4];
            _auvs = new Vector2[MaxParticles * 4];
            _atris = new int[MaxParticles * 6];
            _acols = new Color[MaxParticles * 4];
            for (int i = 0; i < MaxParticles; i++)
            {
                int v = i * 4, t = i * 6;
                _uvs[v] = new Vector2(0, 0); _uvs[v + 1] = new Vector2(1, 0);
                _uvs[v + 2] = new Vector2(1, 1); _uvs[v + 3] = new Vector2(0, 1);
                _auvs[v] = _uvs[v]; _auvs[v + 1] = _uvs[v + 1]; _auvs[v + 2] = _uvs[v + 2]; _auvs[v + 3] = _uvs[v + 3];
                _tris[t] = v; _tris[t + 1] = v + 2; _tris[t + 2] = v + 1;
                _tris[t + 3] = v; _tris[t + 4] = v + 3; _tris[t + 5] = v + 2;
                _atris[t] = _tris[t]; _atris[t + 1] = _tris[t + 1]; _atris[t + 2] = _tris[t + 2];
                _atris[t + 3] = _tris[t + 3]; _atris[t + 4] = _tris[t + 4]; _atris[t + 5] = _tris[t + 5];
            }

            _mesh = NewMeshObject("particles", false, _verts, _uvs, _tris, _cols);
            _addMesh = NewMeshObject("particlesAdd", true, _averts, _auvs, _atris, _acols);

            for (int i = 0; i < 20; i++)
            {
                var go = new GameObject("tracer");
                go.transform.SetParent(_root.transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.widthMultiplier = 0.03f;
                lr.material = ParticleMaterial(true);
                lr.useWorldSpace = true;
                go.SetActive(false);
                _tracers.Add(new Tracer { line = lr });
            }

            for (int i = 0; i < 32; i++)
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                var col = q.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
                q.transform.SetParent(_root.transform, false);
                q.transform.rotation = Quaternion.Euler(90f, 0, 0);
                var m = new Material(Art.UnlitShader);
                m.mainTexture = Art.Particle();
                Art.SetColor(m, new Color(0.29f, 0.04f, 0.03f, 0f));
                MakeTransparent(m);
                q.GetComponent<MeshRenderer>().material = m;
                q.SetActive(false);
                _decals.Add(new Decal { go = q, mat = m });
            }

            for (int i = 0; i < 5; i++)
            {
                var go = new GameObject("flash");
                go.transform.SetParent(_root.transform, false);
                var l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 22f;
                l.intensity = 0f;
                l.shadows = LightShadows.None;
                go.SetActive(false);
                _flashes.Add(new Flash { light = l });
            }
        }

        Mesh NewMeshObject(string name, bool additive, Vector3[] v, Vector2[] uv, int[] tri, Color[] col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.material = ParticleMaterial(additive);
            var mesh = new Mesh();
            mesh.name = name;
            mesh.vertices = v;
            mesh.uv = uv;
            mesh.triangles = tri;
            mesh.colors = col;
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 500f);
            mf.mesh = mesh;
            return mesh;
        }

        static Material ParticleMaterial(bool additive)
        {
            var m = new Material(Art.UnlitShader);
            m.mainTexture = Art.Particle();
            MakeTransparent(m);
            if (additive)
            {
                // 1 = SrcAlpha, 1 = One  (additive blend)
                if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);
                if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 1f);
                m.renderQueue = 3100;
            }
            return m;
        }

        static void MakeTransparent(Material m)
        {
            if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f);   // URP transparent
            if (m.HasProperty("_Mode")) m.SetFloat("_Mode", 3f);         // built-in transparent
            if (m.HasProperty("_SrcBlend")) m.SetFloat("_SrcBlend", 5f);
            if (m.HasProperty("_DstBlend")) m.SetFloat("_DstBlend", 10f);
            if (m.HasProperty("_ZWrite")) m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }

        public void SetCamera(Transform t) { _camera = t; }

        // ------------------------------------------------------------ emitters
        void Emit(Vector3 pos, Vector3 vel, Color col, float life, float size, float gravity, float drag, bool additive)
        {
            int i;
            if (_count < MaxParticles) i = _count++;
            else i = Random.Range(0, MaxParticles);   // recycle rather than drop the effect
            _p[i].pos = pos;
            _p[i].vel = vel;
            _p[i].col = col;
            _p[i].life = life;
            _p[i].maxLife = life;
            _p[i].size = size;
            _p[i].gravity = gravity;
            _p[i].drag = drag;
            _p[i].additive = additive;
        }

        static Vector3 Rv(float a, float b) { return new Vector3(Util.Rand(a, b), Util.Rand(a, b), Util.Rand(a, b)); }

        public void BloodBurst(Vector3 p, Vector3 dir, float scale)
        {
            int n = Mathf.RoundToInt(10 * scale * _scale) + 3;
            for (int i = 0; i < n; i++)
            {
                var v = new Vector3(Util.Rand(-2.2f, 2.2f) + dir.x * 2.4f,
                                    Util.Rand(0.6f, 3.6f) * scale,
                                    Util.Rand(-2.2f, 2.2f) + dir.z * 2.4f);
                Emit(p, v, i % 5 == 0 ? Util.Hex(0x8c1410) : Util.Hex(0x5e0d0a),
                     Util.Rand(0.35f, 0.85f), Util.Rand(0.04f, 0.12f) * scale, 11f, 1.1f, false);
            }
            int mist = Mathf.RoundToInt(4 * scale * _scale);
            for (int i = 0; i < mist; i++)
                Emit(p, new Vector3(Util.Rand(-1, 1), Util.Rand(0.3f, 1.4f), Util.Rand(-1, 1)),
                     new Color(0.23f, 0.03f, 0.02f, 0.55f), Util.Rand(0.5f, 1f), Util.Rand(0.16f, 0.32f), 1.2f, 2.6f, false);
        }

        public void BoneBurst(Vector3 p)
        {
            int n = Mathf.RoundToInt(5 * _scale) + 2;
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-2.4f, 2.4f), Util.Rand(1.5f, 4.2f), Util.Rand(-2.4f, 2.4f)),
                     Util.Hex(0xd8d2bc), Util.Rand(0.6f, 1.2f), Util.Rand(0.03f, 0.07f), 13f, 0.8f, false);
        }

        public void WoodBurst(Vector3 p)
        {
            int n = Mathf.RoundToInt(12 * _scale) + 4;
            int[] cols = { 0x8a6a3c, 0x6a4f2a, 0xa9884f };
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-2.6f, 2.6f), Util.Rand(0.5f, 3.4f), Util.Rand(-2.6f, 2.6f)),
                     Util.Hex(cols[i % 3]), Util.Rand(0.5f, 1.1f), Util.Rand(0.03f, 0.1f), 12f, 1f, false);
        }

        public void SparkBurst(Vector3 p, Color col, int count)
        {
            int n = Mathf.RoundToInt(count * _scale);
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-3.5f, 3.5f), Util.Rand(0.4f, 4f), Util.Rand(-3.5f, 3.5f)),
                     col, Util.Rand(0.18f, 0.5f), Util.Rand(0.02f, 0.06f), 14f, 1.6f, true);
        }

        public void EmberBurst(Vector3 p)
        {
            int n = Mathf.RoundToInt(3 * _scale) + 1;
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-0.5f, 0.5f), Util.Rand(0.6f, 1.8f), Util.Rand(-0.5f, 0.5f)),
                     i % 2 == 0 ? Util.Hex(0xff7b2e) : Util.Hex(0xffc04a),
                     Util.Rand(0.4f, 0.9f), Util.Rand(0.05f, 0.12f), -1.4f, 1.2f, true);
        }

        public void ImpactPuff(Vector3 p, Vector3 normal)
        {
            int n = Mathf.RoundToInt(6 * _scale) + 2;
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-1.4f, 1.4f) + normal.x * 1.6f, Util.Rand(0.2f, 1.6f),
                                    Util.Rand(-1.4f, 1.4f) + normal.z * 1.6f),
                     new Color(0.54f, 0.56f, 0.53f, 0.7f), Util.Rand(0.25f, 0.6f), Util.Rand(0.05f, 0.16f), 3.4f, 2.4f, false);
            SparkBurst(p, Util.Hex(0xffd9a0), 4);
        }

        public void Smoke(Vector3 p, int count)
        {
            int n = Mathf.RoundToInt(count * _scale);
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-1, 1), Util.Rand(0.8f, 2.4f), Util.Rand(-1, 1)),
                     new Color(0.16f, 0.18f, 0.19f, 0.45f), Util.Rand(1f, 2.2f), Util.Rand(0.4f, 1.1f), -0.6f, 1.8f, false);
        }

        public void Explosion(Vector3 p, float radius, Color col)
        {
            SparkBurst(p, col, 34);
            Smoke(p, 14);
            int n = Mathf.RoundToInt(18 * _scale);
            for (int i = 0; i < n; i++)
                Emit(p, new Vector3(Util.Rand(-1, 1) * radius * 2f, Util.Rand(0, 1) * radius * 1.6f,
                                    Util.Rand(-1, 1) * radius * 2f),
                     col, Util.Rand(0.2f, 0.6f), Util.Rand(0.2f, 0.6f), 2f, 3.2f, true);

            foreach (var f in _flashes)
            {
                if (f.light.gameObject.activeSelf) continue;
                f.light.transform.position = p;
                f.light.color = col;
                f.light.range = radius * 5f;
                f.max = 400f;
                f.light.intensity = f.max;
                f.t = 0.4f;
                f.light.gameObject.SetActive(true);
                break;
            }
        }

        /// <summary>An electric arc between two points (wonder weapon + trap).</summary>
        public void Arc(Vector3 from, Vector3 to, Color col)
        {
            const int steps = 7;
            for (int i = 0; i <= steps; i++)
            {
                float k = (float)i / steps;
                var p = Vector3.Lerp(from, to, k) + Rv(-0.28f, 0.28f);
                Emit(p, Rv(-0.4f, 0.4f), col, Util.Rand(0.08f, 0.2f), Util.Rand(0.08f, 0.2f), 0f, 4f, true);
            }
        }

        public void BloodPool(Vector3 p, float groundY)
        {
            var d = _decals[_decalIndex];
            _decalIndex = (_decalIndex + 1) % _decals.Count;
            d.go.transform.position = new Vector3(p.x, groundY + 0.03f, p.z);
            d.go.transform.rotation = Quaternion.Euler(90f, Util.Rand(0, 360f), 0);
            d.go.transform.localScale = Vector3.one * Util.Rand(1f, 2f);
            Art.SetColor(d.mat, new Color(0.29f, 0.04f, 0.03f, 0.72f));
            d.go.SetActive(true);
            d.t = 26f;
        }

        public void Tracer(Vector3 from, Vector3 to, Color col)
        {
            foreach (var t in _tracers)
            {
                if (t.line.gameObject.activeSelf) continue;
                t.line.SetPosition(0, from);
                t.line.SetPosition(1, to);
                Art.SetColor(t.line.material, col);
                t.line.gameObject.SetActive(true);
                t.t = 0.06f;
                return;
            }
        }

        // ------------------------------------------------------------ update
        public void Update(float dt)
        {
            int n = _count;
            for (int i = 0; i < n; i++)
            {
                _p[i].life -= dt;
                if (_p[i].life <= 0f)
                {
                    _p[i] = _p[n - 1];
                    n--; i--;
                    continue;
                }
                float d = Mathf.Exp(-_p[i].drag * dt);
                _p[i].vel = new Vector3(_p[i].vel.x * d, _p[i].vel.y * d - _p[i].gravity * dt, _p[i].vel.z * d);
                _p[i].pos += _p[i].vel * dt;
            }
            _count = n;
            BuildMeshes();

            foreach (var t in _tracers)
            {
                if (!t.line.gameObject.activeSelf) continue;
                t.t -= dt;
                if (t.t <= 0f) t.line.gameObject.SetActive(false);
            }
            foreach (var d in _decals)
            {
                if (!d.go.activeSelf) continue;
                d.t -= dt;
                if (d.t < 4f)
                {
                    var c = new Color(0.29f, 0.04f, 0.03f, Mathf.Max(0f, d.t / 4f) * 0.72f);
                    Art.SetColor(d.mat, c);
                }
                if (d.t <= 0f) d.go.SetActive(false);
            }
            foreach (var f in _flashes)
            {
                if (!f.light.gameObject.activeSelf) continue;
                f.t -= dt;
                f.light.intensity = Mathf.Max(0f, f.t / 0.4f) * f.max;
                if (f.t <= 0f) f.light.gameObject.SetActive(false);
            }
        }

        void BuildMeshes()
        {
            Vector3 right = Vector3.right, up = Vector3.up;
            if (_camera != null) { right = _camera.right; up = _camera.up; }

            int solid = 0, add = 0;
            for (int i = 0; i < _count; i++)
            {
                float a = Mathf.Clamp01(_p[i].life / _p[i].maxLife);
                var c = _p[i].col;
                c.a *= a;
                float s = _p[i].size * 0.5f;
                Vector3 r = right * s, u = up * s;
                Vector3 p = _p[i].pos;

                if (_p[i].additive)
                {
                    if (add >= MaxParticles) continue;
                    int v = add * 4;
                    _averts[v] = p - r - u; _averts[v + 1] = p + r - u;
                    _averts[v + 2] = p + r + u; _averts[v + 3] = p - r + u;
                    _acols[v] = c; _acols[v + 1] = c; _acols[v + 2] = c; _acols[v + 3] = c;
                    add++;
                }
                else
                {
                    if (solid >= MaxParticles) continue;
                    int v = solid * 4;
                    _verts[v] = p - r - u; _verts[v + 1] = p + r - u;
                    _verts[v + 2] = p + r + u; _verts[v + 3] = p - r + u;
                    _cols[v] = c; _cols[v + 1] = c; _cols[v + 2] = c; _cols[v + 3] = c;
                    solid++;
                }
            }
            // Collapse the unused tail so it draws nothing.
            for (int i = solid; i < MaxParticles; i++)
            {
                int v = i * 4;
                _verts[v] = _verts[v + 1] = _verts[v + 2] = _verts[v + 3] = Vector3.zero;
            }
            for (int i = add; i < MaxParticles; i++)
            {
                int v = i * 4;
                _averts[v] = _averts[v + 1] = _averts[v + 2] = _averts[v + 3] = Vector3.zero;
            }

            _mesh.vertices = _verts;
            _mesh.colors = _cols;
            _addMesh.vertices = _averts;
            _addMesh.colors = _acols;
        }

        public void Reset()
        {
            _count = 0;
            BuildMeshes();
            foreach (var t in _tracers) t.line.gameObject.SetActive(false);
            foreach (var d in _decals) d.go.SetActive(false);
            foreach (var f in _flashes) f.light.gameObject.SetActive(false);
        }

        public void Dispose()
        {
            if (_root != null) Object.Destroy(_root);
        }
    }
}
