using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum Bind
    {
        Forward, Back, Left, Right, Jump, Sprint, Crouch, Interact,
        Reload, Melee, Swap, Slot1, Slot2, Fire, Ads, Flashlight, Scoreboard
    }

    public struct QualityPreset
    {
        public string label;
        public bool shadows;
        public int shadowRes;
        public float fogDensity;
        public float particles;
        public int maxZombies;
        public int dynamicLights;

        public QualityPreset(string l, bool sh, int sr, float fog, float part, int mz, int dl)
        {
            label = l; shadows = sh; shadowRes = sr; fogDensity = fog;
            particles = part; maxZombies = mz; dynamicLights = dl;
        }
    }

    /// <summary>Persistent player settings, stored in PlayerPrefs.</summary>
    public static class GameSettings
    {
        public const string PrefPrefix = "rotgrid.";

        public static string PlayerName = "OPERATOR";
        public static float MasterVolume = 0.8f;
        public static float MusicVolume = 0.5f;
        public static float SfxVolume = 0.9f;
        public static float MouseSensitivity = 0.16f;
        public static float ControllerSensitivity = 2.2f;
        public static bool InvertY = false;
        public static float Fov = 80f;
        public static bool MotionBlur = true;
        public static bool ScreenShake = true;
        public static string Quality = "high";
        public static bool Fullscreen = false;
        public static bool ShowCrosshair = true;

        static readonly Dictionary<Bind, KeyCode> _binds = new Dictionary<Bind, KeyCode>();
        public static event Action Changed;

        public static readonly Dictionary<string, QualityPreset> Presets = new Dictionary<string, QualityPreset>
        {
            { "low",    new QualityPreset("Low",    false, 512,  0.050f, 0.4f, 16, 2) },
            { "medium", new QualityPreset("Medium", true,  1024, 0.042f, 0.8f, 24, 5) },
            { "high",   new QualityPreset("High",   true,  2048, 0.036f, 1.0f, 30, 8) },
            { "ultra",  new QualityPreset("Ultra",  true,  2048, 0.030f, 1.5f, 38, 12) },
        };

        public static QualityPreset Preset
        {
            get { return Presets.ContainsKey(Quality) ? Presets[Quality] : Presets["high"]; }
        }

        static readonly Dictionary<Bind, KeyCode> Defaults = new Dictionary<Bind, KeyCode>
        {
            { Bind.Forward, KeyCode.W }, { Bind.Back, KeyCode.S },
            { Bind.Left, KeyCode.A }, { Bind.Right, KeyCode.D },
            { Bind.Jump, KeyCode.Space }, { Bind.Sprint, KeyCode.LeftShift },
            { Bind.Crouch, KeyCode.LeftControl }, { Bind.Interact, KeyCode.F },
            { Bind.Reload, KeyCode.R }, { Bind.Melee, KeyCode.V },
            { Bind.Swap, KeyCode.Q }, { Bind.Slot1, KeyCode.Alpha1 },
            { Bind.Slot2, KeyCode.Alpha2 }, { Bind.Fire, KeyCode.Mouse0 },
            { Bind.Ads, KeyCode.Mouse1 }, { Bind.Flashlight, KeyCode.L },
            { Bind.Scoreboard, KeyCode.Tab },
        };

        public static string Label(Bind b)
        {
            switch (b)
            {
                case Bind.Forward: return "Move Forward";
                case Bind.Back: return "Move Backward";
                case Bind.Left: return "Strafe Left";
                case Bind.Right: return "Strafe Right";
                case Bind.Jump: return "Jump / Mantle";
                case Bind.Sprint: return "Sprint";
                case Bind.Crouch: return "Crouch / Slide";
                case Bind.Interact: return "Interact";
                case Bind.Reload: return "Reload";
                case Bind.Melee: return "Melee Attack";
                case Bind.Swap: return "Swap Weapon";
                case Bind.Slot1: return "Weapon Slot 1";
                case Bind.Slot2: return "Weapon Slot 2";
                case Bind.Fire: return "Fire";
                case Bind.Ads: return "Aim Down Sights";
                case Bind.Flashlight: return "Flashlight";
                default: return "Scoreboard";
            }
        }

        public static KeyCode Key(Bind b)
        {
            KeyCode k;
            if (_binds.TryGetValue(b, out k)) return k;
            return Defaults[b];
        }

        public static void SetKey(Bind b, KeyCode k)
        {
            // A key may only drive one action.
            var conflicts = new List<Bind>();
            foreach (var kv in _binds) if (kv.Value == k && kv.Key != b) conflicts.Add(kv.Key);
            foreach (var c in conflicts) _binds[c] = KeyCode.None;
            _binds[b] = k;
            Save();
        }

        public static void ResetBinds()
        {
            _binds.Clear();
            foreach (var kv in Defaults) _binds[kv.Key] = kv.Value;
            Save();
        }

        public static string KeyLabel(KeyCode k)
        {
            if (k == KeyCode.None) return "—";
            switch (k)
            {
                case KeyCode.Mouse0: return "LMB";
                case KeyCode.Mouse1: return "RMB";
                case KeyCode.Mouse2: return "MMB";
                case KeyCode.LeftShift: return "L SHIFT";
                case KeyCode.RightShift: return "R SHIFT";
                case KeyCode.LeftControl: return "L CTRL";
                case KeyCode.RightControl: return "R CTRL";
                case KeyCode.LeftAlt: return "L ALT";
                case KeyCode.RightAlt: return "R ALT";
                case KeyCode.Space: return "SPACE";
                case KeyCode.Tab: return "TAB";
                case KeyCode.Escape: return "ESC";
                case KeyCode.Return: return "ENTER";
                default: break;
            }
            string s = k.ToString();
            if (s.StartsWith("Alpha")) return s.Substring(5);
            if (s.StartsWith("Keypad")) return "NUM " + s.Substring(6);
            return s.ToUpperInvariant();
        }

        public static void Load()
        {
            ResetBinds();
            PlayerName = PlayerPrefs.GetString(PrefPrefix + "name", PlayerName);
            MasterVolume = PlayerPrefs.GetFloat(PrefPrefix + "master", MasterVolume);
            MusicVolume = PlayerPrefs.GetFloat(PrefPrefix + "music", MusicVolume);
            SfxVolume = PlayerPrefs.GetFloat(PrefPrefix + "sfx", SfxVolume);
            MouseSensitivity = PlayerPrefs.GetFloat(PrefPrefix + "mouse", MouseSensitivity);
            ControllerSensitivity = PlayerPrefs.GetFloat(PrefPrefix + "pad", ControllerSensitivity);
            InvertY = PlayerPrefs.GetInt(PrefPrefix + "inv", 0) == 1;
            Fov = PlayerPrefs.GetFloat(PrefPrefix + "fov", Fov);
            MotionBlur = PlayerPrefs.GetInt(PrefPrefix + "mblur", 1) == 1;
            ScreenShake = PlayerPrefs.GetInt(PrefPrefix + "shake", 1) == 1;
            ShowCrosshair = PlayerPrefs.GetInt(PrefPrefix + "cross", 1) == 1;
            Quality = PlayerPrefs.GetString(PrefPrefix + "quality", Quality);
            Fullscreen = PlayerPrefs.GetInt(PrefPrefix + "fs", 0) == 1;

            foreach (Bind b in Enum.GetValues(typeof(Bind)))
            {
                int stored = PlayerPrefs.GetInt(PrefPrefix + "bind." + b, -1);
                if (stored >= 0) _binds[b] = (KeyCode)stored;
            }
        }

        public static void Save()
        {
            PlayerPrefs.SetString(PrefPrefix + "name", PlayerName);
            PlayerPrefs.SetFloat(PrefPrefix + "master", MasterVolume);
            PlayerPrefs.SetFloat(PrefPrefix + "music", MusicVolume);
            PlayerPrefs.SetFloat(PrefPrefix + "sfx", SfxVolume);
            PlayerPrefs.SetFloat(PrefPrefix + "mouse", MouseSensitivity);
            PlayerPrefs.SetFloat(PrefPrefix + "pad", ControllerSensitivity);
            PlayerPrefs.SetInt(PrefPrefix + "inv", InvertY ? 1 : 0);
            PlayerPrefs.SetFloat(PrefPrefix + "fov", Fov);
            PlayerPrefs.SetInt(PrefPrefix + "mblur", MotionBlur ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "shake", ScreenShake ? 1 : 0);
            PlayerPrefs.SetInt(PrefPrefix + "cross", ShowCrosshair ? 1 : 0);
            PlayerPrefs.SetString(PrefPrefix + "quality", Quality);
            PlayerPrefs.SetInt(PrefPrefix + "fs", Fullscreen ? 1 : 0);
            foreach (var kv in _binds) PlayerPrefs.SetInt(PrefPrefix + "bind." + kv.Key, (int)kv.Value);
            PlayerPrefs.Save();
            if (Changed != null) Changed();
        }

        public static void ResetAll()
        {
            PlayerName = "OPERATOR";
            MasterVolume = 0.8f; MusicVolume = 0.5f; SfxVolume = 0.9f;
            MouseSensitivity = 0.16f; ControllerSensitivity = 2.2f; InvertY = false;
            Fov = 80f; MotionBlur = true; ScreenShake = true; ShowCrosshair = true;
            Quality = "high"; Fullscreen = false;
            ResetBinds();
            Save();
        }

        public static void ApplyQuality()
        {
            var p = Preset;
            QualitySettings.shadows = p.shadows ? ShadowQuality.All : ShadowQuality.Disable;
            QualitySettings.shadowDistance = p.shadows ? 40f : 0f;
            if (Fullscreen != Screen.fullScreen) Screen.fullScreen = Fullscreen;
        }
    }
}
