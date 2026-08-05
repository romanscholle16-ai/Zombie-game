using System.Collections.Generic;
using UnityEngine;

namespace Rotgrid
{
    public enum Sfx
    {
        ShootLight, ShootMedium, ShootHeavy, ShootShotgun, ShootEnergy,
        DryFire, Reload1, Reload2, Reload3, Melee,
        Hit, HitCrit, Hitmarker, HitmarkerCrit, Ricochet, Explosion,
        Groan, ZombieDeath, ZombieAttack,
        Board, Repair, Door, Power, Perk, BoxSpin, BoxStop, BoxStopRare, Pap,
        PowerUp, TrapElectric, TrapFire, Hurt, Step, Heartbeat,
        UiMove, UiSelect, UiBack, UiDeny, UiTick, RoundStart
    }

    /// <summary>
    /// Every sound is synthesised into an AudioClip at boot — there are no
    /// audio files in this project, so nothing is licensed from anyone.
    /// </summary>
    public class AudioEngine
    {
        const int Rate = 44100;
        const int Voices = 24;

        readonly Dictionary<Sfx, AudioClip[]> _clips = new Dictionary<Sfx, AudioClip[]>();
        readonly List<AudioSource> _pool = new List<AudioSource>();
        AudioSource _ui;
        AudioSource _ambient;
        AudioSource _music;
        GameObject _root;
        int _next;
        float _lastGroan;

