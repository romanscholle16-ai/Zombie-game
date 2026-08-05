using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// A purchasable blast door. Buying it permanently opens the zones behind
    /// it, which expands both the playable area and the spawn pool.
    /// </summary>
    public class DoorObj
    {
        public readonly DoorDef def;
        public readonly string id, name;
        public readonly int cost;
        public readonly bool requiresPower;
        public readonly float cx, cz, y, width, height;
        public bool open;

        float _t;
        readonly Transform[] _leaves = new Transform[2];
        readonly float[] _home = new float[2];
        readonly MeshRenderer _lamp;
        public readonly GameObject root;
        public Aabb collider;

        public DoorObj(DoorDef d, float floorY, float zoneHeight, Transform parent)
        {
            def = d;
            id = d.id;
            name = d.name;
            cost = d.cost;
            requiresPower = d.requiresPower;
            cx = (d.x0 + d.x1) * 0.5f;
            cz = (d.z0 + d.z1) * 0.5f;
            y = floorY;
            bool alongX = (d.x1 - d.x0) > (d.z1 - d.z0);
            width = Mathf.Max(d.x1 - d.x0, d.z1 - d.z0);
            height = Mathf.Min(zoneHeight, 4.2f);

            collider = new Aabb(
                d.x0 - (alongX ? 0f : 0.05f), d.x1 + (alongX ? 0f : 0.05f),
                d.z0 - (alongX ? 0.05f : 0f), d.z1 + (alongX ? 0.05f : 0f),
                floorY, floorY + height, false);

            root = new GameObject("door_" + d.id);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(cx, floorY, cz);
            root.transform.rotation = Quaternion.Euler(0, alongX ? 0f : 90f, 0);

            float leafW = width * 0.5f;
            for (int s = 0; s < 2; s++)
            {
                float dir = s == 0 ? -1f : 1f;
                var leaf = new GameObject("leaf" + s);
                leaf.transform.SetParent(root.transform, false);
                Props.Box(leafW, height, 0.35f, Props.Metal, new Vector3(0, height * 0.5f, 0), leaf.transform);
                for (int i = 0; i < 3; i++)
                    Props.Box(leafW * 0.82f, 0.12f, 0.42f, Props.DarkMetal,
                        new Vector3(0, height * (0.25f + i * 0.25f), 0), leaf.transform);
                Props.Box(0.14f, height, 0.45f, Props.HazardMat,
                    new Vector3(dir * (leafW * 0.5f - 0.1f), height * 0.5f, 0), leaf.transform);
                leaf.transform.localPosition = new Vector3(dir * leafW * 0.5f, 0, 0);
                _leaves[s] = leaf.transform;
                _home[s] = dir * leafW * 0.5f;
            }

            var signTex = requiresPower
                ? PixelFont.Sign("SEALED", "REQUIRES POWER", Util.Hex(0xe8a33d), Util.Hex(0x0d0b08), 256, 96)
                : PixelFont.Sign(cost.ToString(), "HOLD F TO OPEN", Util.Hex(0x9fd93a), Util.Hex(0x0a0d08), 256, 96);
            for (int s = 0; s < 2; s++)
            {
                var q = Props.Quad(1.7f, 0.85f, Art.Emissive("sign_" + d.id, Color.white, 1.2f, signTex),
                    new Vector3(0, height * 0.62f, s == 0 ? -0.2f : 0.2f), root.transform);
                q.transform.localRotation = Quaternion.Euler(0, s == 0 ? 0f : 180f, 0);
            }

            var lampGo = Props.Box(0.16f, 0.16f, 0.5f, Art.Emissive("em_doorlamp", Util.Hex(0xff2b1e), 2f),
                new Vector3(0, height - 0.35f, 0), root.transform);
            _lamp = lampGo.GetComponent<MeshRenderer>();
        }

        public void Purchase()
        {
            if (open) return;
            open = true;
            Game.Audio.PlayAt(Sfx.Door, new Vector3(cx, y + 1.5f, cz), 1.1f);
            if (_lamp != null) Art.SetEmission(_lamp.material, Util.Hex(0x9fd93a) * 1.6f);
        }

        public void Update(float dt)
        {
            float target = open ? 1f : 0f;
            if (Mathf.Abs(_t - target) < 0.001f) return;
            _t = Mathf.Clamp01(_t + (target > _t ? dt / 1.6f : -dt / 1.6f));
            float e = _t * _t * (3f - 2f * _t);
            for (int s = 0; s < 2; s++)
            {
                float dir = s == 0 ? -1f : 1f;
                _leaves[s].localPosition = new Vector3(_home[s] + dir * e * (width * 0.5f + 0.15f), -e * 0.02f, 0);
            }
            if (_t >= 1f) root.SetActive(false);
        }
    }
}
