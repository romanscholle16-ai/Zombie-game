using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    [Serializable]
    public class WeaponRecord
    {
        public string id;
        public int kills;
        public int headshots;
        public int shotsFired;
        public int shotsHit;
        public int damage;
    }

    [Serializable]
    public class RunRecord
    {
        public string date;
        public int round;
        public int kills;
        public int headshots;
        public float time;
        public int points;
    }

    [Serializable]
    public class CareerData
    {
        public int lifetimeKills;
        public int lifetimeHeadshots;
        public int highestRound;
        public int gamesPlayed;
        public float totalTimeSurvived;
        public int totalPointsEarned;
        public int totalDowns;
        public int totalRevives;
        public int barriersRepaired;
        public int boxSpins;
        public List<WeaponRecord> weapons = new List<WeaponRecord>();
        public List<RunRecord> history = new List<RunRecord>();
    }

    /// <summary>Lifetime statistics, persisted as JSON in PlayerPrefs.</summary>
    public static class SaveData
    {
        const string Key = GameSettings.PrefPrefix + "career";
        public static CareerData Data = new CareerData();

        public static void Load()
        {
            string json = PlayerPrefs.GetString(Key, "");
            if (string.IsNullOrEmpty(json)) { Data = new CareerData(); return; }
            try
            {
                var parsed = JsonUtility.FromJson<CareerData>(json);
                Data = parsed ?? new CareerData();
            }
            catch (Exception) { Data = new CareerData(); }
            if (Data.weapons == null) Data.weapons = new List<WeaponRecord>();
            if (Data.history == null) Data.history = new List<RunRecord>();
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Data));
            PlayerPrefs.Save();
        }

        public static WeaponRecord Weapon(string id)
        {
            for (int i = 0; i < Data.weapons.Count; i++) if (Data.weapons[i].id == id) return Data.weapons[i];
            var rec = new WeaponRecord { id = id };
            Data.weapons.Add(rec);
            return rec;
        }

        /// <summary>Folds a finished run into the career totals. Returns true on a new best.</summary>
        public static bool RecordRun(RunSummary s)
        {
            Data.gamesPlayed++;
            Data.lifetimeKills += s.kills;
            Data.lifetimeHeadshots += s.headshots;
            Data.totalTimeSurvived += s.time;
            Data.totalPointsEarned += s.pointsEarned;
            Data.totalDowns += s.downs;
            Data.totalRevives += s.revives;
            Data.barriersRepaired += s.repairs;
            Data.boxSpins += s.boxSpins;

            bool newBest = s.round > Data.highestRound;
            if (newBest) Data.highestRound = s.round;

            foreach (var kv in s.weaponStats)
            {
                var rec = Weapon(kv.Key);
                rec.kills += kv.Value.kills;
                rec.headshots += kv.Value.headshots;
                rec.shotsFired += kv.Value.shotsFired;
                rec.shotsHit += kv.Value.shotsHit;
                rec.damage += kv.Value.damage;
            }

            Data.history.Insert(0, new RunRecord
            {
                date = DateTime.Now.ToString("yyyy-MM-dd"),
                round = s.round,
                kills = s.kills,
                headshots = s.headshots,
                time = s.time,
                points = s.pointsEarned,
            });
            while (Data.history.Count > 10) Data.history.RemoveAt(Data.history.Count - 1);

            Save();
            return newBest;
        }

        public static void Wipe()
        {
            Data = new CareerData();
            Save();
        }
    }

    /// <summary>Per-run tally handed to the death screen and the career store.</summary>
    public class RunSummary
    {
        public int round;
        public int roundsSurvived;
        public int kills;
        public int headshots;
        public int meleeKills;
        public int repairs;
        public float time;
        public int pointsEarned;
        public int shotsFired;
        public int shotsHit;
        public int downs;
        public int revives;
        public int boxSpins;
        public bool newBest;
        public List<string> perksBought = new List<string>();
        public Dictionary<string, WeaponRecord> weaponStats = new Dictionary<string, WeaponRecord>();

        public WeaponRecord Weapon(string id)
        {
            WeaponRecord r;
            if (!weaponStats.TryGetValue(id, out r))
            {
                r = new WeaponRecord { id = id };
                weaponStats[id] = r;
            }
            return r;
        }
    }
}
