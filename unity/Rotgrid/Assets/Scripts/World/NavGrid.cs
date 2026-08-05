using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    /// <summary>
    /// Uniform grid navigation.
    ///
    /// Rather than running A* per zombie, a Dijkstra "flow field" is flooded
    /// outward from the player a few times a second. Each agent then just reads
    /// the gradient of the cell it stands in, which stays cheap with 40 agents
    /// and still follows corridors properly around obstacles.
    /// </summary>
    public class NavGrid
    {
        const ushort Unreachable = 0xffff;
        static readonly int[] NX = { 1, -1, 0, 0, 1, 1, -1, -1 };
        static readonly int[] NZ = { 0, 0, 1, -1, 1, -1, 1, -1 };

        public readonly float cell;
        public readonly int w, h;
        readonly float _x0, _z0;

        readonly byte[] _walk;
        readonly byte[] _block;
        readonly float[] _height;
        readonly short[] _zone;
        readonly byte[] _active;
        readonly ushort[] _dist;
        readonly sbyte[] _flowX;
        readonly sbyte[] _flowZ;
        readonly float[] _danger;
        List<int>[] _buckets;

        public NavGrid(float x0, float x1, float z0, float z1, float cellSize)
        {
            cell = cellSize;
            _x0 = Mathf.Floor(x0);
            _z0 = Mathf.Floor(z0);
            w = Mathf.CeilToInt((x1 - x0) / cellSize);
            h = Mathf.CeilToInt((z1 - z0) / cellSize);
            int n = w * h;

            _walk = new byte[n];
            _block = new byte[n];
            _height = new float[n];
            _zone = new short[n];
            _active = new byte[n];
            _dist = new ushort[n];
            _flowX = new sbyte[n];
            _flowZ = new sbyte[n];
            _danger = new float[n];
            for (int i = 0; i < n; i++) { _zone[i] = -1; _dist[i] = Unreachable; }
        }

        public int Idx(int cx, int cz) { return cz * w + cx; }
        public bool InBounds(int cx, int cz) { return cx >= 0 && cz >= 0 && cx < w && cz < h; }
        public int CellX(float x) { return Mathf.FloorToInt((x - _x0) / cell); }
        public int CellZ(float z) { return Mathf.FloorToInt((z - _z0) / cell); }
        public float WorldX(int cx) { return _x0 + (cx + 0.5f) * cell; }
        public float WorldZ(int cz) { return _z0 + (cz + 0.5f) * cell; }

        public void AddWalkableRect(float x0, float x1, float z0, float z1, float y, int zoneIndex, ZoneDef slope)
        {
            int cx0 = Mathf.Max(0, CellX(x0));
            int cx1 = Mathf.Min(w - 1, CellX(x1 - 0.0001f));
            int cz0 = Mathf.Max(0, CellZ(z0));
            int cz1 = Mathf.Min(h - 1, CellZ(z1 - 0.0001f));
            for (int cz = cz0; cz <= cz1; cz++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int i = Idx(cx, cz);
                    _walk[i] = 1;
                    _zone[i] = (short)zoneIndex;
                    _height[i] = (slope != null && slope.hasSlope)
                        ? slope.HeightAt(WorldX(cx), WorldZ(cz))
                        : y;
                }
            }
        }

        /// <summary>Adds or removes a dynamic blocker (doors, machines, crates).</summary>
        public void Block(float x0, float x1, float z0, float z1, bool on, float pad)
        {
            int cx0 = Mathf.Max(0, CellX(x0 - pad));
            int cx1 = Mathf.Min(w - 1, CellX(x1 + pad));
            int cz0 = Mathf.Max(0, CellZ(z0 - pad));
            int cz1 = Mathf.Min(h - 1, CellZ(z1 + pad));
            for (int cz = cz0; cz <= cz1; cz++)
            {
                for (int cx = cx0; cx <= cx1; cx++)
                {
                    int i = Idx(cx, cz);
                    if (on) { if (_block[i] < 255) _block[i]++; }
                    else if (_block[i] > 0) _block[i]--;
                }
            }
        }

        public void SetZoneActive(int zoneIndex, bool on)
        {
            for (int i = 0; i < _active.Length; i++)
                if (_zone[i] == zoneIndex) _active[i] = (byte)(on ? 1 : 0);
        }

        public bool Passable(int i) { return _walk[i] == 1 && _block[i] == 0 && _active[i] == 1; }

        public float HeightAt(float x, float z)
        {
            int cx = CellX(x), cz = CellZ(z);
            if (!InBounds(cx, cz)) return 0f;
            int i = Idx(cx, cz);
            return _walk[i] == 1 ? _height[i] : 0f;
        }

        public int NearestPassable(float x, float z, int radius)
        {
            int cx = CellX(x), cz = CellZ(z);
            if (InBounds(cx, cz) && Passable(Idx(cx, cz))) return Idx(cx, cz);
            for (int r = 1; r <= radius; r++)
            {
                for (int dz = -r; dz <= r; dz++)
                {
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        int nx = cx + dx, nz = cz + dz;
                        if (!InBounds(nx, nz)) continue;
                        int i = Idx(nx, nz);
                        if (Passable(i)) return i;
                    }
                }
            }
            return -1;
        }

        /// <summary>Temporary avoidance cost, e.g. around an armed trap.</summary>
        public void SetDanger(RectDef r, float value)
        {
            int cx0 = Mathf.Max(0, CellX(r.x0));
            int cx1 = Mathf.Min(w - 1, CellX(r.x1));
            int cz0 = Mathf.Max(0, CellZ(r.z0));
            int cz1 = Mathf.Min(h - 1, CellZ(r.z1));
            for (int cz = cz0; cz <= cz1; cz++)
                for (int cx = cx0; cx <= cx1; cx++) _danger[Idx(cx, cz)] = value;
        }

        /// <summary>Dial's algorithm flood from the goal cell, then a gradient pass.</summary>
        public void ComputeFlow(int goal)
        {
            const int MaxD = 16384;
            for (int i = 0; i < _dist.Length; i++) _dist[i] = Unreachable;
            if (_buckets == null) _buckets = new List<int>[MaxD];

            if (goal < 0 || goal >= _dist.Length || !Passable(goal))
            {
                for (int i = 0; i < _flowX.Length; i++) { _flowX[i] = 0; _flowZ[i] = 0; }
                return;
            }

            int minB = 0, maxB = 0, live = 0;
            _dist[goal] = 0;
            if (_buckets[0] == null) _buckets[0] = new List<int>();
            _buckets[0].Add(goal);
            live = 1;

            for (int d = minB; d <= maxB && live > 0; d++)
            {
                var arr = _buckets[d];
                if (arr == null || arr.Count == 0) continue;
                for (int k = 0; k < arr.Count; k++)
                {
                    int i = arr[k];
                    live--;
                    if (_dist[i] != d) continue;      // superseded by a shorter path
                    int cx = i % w, cz = i / w;
                    for (int n = 0; n < 8; n++)
                    {
                        int dx = NX[n], dz = NZ[n];
                        int nx = cx + dx, nz = cz + dz;
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                        int ni = Idx(nx, nz);
                        if (!Passable(ni)) continue;
                        // No corner cutting through wall diagonals.
                        if (dx != 0 && dz != 0 &&
                            (!Passable(Idx(cx + dx, cz)) || !Passable(Idx(cx, cz + dz)))) continue;
                        int step = (dx != 0 && dz != 0) ? 6 : 4;
                        int climb = Mathf.RoundToInt(Mathf.Abs(_height[ni] - _height[i]) * 3f);
                        int nd = d + step + climb + Mathf.RoundToInt(_danger[ni] * 4f);
                        if (nd < _dist[ni] && nd < MaxD)
                        {
                            _dist[ni] = (ushort)nd;
                            if (_buckets[nd] == null) _buckets[nd] = new List<int>();
                            _buckets[nd].Add(ni);
                            live++;
                            if (nd > maxB) maxB = nd;
                        }
                    }
                }
                arr.Clear();
            }
            for (int d = 0; d <= maxB && d < MaxD; d++) if (_buckets[d] != null) _buckets[d].Clear();

            // Precompute the descent direction so agents do no searching at all.
            for (int cz = 0; cz < h; cz++)
            {
                for (int cx = 0; cx < w; cx++)
                {
                    int i = Idx(cx, cz);
                    if (_dist[i] == Unreachable) { _flowX[i] = 0; _flowZ[i] = 0; continue; }
                    int best = _dist[i];
                    sbyte bx = 0, bz = 0;
                    for (int n = 0; n < 8; n++)
                    {
                        int nx = cx + NX[n], nz = cz + NZ[n];
                        if (nx < 0 || nz < 0 || nx >= w || nz >= h) continue;
                        int ni = Idx(nx, nz);
                        if (!Passable(ni)) continue;
                        if (_dist[ni] < best) { best = _dist[ni]; bx = (sbyte)NX[n]; bz = (sbyte)NZ[n]; }
                    }
                    _flowX[i] = bx; _flowZ[i] = bz;
                }
            }
        }

        /// <summary>Unit direction toward the goal, or false when unreachable.</summary>
        public bool FlowAt(float x, float z, out Vector3 dir, out bool arrived)
        {
            dir = Vector3.zero;
            arrived = false;
            int cx = CellX(x), cz = CellZ(z);
            if (!InBounds(cx, cz)) return false;
            int i = Idx(cx, cz);
            if (_dist[i] == Unreachable)
            {
                int alt = NearestPassable(x, z, 3);
                if (alt < 0 || _dist[alt] == Unreachable) return false;
                i = alt;
                cx = i % w; cz = i / w;
            }
            int fx = _flowX[i], fz = _flowZ[i];
            if (fx == 0 && fz == 0) { arrived = true; return true; }
            float tx = WorldX(CellX(x) + fx);
            float tz = WorldZ(CellZ(z) + fz);
            float ddx = tx - x, ddz = tz - z;
            float len = Mathf.Sqrt(ddx * ddx + ddz * ddz);
            if (len < 0.0001f) len = 1f;
            dir = new Vector3(ddx / len, 0f, ddz / len);
            return true;
        }

        public int CellIndexAt(float x, float z)
        {
            int cx = CellX(x), cz = CellZ(z);
            return InBounds(cx, cz) ? Idx(cx, cz) : -1;
        }
    }
}
