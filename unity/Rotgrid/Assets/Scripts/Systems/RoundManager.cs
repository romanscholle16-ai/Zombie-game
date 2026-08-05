using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum RoundPhase { Intro, Spawning, Clearing, Intermission, Over }

    public static class Points
    {
        public const int Hit = 10;
        public const int Kill = 60;
        public const int HeadshotBonus = 40;   // headshot kill = Kill + this
        public const int MeleeKill = 130;
        public const int TrapKill = 50;
        public const int Repair = 10;
        public const int RoundBase = 50;
        public const int RoundPerLevel = 10;
    }

    /// <summary>Drives the wave loop: how many, how tough, how fast, what kind.</summary>
    public class RoundManager
    {
        static readonly int[] EarlyCounts = { 6, 8, 13, 18, 24, 27, 28, 28, 29, 33 };

        readonly WorldBuilder _world;
        readonly ZombieManager _zombies;

        public int round;
        public RoundPhase phase = RoundPhase.Intro;
        public string special;
        public float timer;
        public int toSpawn, spawnedThisRound, killsThisRound;
        float _spawnT;
        int _bossesToSpawn;

        public float intermission = 9f;
        public float firstRoundDelay = 5f;

        public System.Action<int, string> OnRoundStart = delegate { };
        public System.Action<int> OnRoundEnd = delegate { };

        public RoundManager(WorldBuilder world, ZombieManager zombies)
        {
            _world = world;
            _zombies = zombies;
        }

        // ------------------------------------------------------------- curves
        public static int CountForRound(int r)
        {
            if (r <= EarlyCounts.Length) return EarlyCounts[r - 1];
            return Mathf.Min(140, Mathf.RoundToInt(33 + (r - 10) * 2.3f));
        }

        public static float HealthForRound(int r)
        {
            if (r <= 9) return 50f + 100f * r;
            return Mathf.Round(950f * Mathf.Pow(1.1f, r - 9));
        }

        public static float AggressionForRound(int r) { return Mathf.Clamp01((r - 1) / 20f); }
        public static float DamageScaleForRound(int r) { return 1f + Mathf.Clamp((r - 5) / 45f, 0f, 0.9f); }
        public static float SpawnIntervalForRound(int r) { return Mathf.Clamp(2.7f - r * 0.115f, 0.34f, 2.7f); }

        public int MaxConcurrent(int r)
        {
            return Mathf.Min(_zombies.MaxConcurrent, 8 + Mathf.FloorToInt(r * 1.6f));
        }

        public static string SpecialForRound(int r)
        {
            if (r > 0 && r % 12 == 0) return "boss";
            if (r > 0 && r % 8 == 0) return "horde";
            return null;
        }

        public int Remaining { get { return toSpawn - spawnedThisRound + _zombies.LivingCount; } }

        // ------------------------------------------------------------- flow
        public void StartRun()
        {
            round = 0;
            phase = RoundPhase.Intro;
            timer = firstRoundDelay;
            killsThisRound = 0;
        }

        public void BeginRound(int r)
        {
            round = r;
            special = SpecialForRound(r);
            toSpawn = CountForRound(r);
            if (special == "boss") toSpawn = Mathf.RoundToInt(toSpawn * 0.6f);
            spawnedThisRound = 0;
            killsThisRound = 0;
            _bossesToSpawn = special == "boss" ? 1 + r / 24 : 0;
            _spawnT = 1f;
            phase = RoundPhase.Spawning;
            Game.Audio.Play(Sfx.RoundStart, 1f);
            OnRoundStart(r, special);
        }

        public void EndRound()
        {
            phase = RoundPhase.Intermission;
            timer = intermission;
            OnRoundEnd(round);
        }

        public void OnKill() { killsThisRound++; }

        public void ForceClear()
        {
            spawnedThisRound = toSpawn;
            if (phase == RoundPhase.Spawning) phase = RoundPhase.Clearing;
        }

        public void Stop() { phase = RoundPhase.Over; }

        // ------------------------------------------------------------- spawning
        bool SpawnOne(Vector3 playerPos)
        {
            var bars = _world.ActiveBarricades();
            if (bars.Count == 0) return false;

            // Weight toward the player, with a floor so far windows still feed.
            var weights = new float[bars.Count];
            for (int i = 0; i < bars.Count; i++)
            {
                float d = bars[i].DistanceTo(playerPos.x, playerPos.z);
                weights[i] = Mathf.Max(0.12f, 1f / (1f + d * 0.06f)) / (1f + bars[i].attackers.Count * 0.8f);
            }
            var chosen = bars[Util.WeightedPick(weights)];

            string type;
            if (_bossesToSpawn > 0) { type = "boss"; _bossesToSpawn--; }
            else if (special == "horde") type = Random.value < 0.78f ? "runner" : "normal";
            else type = Zombie.RandomTypeForRound(round);

            var p = chosen.alcoveCenter + new Vector3(Util.Rand(-0.6f, 0.6f), 0f, Util.Rand(-0.6f, 0.6f));
            _zombies.Spawn(type, p, HealthForRound(round), AggressionForRound(round),
                DamageScaleForRound(round), chosen);
            spawnedThisRound++;
            return true;
        }

        public void Update(float dt, Vector3 playerPos)
        {
            switch (phase)
            {
                case RoundPhase.Intro:
                    timer -= dt;
                    if (timer <= 0f) BeginRound(1);
                    break;

                case RoundPhase.Spawning:
                    _spawnT -= dt;
                    if (_spawnT <= 0f && _zombies.LivingCount < MaxConcurrent(round))
                    {
                        _spawnT = SpawnIntervalForRound(round) * Util.Rand(0.7f, 1.3f);
                        SpawnOne(playerPos);
                        if (spawnedThisRound >= toSpawn) phase = RoundPhase.Clearing;
                    }
                    break;

                case RoundPhase.Clearing:
                    if (_zombies.LivingCount == 0) EndRound();
                    break;

                case RoundPhase.Intermission:
                    timer -= dt;
                    if (timer <= 0f) BeginRound(round + 1);
                    break;
            }
        }
    }
}