        public void Init()
        {
            if (_root != null) return;
            _root = new GameObject("~Audio");
            Object.DontDestroyOnLoad(_root);

            for (int i = 0; i < Voices; i++)
            {
                var go = new GameObject("voice" + i);
                go.transform.SetParent(_root.transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 3f;
                src.maxDistance = 46f;
                src.dopplerLevel = 0f;
                _pool.Add(src);
            }

            _ui = MakeFlat("ui");
            _ambient = MakeFlat("ambient");
            _ambient.loop = true;
            _music = MakeFlat("music");
            _music.loop = true;

            BuildClips();
            ApplyVolumes();
        }

        AudioSource MakeFlat(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        public void ApplyVolumes()
        {
            AudioListener.volume = GameSettings.MasterVolume;
            if (_music != null) _music.volume = GameSettings.MusicVolume * 0.45f;
            if (_ambient != null) _ambient.volume = GameSettings.MusicVolume * 0.5f;
        }

        // ------------------------------------------------------------ playback
        public void Play(Sfx id, float volume = 1f, float pitch = 1f)
        {
            var clip = Get(id);
            if (clip == null || _ui == null) return;
            _ui.pitch = pitch;
            _ui.PlayOneShot(clip, volume * GameSettings.SfxVolume);
        }

        public void PlayAt(Sfx id, Vector3 pos, float volume = 1f, float pitch = 1f)
        {
            var clip = Get(id);
            if (clip == null || _pool.Count == 0) return;
            var src = _pool[_next];
            _next = (_next + 1) % _pool.Count;
            src.transform.position = pos;
            src.clip = clip;
            src.pitch = pitch;
            src.volume = volume * GameSettings.SfxVolume;
            src.Play();
        }

        AudioClip Get(Sfx id)
        {
            AudioClip[] variants;
            if (!_clips.TryGetValue(id, out variants) || variants.Length == 0) return null;
            return variants[Random.Range(0, variants.Length)];
        }

        /// <summary>Rate-limited ambient groan so a big horde does not turn to mush.</summary>
        public void MaybeGroan(Vector3 pos, float now, float pitch)
        {
            if (now - _lastGroan < 0.55f) return;
            _lastGroan = now;
            PlayAt(Sfx.Groan, pos, 0.9f, pitch);
        }

        public void StartAmbient()
        {
            if (_ambient == null) return;
            if (_ambient.clip == null) _ambient.clip = BuildAmbientLoop();
            _ambient.volume = GameSettings.MusicVolume * 0.5f;
            _ambient.Play();
        }

        public void StopAmbient() { if (_ambient != null) _ambient.Stop(); }

        public void StartMusic()
        {
            if (_music == null) return;
            if (_music.clip == null) _music.clip = BuildMusicLoop();
            _music.volume = GameSettings.MusicVolume * 0.45f;
            _music.Play();
        }

        public void StopMusic() { if (_music != null) _music.Stop(); }

        // ------------------------------------------------------------ synthesis
        static AudioClip FromSamples(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        void BuildClips()
        {
            _clips[Sfx.ShootLight] = Variants(3, i => Gunshot(0.15f, 3600f, 240f, 150f, 0.55f));
            _clips[Sfx.ShootMedium] = Variants(3, i => Gunshot(0.2f, 3000f, 180f, 110f, 0.8f));
            _clips[Sfx.ShootHeavy] = Variants(3, i => Gunshot(0.3f, 2400f, 120f, 78f, 1.1f));
            _clips[Sfx.ShootShotgun] = Variants(2, i => Gunshot(0.4f, 2000f, 90f, 62f, 1.3f));
            _clips[Sfx.ShootEnergy] = Variants(2, i => Energy(0.3f));
            _clips[Sfx.DryFire] = Variants(1, i => Click(0.05f, 2600f, 0.35f));
            _clips[Sfx.Reload1] = Variants(2, i => Mech(900f));
            _clips[Sfx.Reload2] = Variants(2, i => Mech(620f));
            _clips[Sfx.Reload3] = Variants(2, i => Mech(1500f));
            _clips[Sfx.Melee] = Variants(2, i => Swipe());
            _clips[Sfx.Hit] = Variants(3, i => Impact(false));
            _clips[Sfx.HitCrit] = Variants(3, i => Impact(true));
            _clips[Sfx.Hitmarker] = Variants(1, i => Beep(1280f, 0.055f, 0.35f, false));
            _clips[Sfx.HitmarkerCrit] = Variants(1, i => Beep(1750f, 0.055f, 0.35f, false));
            _clips[Sfx.Ricochet] = Variants(3, i => Ricochet());
            _clips[Sfx.Explosion] = Variants(2, i => Explosion());
            _clips[Sfx.Groan] = Variants(5, i => Groan());
            _clips[Sfx.ZombieDeath] = Variants(3, i => ZombieDeath());
            _clips[Sfx.ZombieAttack] = Variants(3, i => Swipe());
            _clips[Sfx.Board] = Variants(3, i => WoodCrack());
            _clips[Sfx.Repair] = Variants(2, i => Hammer());
            _clips[Sfx.Door] = Variants(1, i => DoorSlide());
            _clips[Sfx.Power] = Variants(1, i => PowerUpSurge());
            _clips[Sfx.Perk] = Variants(1, i => Jingle(new float[] { 392f, 523.25f, 659.25f, 784f }, 0.11f));
            _clips[Sfx.BoxSpin] = Variants(1, i => BoxSpin());
            _clips[Sfx.BoxStop] = Variants(1, i => Chord(new float[] { 392f, 494f, 587f }, 1.4f));
            _clips[Sfx.BoxStopRare] = Variants(1, i => Chord(new float[] { 523.25f, 659.25f, 830.6f, 1046.5f }, 1.6f));
            _clips[Sfx.Pap] = Variants(1, i => Sweep());
            _clips[Sfx.PowerUp] = Variants(1, i => Jingle(new float[] { 659.25f, 880f, 1174.7f }, 0.07f));
            _clips[Sfx.TrapElectric] = Variants(1, i => Electric());
            _clips[Sfx.TrapFire] = Variants(1, i => FireJet());
            _clips[Sfx.Hurt] = Variants(3, i => Hurt());
            _clips[Sfx.Step] = Variants(4, i => Step());
            _clips[Sfx.Heartbeat] = Variants(1, i => Heartbeat());
            _clips[Sfx.UiMove] = Variants(1, i => Beep(620f, 0.08f, 0.3f, true));
            _clips[Sfx.UiSelect] = Variants(1, i => Beep(880f, 0.09f, 0.32f, true));
            _clips[Sfx.UiBack] = Variants(1, i => Beep(380f, 0.09f, 0.3f, true));
            _clips[Sfx.UiDeny] = Variants(1, i => Deny());
            _clips[Sfx.UiTick] = Variants(1, i => Beep(1200f, 0.05f, 0.25f, true));
            _clips[Sfx.RoundStart] = Variants(1, i => RoundSting());
        }

        AudioClip[] Variants(int n, System.Func<int, float[]> gen)
        {
            var arr = new AudioClip[n];
            for (int i = 0; i < n; i++) arr[i] = FromSamples("sfx", gen(i));
            return arr;
        }

        static float Noise() { return Random.value * 2f - 1f; }

        /// <summary>One-pole low-pass, used to shape noise bursts.</summary>
        struct Lp
        {
            public float y;
            public float Step(float x, float cutoff)
            {
                float a = Mathf.Clamp01(cutoff / (Rate * 0.5f));
                y += a * (x - y);
                return y;
            }
        }

        static float[] Gunshot(float dur, float hiHz, float loHz, float bodyHz, float punch)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var buf = new float[n];
            var lp = new Lp();
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Exp(-t * 9f);
                float cutoff = Mathf.Lerp(hiHz, loHz, t);
                float crack = lp.Step(Noise(), cutoff) * env * 0.9f;

                float bf = Mathf.Lerp(bodyHz * 2.2f, bodyHz * 0.5f, t);
                phase += bf / Rate;
                float body = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 12f) * punch * 0.45f;

                buf[i] = Mathf.Clamp(crack + body, -1f, 1f);
            }
            return buf;
        }

