using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Owns the zombie pool, the spatial hash used for steering separation, and
    /// the cadence of the flow-field refresh.
    /// </summary>
    public class ZombieManager
    {
        const float Cell = 3f;

        readonly List<Zombie> _pool = new List<Zombie>();
        public readonly List<Zombie> alive = new List<Zombie>();
        readonly Dictionary<long, List<Zombie>> _grid = new Dictionary<long, List<Zombie>>();
        readonly WorldBuilder _world;
        readonly Transform _parent;
        float _flowT;

        public ZombieManager(WorldBuilder world)
        {
            _world = world;
            var go = new GameObject("Zombies");
            _parent = go.transform;
        }

        public int Count { get { return alive.Count; } }

        public int LivingCount
        {
            get
            {
                int n = 0;
                foreach (var z in alive) if (z.state != ZState.Dying) n++;
                return n;
            }
        }

        public int MaxConcurrent { get { return GameSettings.Preset.maxZombies; } }

        public Zombie Spawn(string type, Vector3 pos, float health, float aggression, float damageScale, Barricade window)
        {
            Zombie z = null;
            foreach (var p in _pool) if (!p.active) { z = p; break; }
            if (z == null)
            {
                z = new Zombie(_parent);
                _pool.Add(z);
            }
            z.Spawn(type, pos, health, aggression, damageScale, window);
            alive.Add(z);
            return z;
        }

        public void Remove(Zombie z) { alive.Remove(z); }

        public void Clear()
        {
            for (int i = alive.Count - 1; i >= 0; i--) alive[i].Despawn();
            alive.Clear();
        }

        /// <summary>Zombies within `r` of a point (explosions, traps, chaining).</summary>
        public List<Zombie> Near(float x, float z, float r)
        {
            var outList = new List<Zombie>();
            float r2 = r * r;
            foreach (var zz in alive)
            {
                if (zz.state == ZState.Dying) continue;
                float dx = zz.pos.x - x, dz = zz.pos.z - z;
                if (dx * dx + dz * dz <= r2) outList.Add(zz);
            }
            return outList;
        }

        static long Key(int cx, int cz) { return ((long)cx << 32) ^ (uint)cz; }

        void RebuildGrid()
        {
            foreach (var kv in _grid) kv.Value.Clear();
            foreach (var z in alive)
            {
                long k = Key(Mathf.FloorToInt(z.pos.x / Cell), Mathf.FloorToInt(z.pos.z / Cell));
                List<Zombie> list;
                if (!_grid.TryGetValue(k, out list))
                {
                    list = new List<Zombie>();
                    _grid[k] = list;
                }
                list.Add(z);
            }
        }

        public Vector3 Separation(Zombie z)
        {
            float ox = 0f, oz = 0f;
            int cx = Mathf.FloorToInt(z.pos.x / Cell), cz = Mathf.FloorToInt(z.pos.z / Cell);
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    List<Zombie> list;
                    if (!_grid.TryGetValue(Key(cx + i, cz + j), out list)) continue;
                    foreach (var o in list)
                    {
                        if (o == z || o.state == ZState.Dying) continue;
                        float dx = z.pos.x - o.pos.x, dz2 = z.pos.z - o.pos.z;
                        float d2 = dx * dx + dz2 * dz2;
                        float minD = z.radius + o.radius + 0.16f;
                        if (d2 > minD * minD || d2 < 1e-5f) continue;
                        float d = Mathf.Sqrt(d2);
                        float push = (minD - d) / minD;
                        ox += (dx / d) * push * 1.5f;
                        oz += (dz2 / d) * push * 1.5f;
                    }
                }
            }
            return new Vector3(ox, 0f, oz);
        }

        public void Update(float dt, ZombieContext ctx)
        {
            RebuildGrid();

            // Refresh the flow field a few times a second from the player's cell.
            _flowT -= dt;
            if (_flowT <= 0f)
            {
                _flowT = 0.28f;
                int goal = _world.nav.NearestPassable(ctx.playerPos.x, ctx.playerPos.z, 6);
                if (goal >= 0) _world.nav.ComputeFlow(goal);
            }

            ctx.world = _world;
            ctx.nav = _world.nav;
            ctx.SeparationFn = Separation;

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (i < alive.Count) alive[i].Tick(dt, ctx);
            }
        }
    }
}
