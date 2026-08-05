using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// A boarded window. Zombies queue outside, tear planks off one at a time,
    /// then vault the sill. Players can renail planks for points.
    /// </summary>
    public class Barricade
    {
        public const int BoardsPerWindow = 6;

        public readonly string id;
        public readonly string zone;
        public readonly float x, y, z;
        public readonly float nx, nz;
        public readonly Vector3 outside;   // where attackers stage
        public readonly Vector3 inside;    // where a vault lands
        public readonly Vector3 alcoveCenter;
        public readonly HashSet<object> attackers = new HashSet<object>();

        readonly GameObject[] _boards = new GameObject[BoardsPerWindow];
        readonly bool[] _intact = new bool[BoardsPerWindow];
        readonly float[] _hp = new float[BoardsPerWindow];
        readonly Vector3[] _home = new Vector3[BoardsPerWindow];
        public readonly GameObject root;

        public Barricade(WindowDef def, float floorY, Transform parent)
        {
            id = def.id;
            zone = def.zone;
            x = def.x; z = def.z; y = floorY;
            nx = def.nx; nz = def.nz;
            outside = new Vector3(def.x + def.nx * 2.0f, floorY, def.z + def.nz * 2.0f);
            inside = new Vector3(def.x - def.nx * 1.6f, floorY, def.z - def.nz * 1.6f);
            alcoveCenter = new Vector3((def.ax0 + def.ax1) * 0.5f, floorY, (def.az0 + def.az1) * 0.5f);

            root = new GameObject("window_" + def.id);
            root.transform.SetParent(parent, false);
            root.transform.position = new Vector3(def.x, floorY, def.z);
            root.transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(def.nx, def.nz) * Mathf.Rad2Deg, 0f);

            BuildFrame();
            for (int i = 0; i < BoardsPerWindow; i++) MakeBoard(i);
        }

        public int BoardCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < BoardsPerWindow; i++) if (_intact[i]) n++;
                return n;
            }
        }

        public bool IsOpen { get { return BoardCount == 0; } }

        void BuildFrame()
        {
            var m = Props.Concrete;
            Props.Box(3.2f, 1.05f, 0.5f, m, new Vector3(0, 0.52f, 0), root.transform);
            Props.Box(3.2f, 0.9f, 0.5f, m, new Vector3(0, 3.05f, 0), root.transform);
            Props.Box(0.4f, 1.6f, 0.5f, m, new Vector3(-1.6f, 1.85f, 0), root.transform);
            Props.Box(0.4f, 1.6f, 0.5f, m, new Vector3(1.6f, 1.85f, 0), root.transform);
            // Backing plate so the alcove reads as a void.
            Props.Box(3.0f, 1.5f, 0.05f, Art.Lit("m_void", Util.Hex(0x04060a), 0f),
                new Vector3(0, 1.85f, 0.28f), root.transform);
        }

        void MakeBoard(int i)
        {
            float by = 1.28f + (i % 3) * 0.5f + (i > 2 ? 0.06f : 0f);
            float tilt = ((i * 37) % 13 - 6) * 1.26f;      // degrees
            float off = ((i * 53) % 11 - 5) * 0.045f;
            float depth = i > 2 ? -0.16f : 0.16f;

            var plank = new GameObject("plank" + i);
            plank.transform.SetParent(root.transform, false);
            plank.transform.localPosition = new Vector3(off, by, depth);
            plank.transform.localRotation = Quaternion.Euler(0, 0, tilt);
            Props.Box(2.7f, 0.19f, 0.09f, Props.Plank, Vector3.zero, plank.transform);
            Props.Box(0.03f, 0.03f, 0.13f, Props.Metal, new Vector3(-1.19f, 0, 0), plank.transform);
            Props.Box(0.03f, 0.03f, 0.13f, Props.Metal, new Vector3(1.19f, 0, 0), plank.transform);

            _boards[i] = plank;
            _home[i] = plank.transform.localPosition;
            _intact[i] = true;
            _hp[i] = 1f;
        }

        /// <summary>A zombie tears at the boards. Returns true when one comes off.</summary>
        public bool Damage(float amount, Effects fx)
        {
            int last = -1;
            for (int i = 0; i < BoardsPerWindow; i++) if (_intact[i]) last = i;
            if (last < 0) return false;

            _hp[last] -= amount;
            // Shake the plank while it is being worked on.
            var t = _boards[last].transform;
            t.localPosition = _home[last] + new Vector3(Util.Rand(-0.02f, 0.02f), 0, 0);

            if (_hp[last] <= 0f)
            {
                _intact[last] = false;
                _boards[last].SetActive(false);
                if (fx != null) fx.WoodBurst(t.position);
                Game.Audio.PlayAt(Sfx.Board, new Vector3(x, y + 1.5f, z), 1f);
                return true;
            }
            return false;
        }

        /// <summary>Player renails one plank. Returns the points awarded.</summary>
        public int Repair(Effects fx)
        {
            for (int i = 0; i < BoardsPerWindow; i++)
            {
                if (_intact[i]) continue;
                _intact[i] = true;
                _hp[i] = 1f;
                _boards[i].SetActive(true);
                _boards[i].transform.localPosition = _home[i];
                Game.Audio.PlayAt(Sfx.Repair, new Vector3(x, y + 1.5f, z), 1f);
                if (fx != null) fx.SparkBurst(_boards[i].transform.position, Util.Hex(0xd9b46a), 6);
                return 10;
            }
            return 0;
        }

        public void RestoreAll()
        {
            for (int i = 0; i < BoardsPerWindow; i++)
            {
                if (_intact[i]) continue;
                _intact[i] = true;
                _hp[i] = 1f;
                _boards[i].SetActive(true);
                _boards[i].transform.localPosition = _home[i];
            }
        }

        /// <summary>True when a point is on the interior side of the wall.</summary>
        public bool IsInside(float px, float pz)
        {
            return (px - x) * nx + (pz - z) * nz < 0f;
        }

        public float DistanceTo(float px, float pz)
        {
            float dx = px - x, dz = pz - z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }
    }
}