        static float[] Energy(float dur)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(1400f, 180f, t * t);
                phase += f / Rate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                float env = Mathf.Exp(-t * 7f);
                buf[i] = Mathf.Clamp((saw * 0.5f + Noise() * 0.25f) * env, -1f, 1f);
            }
            return buf;
        }

        static float[] Click(float dur, float hz, float amp)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var buf = new float[n];
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                buf[i] = (Noise() - lp.Step(Noise(), hz)) * Mathf.Exp(-t * 28f) * amp;
            }
            return buf;
        }

        static float[] Mech(float hz)
        {
            int n = Mathf.RoundToInt(Rate * 0.1f);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(hz, hz * 0.55f, t) / Rate;
                float sq = Mathf.Sin(phase * Mathf.PI * 2f) > 0f ? 1f : -1f;
                float env = Mathf.Exp(-t * 24f);
                buf[i] = (sq * 0.18f + Noise() * 0.22f) * env;
            }
            return buf;
        }

        static float[] Swipe()
        {
            int n = Mathf.RoundToInt(Rate * 0.2f);
            var buf = new float[n];
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float cutoff = Mathf.Lerp(2400f, 320f, t);
                float env = Mathf.Sin(t * Mathf.PI);
                buf[i] = lp.Step(Noise(), cutoff) * env * 0.7f;
            }
            return buf;
        }

        static float[] Impact(bool crit)
        {
            int n = Mathf.RoundToInt(Rate * 0.13f);
            var buf = new float[n];
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float cutoff = Mathf.Lerp(crit ? 2600f : 1200f, 300f, t);
                buf[i] = lp.Step(Noise(), cutoff) * Mathf.Exp(-t * 16f) * (crit ? 0.75f : 0.55f);
            }
            return buf;
        }

        static float[] Beep(float hz, float dur, float amp, bool square)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float s = Mathf.Sin((float)i * hz / Rate * Mathf.PI * 2f);
                if (square) s = s > 0f ? 1f : -1f;
                buf[i] = s * Mathf.Exp(-t * 14f) * amp;
            }
            return buf;
        }

        static float[] Deny()
        {
            int n = Mathf.RoundToInt(Rate * 0.18f);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(180f, 90f, t) / Rate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                buf[i] = saw * Mathf.Exp(-t * 8f) * 0.35f;
            }
            return buf;
        }

        static float[] Ricochet()
        {
            int n = Mathf.RoundToInt(Rate * 0.26f);
            var buf = new float[n];
            float phase = 0f;
            float start = Random.Range(1800f, 3200f);
            float end = Random.Range(400f, 800f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(start, end, t) / Rate;
                buf[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 9f) * 0.3f;
            }
            return buf;
        }

        static float[] Explosion()
        {
            int n = Mathf.RoundToInt(Rate * 1.2f);
            var buf = new float[n];
            var lp = new Lp();
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float cutoff = Mathf.Lerp(1800f, 80f, Mathf.Sqrt(t));
                float boom = lp.Step(Noise(), cutoff) * Mathf.Exp(-t * 4.5f);
                phase += Mathf.Lerp(90f, 24f, t) / Rate;
                float sub = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 6f) * 0.8f;
                buf[i] = Mathf.Clamp(boom + sub, -1f, 1f);
            }
            return buf;
        }

        static float[] Groan()
        {
            float dur = Random.Range(0.7f, 1.3f);
            int n = Mathf.RoundToInt(Rate * dur);
            var buf = new float[n];
            float baseHz = Random.Range(58f, 96f);
            float endHz = baseHz * Random.Range(0.7f, 1.25f);
            float f1 = Random.Range(420f, 620f);
            float f2 = Random.Range(950f, 1500f);
            float phase = 0f;
            float b1 = 0f, b1p = 0f, b2 = 0f, b2p = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(baseHz, endHz, t) / Rate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                // Two resonant band-passes make a throat-like vowel.
                b1 += (saw - b1) * (f1 / Rate) * 6f; b1p += (b1 - b1p) * (f1 / Rate) * 6f;
                b2 += (saw - b2) * (f2 / Rate) * 6f; b2p += (b2 - b2p) * (f2 / Rate) * 6f;
                float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                buf[i] = Mathf.Clamp(((b1 - b1p) * 1.6f + (b2 - b2p) * 1.1f + Noise() * 0.08f) * env * 0.9f, -1f, 1f);
            }
            return buf;
        }

        static float[] ZombieDeath()
        {
            var g = Groan();
            var lp = new Lp();
            for (int i = 0; i < g.Length; i++)
            {
                float t = (float)i / g.Length;
                g[i] = Mathf.Clamp(g[i] * 1.1f + lp.Step(Noise(), Mathf.Lerp(900f, 120f, t)) * Mathf.Exp(-t * 6f) * 0.4f, -1f, 1f);
            }
            return g;
        }

        static float[] WoodCrack()
        {
            int n = Mathf.RoundToInt(Rate * 0.3f);
            var buf = new float[n];
            var lp = new Lp();
            float peak = Random.Range(700f, 1400f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float cutoff = Mathf.Lerp(peak, 180f, t);
                float crackle = Random.value < 0.06f ? Noise() * 1.4f : 0f;
                buf[i] = Mathf.Clamp((lp.Step(Noise(), cutoff) + crackle) * Mathf.Exp(-t * 8f) * 0.7f, -1f, 1f);
            }
            return buf;
        }

        static float[] Hammer()
        {
            int n = Mathf.RoundToInt(Rate * 0.2f);
            var buf = new float[n];
            for (int hit = 0; hit < 2; hit++)
            {
                int off = hit * Mathf.RoundToInt(Rate * 0.07f);
                float hz = Random.Range(300f, 460f);
                for (int i = 0; i + off < n; i++)
                {
                    float t = (float)i / (Rate * 0.1f);
                    if (t > 1f) break;
                    buf[i + off] += Mathf.Sin((float)i * hz / Rate * Mathf.PI * 2f) * Mathf.Exp(-t * 12f) * 0.4f;
                }
            }
            return buf;
        }

        static float[] DoorSlide()
        {
            int n = Mathf.RoundToInt(Rate * 0.9f);
            var buf = new float[n];
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                buf[i] = lp.Step(Noise(), Mathf.Lerp(1400f, 200f, t)) * env * 0.65f;
            }
            return buf;
        }

        static float[] PowerUpSurge()
        {
            int n = Mathf.RoundToInt(Rate * 2.6f);
            var buf = new float[n];
            var lp = new Lp();
            float p1 = 0f, p2 = 0f, p3 = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float slam = t < 0.1f ? lp.Step(Noise(), 900f) * Mathf.Exp(-t * 40f) * 0.9f : 0f;
                float k = Mathf.Clamp01((t - 0.06f) / 0.6f);
                p1 += Mathf.Lerp(22f, 55f, k) / Rate;
                p2 += Mathf.Lerp(44f, 110f, k) / Rate;
                p3 += Mathf.Lerp(66f, 165f, k) / Rate;
                float hum = (Mathf.Repeat(p1, 1f) * 2f - 1f) * 0.22f
                          + Mathf.Sin(p2 * Mathf.PI * 2f) * 0.14f
                          + Mathf.Sin(p3 * Mathf.PI * 2f) * 0.08f;
                float env = Mathf.Sin(Mathf.Clamp01((t - 0.05f) / 0.95f) * Mathf.PI);
                buf[i] = Mathf.Clamp(slam + hum * env, -1f, 1f);
            }
            return buf;
        }

        static float[] Jingle(float[] notes, float step)
        {
            int n = Mathf.RoundToInt(Rate * (step * notes.Length + 0.35f));
            var buf = new float[n];
            for (int k = 0; k < notes.Length; k++)
            {
                int off = Mathf.RoundToInt(Rate * step * k);
                for (int i = 0; i + off < n; i++)
                {
                    float t = (float)i / (Rate * 0.32f);
                    if (t > 1f) break;
                    float tri = Mathf.Asin(Mathf.Sin((float)i * notes[k] / Rate * Mathf.PI * 2f)) * (2f / Mathf.PI);
                    buf[i + off] += tri * Mathf.Exp(-t * 5f) * 0.28f;
                }
            }
            for (int i = 0; i < n; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
            return buf;
        }

        static float[] Chord(float[] notes, float dur)
        {
            int n = Mathf.RoundToInt(Rate * dur);
            var buf = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float s = 0f;
                for (int k = 0; k < notes.Length; k++) s += Mathf.Sin((float)i * notes[k] / Rate * Mathf.PI * 2f);
                buf[i] = Mathf.Clamp(s / notes.Length * Mathf.Exp(-t * 3.2f) * 0.6f, -1f, 1f);
            }
            return buf;
        }

        static float[] BoxSpin()
        {
            int n = Mathf.RoundToInt(Rate * 2f);
            var buf = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(180f, 880f, t * t) / Rate;
                float tri = Mathf.Asin(Mathf.Sin(phase * Mathf.PI * 2f)) * (2f / Mathf.PI);
                float trem = 0.75f + 0.25f * Mathf.Sin((float)i * 7f / Rate * Mathf.PI * 2f);
                float env = Mathf.Min(1f, t * 4f) * Mathf.Min(1f, (1f - t) * 5f);
                buf[i] = tri * trem * env * 0.35f;
            }
            return buf;
        }

        static float[] Sweep()
        {
            int n = Mathf.RoundToInt(Rate * 1.7f);
            var buf = new float[n];
            float bp = 0f, bpp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float f = Mathf.Lerp(400f, 3800f, t);
                bp += (Noise() - bp) * (f / Rate) * 5f;
                bpp += (bp - bpp) * (f / Rate) * 5f;
                float env = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI);
                buf[i] = Mathf.Clamp((bp - bpp) * 4f * env, -1f, 1f);
            }
            return buf;
        }

        static float[] Electric()
        {
            int n = Mathf.RoundToInt(Rate * 0.9f);
            var buf = new float[n];
            float hp = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float x = Noise();
                hp += (x - hp) * 0.35f;
                float env = Mathf.Min(1f, t * 12f) * Mathf.Exp(-t * 3.5f);
                buf[i] = (x - hp) * env * 0.8f;
            }
            return buf;
        }

        static float[] FireJet()
        {
            int n = Mathf.RoundToInt(Rate * 1.4f);
            var buf = new float[n];
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                float env = Mathf.Min(1f, t * 6f) * Mathf.Exp(-t * 2.2f);
                buf[i] = lp.Step(Noise(), 700f) * env * 0.9f;
            }
            return buf;
        }

        static float[] Hurt()
        {
            int n = Mathf.RoundToInt(Rate * 0.42f);
            var buf = new float[n];
            float phase = 0f;
            float start = Random.Range(140f, 200f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(start, 70f, t) / Rate;
                buf[i] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 7f) * 0.5f;
            }
            return buf;
        }

        static float[] Step()
        {
            int n = Mathf.RoundToInt(Rate * 0.11f);
            var buf = new float[n];
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                buf[i] = lp.Step(Noise(), 620f) * Mathf.Exp(-t * 22f) * 0.5f;
            }
            return buf;
        }

        static float[] Heartbeat()
        {
            int n = Mathf.RoundToInt(Rate * 0.45f);
            var buf = new float[n];
            for (int beat = 0; beat < 2; beat++)
            {
                int off = beat * Mathf.RoundToInt(Rate * 0.22f);
                float amp = beat == 0 ? 0.7f : 0.45f;
                float phase = 0f;
                for (int i = 0; i + off < n; i++)
                {
                    float t = (float)i / (Rate * 0.2f);
                    if (t > 1f) break;
                    phase += Mathf.Lerp(64f, 38f, t) / Rate;
                    buf[i + off] += Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Exp(-t * 7f) * amp;
                }
            }
            for (int i = 0; i < n; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
            return buf;
        }

        static float[] RoundSting()
        {
            int n = Mathf.RoundToInt(Rate * 2.5f);
            var buf = new float[n];
            float phase = 0f;
            var lp = new Lp();
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                phase += Mathf.Lerp(180f, 42f, Mathf.Sqrt(t)) / Rate;
                float saw = Mathf.Repeat(phase, 1f) * 2f - 1f;
                float low = lp.Step(saw, 700f);
                float env = Mathf.Min(1f, t * 5f) * Mathf.Exp(-t * 1.6f);
                float swell = Mathf.Sin(Mathf.Clamp01(t / 0.7f) * Mathf.PI) * 0.25f * Noise();
                buf[i] = Mathf.Clamp(low * env * 0.8f + swell * env, -1f, 1f);
            }
            return buf;
        }

        AudioClip BuildAmbientLoop()
        {
            int n = Rate * 8;
            var buf = new float[n];
            var lp = new Lp();
            float drone = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float cutoff = 260f + Mathf.Sin(t * 0.35f) * 120f;
                float wind = lp.Step(Noise(), cutoff) * 0.35f;
                drone += 47f / Rate;
                float metal = Mathf.Sin(drone * Mathf.PI * 2f) * (0.12f + 0.06f * Mathf.Sin(t * 0.19f));
                // Fade the seam so the loop does not click.
                float edge = Mathf.Min(1f, Mathf.Min((float)i, n - 1f - i) / (Rate * 0.4f));
                buf[i] = Mathf.Clamp((wind + metal) * edge, -1f, 1f);
            }
            return FromSamples("ambient", buf);
        }

        AudioClip BuildMusicLoop()
        {
            int n = Rate * 12;
            var buf = new float[n];
            float[] mult = { 1f, 1.5f, 1.7818f, 2.3784f };
            float root = 55f;
            var phases = new float[mult.Length];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float s = 0f;
                for (int v = 0; v < mult.Length; v++)
                {
                    phases[v] += root * mult[v] / Rate;
                    float saw = v % 2 == 0 ? Mathf.Repeat(phases[v], 1f) * 2f - 1f : Mathf.Sin(phases[v] * Mathf.PI * 2f);
                    float lfo = 0.7f + 0.3f * Mathf.Sin((t * (0.05f + v * 0.017f)) * Mathf.PI * 2f);
                    s += saw * lfo / (v * 0.7f + 1f);
                }
                // Sparse pulse under the pad.
                float beat = Mathf.Repeat(t, 2.1f);
                float pulse = beat < 0.4f ? Mathf.Sin(beat * 70f * Mathf.PI * 2f) * Mathf.Exp(-beat * 9f) * 0.5f : 0f;
                float edge = Mathf.Min(1f, Mathf.Min((float)i, n - 1f - i) / (Rate * 0.6f));
                buf[i] = Mathf.Clamp((s * 0.14f + pulse) * edge, -1f, 1f);
            }
            return FromSamples("music", buf);
        }
    }
}
