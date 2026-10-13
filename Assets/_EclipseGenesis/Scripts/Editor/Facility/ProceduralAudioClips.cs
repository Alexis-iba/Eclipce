using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EclipseGenesis.EditorTools.Facility
{
    /// <summary>
    /// Synthesises provisional ambience/SFX as 16-bit WAV assets (replaceable by recorded audio later,
    /// see Docs/ASSETS_REQUIRED.md). Loops are cross-faded so they repeat without clicks.
    /// </summary>
    public static class ProceduralAudioClips
    {
        public const string Folder = "Assets/_EclipseGenesis/Audio/Generated";
        private const int Rate = 44100;

        public static AudioClip Hum() => Get("Amb_FacilityHum", () => Loop(6f, (t, rnd, s) =>
            0.22f * Mathf.Sin(2f * Mathf.PI * 50f * t) + 0.12f * Mathf.Sin(2f * Mathf.PI * 100f * t) +
            0.05f * Mathf.Sin(2f * Mathf.PI * 150f * t) + 0.18f * s.Brown(rnd)));

        public static AudioClip VentAir() => Get("Amb_VentAir", () => Loop(5f, (t, rnd, s) =>
            0.35f * s.Pink(rnd) * (0.8f + 0.2f * Mathf.Sin(2f * Mathf.PI * 0.4f * t))));

        public static AudioClip Alarm() => Get("Sfx_AlarmSiren", () => Loop(2f, (t, rnd, s) =>
        {
            float f = t % 1f < 0.5f ? 640f : 820f;
            float w = Mathf.Sin(2f * Mathf.PI * f * t);
            return 0.35f * Mathf.Clamp(w * 2.5f, -1f, 1f);
        }));

        public static AudioClip DoorSlide() => Get("Sfx_DoorSlide", () => OneShot(0.9f, (t, rnd, s) =>
        {
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.9f - t) / 0.3f);
            float motor = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(90f, 170f, t / 0.9f) * t);
            float hiss = s.Pink(rnd);
            float clunk = t > 0.78f ? Mathf.Sin(2f * Mathf.PI * 60f * t) * Mathf.Exp(-(t - 0.78f) * 30f) : 0f;
            return env * (0.35f * motor + 0.3f * hiss) + 0.6f * clunk;
        }));

        public static AudioClip DoorDenied() => Get("Sfx_DoorDenied", () => OneShot(0.45f, (t, rnd, s) =>
        {
            float beep = (t < 0.15f || (t > 0.22f && t < 0.37f)) ? 1f : 0f;
            return 0.3f * beep * Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 330f * t));
        }));

        // ------------------------------------------------------------------ weapons

        /// <summary>Rifle shot: sharp crack, low body thump and a short interior tail.</summary>
        public static AudioClip RifleShot() => Get("Wpn_EG47_Shot", () => OneShot(0.65f, (t, rnd, s) =>
        {
            float crack = s.White(rnd) * Mathf.Exp(-t * 90f);
            float body = Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(140f, 55f, Mathf.Clamp01(t * 8f)) * t) * Mathf.Exp(-t * 22f);
            float tail = s.Pink(rnd) * Mathf.Exp(-t * 7f) * 0.35f;
            float mech = t > 0.03f && t < 0.045f ? s.White(rnd) * 0.4f : 0f;
            return 1.1f * crack + 0.9f * body + tail + mech;
        }));

        /// <summary>
        /// EG-47 v2 outdoor shot: broadband muzzle blast, pitch-dropping low boom, high-mid snap, bolt clacks,
        /// two slapback echoes off the houses and a long rumbling tail; soft-saturated for punch.
        /// </summary>
        public static AudioClip RifleShotV2() => Get("Wpn_EG47_Shot_v2", () =>
        {
            float lp1 = 0f, lp2 = 0f, hp = 0f, prev = 0f, lpEcho = 0f;
            var echo = new float[Rate];
            int n = 0;
            return OneShot(1.6f, (t, rnd, s) =>
            {
                float w = s.White(rnd);
                lp1 += (w - lp1) * 0.35f;                       // ~3.5 kHz
                hp = 0.92f * (hp + lp1 - prev); prev = lp1;     // high-pass -> snap band
                lp2 += (s.Brown(rnd) - lp2) * 0.02f;            // rumble
                float blast = w * Mathf.Exp(-t * 900f) * 1.4f;
                float snap = hp * Mathf.Exp(-t * 40f) * 2.2f;
                float f = Mathf.Lerp(95f, 38f, Mathf.Clamp01(t * 6f));
                float boom = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t * 9f) * 1.3f;
                float Clack(float at, float freq) => t >= at ? Mathf.Sin(2f * Mathf.PI * freq * (t - at)) * Mathf.Exp(-(t - at) * 160f) : 0f;
                float mech = 0.35f * Clack(0.028f, 2600f) + 0.3f * Clack(0.052f, 1350f);
                float tail = lp2 * Mathf.Exp(-t * 2.4f) * 6f * Mathf.Clamp01(t * 20f);
                float dry = blast + snap + boom + mech + tail;
                // Slapback echoes (120 ms and 270 ms), low-passed.
                float e = 0f;
                int i120 = n - (int)(0.12f * Rate), i270 = n - (int)(0.27f * Rate);
                if (i120 >= 0 && i120 < echo.Length) e += echo[i120] * 0.38f;
                if (i270 >= 0 && i270 < echo.Length) e += echo[i270] * 0.22f;
                lpEcho += (e - lpEcho) * 0.18f;
                if (n < echo.Length) echo[n] = blast + snap;
                n++;
                float x = dry + lpEcho;
                return (float)System.Math.Tanh(x * 1.6f);
            });
        });

        public static AudioClip RifleReload() => Get("Wpn_EG47_Reload", () => OneShot(2.3f, (t, rnd, s) =>
        {
            float Click(float at, float decay, float freq) => t >= at ? Mathf.Sin(2f * Mathf.PI * freq * (t - at)) * Mathf.Exp(-(t - at) * decay) : 0f;
            float slide = t > 0.45f && t < 0.75f ? s.Pink(rnd) * 0.25f : 0f;
            float insert = t > 1.35f && t < 1.5f ? s.Pink(rnd) * 0.3f : 0f;
            return 0.7f * Click(0.35f, 60f, 1800f) + slide + insert + 0.9f * Click(1.52f, 45f, 900f) + 0.8f * Click(1.95f, 50f, 1400f) + 0.7f * Click(2.05f, 55f, 700f);
        }));

        public static AudioClip DryFire() => Get("Wpn_DryFire", () => OneShot(0.15f, (t, rnd, s) =>
            Mathf.Sin(2f * Mathf.PI * 2200f * t) * Mathf.Exp(-t * 80f) + 0.3f * s.White(rnd) * Mathf.Exp(-t * 120f)));

        public static AudioClip Hitmarker() => Get("UI_Hitmarker", () => OneShot(0.08f, (t, rnd, s) =>
            Mathf.Sin(2f * Mathf.PI * 3200f * t) * Mathf.Exp(-t * 70f)));

        // ------------------------------------------------------------------ aliens

        public static AudioClip AlienScreech() => Get("Alien_Stalker_Screech", () => OneShot(0.9f, (t, rnd, s) =>
        {
            float f = Mathf.Lerp(900f, 420f, t / 0.9f) + 60f * Mathf.Sin(2f * Mathf.PI * 28f * t);
            float env = Mathf.Clamp01(t / 0.05f) * Mathf.Exp(-t * 2.5f);
            return env * (0.6f * Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * s.Pink(rnd));
        }));

        /// <summary>Stalker v2 voice: formant-filtered vocal rasp with vibrato, a 32 Hz growl modulation and a sub roar.</summary>
        private static float AlienVoice(float t, float f0, float rasp, float growl, ref float bp1, ref float bp2, System.Random rnd, State s)
        {
            float vib = 1f + 0.04f * Mathf.Sin(2f * Mathf.PI * 7f * t) + 0.02f * (s.Pink(rnd));
            float ph = 2f * Mathf.PI * f0 * vib * t;
            float voice = 0f;
            for (int h = 1; h <= 9; h++) voice += Mathf.Sin(ph * h + h * 0.7f) / (h * 0.9f);   // buzzy source
            float noise = s.White(rnd);
            bp1 += (noise - bp1) * 0.25f;
            bp2 += (bp1 - bp2) * 0.25f;
            float raspy = (bp1 - bp2) * rasp;
            float am = 1f - growl * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 32f * t));
            float sub = Mathf.Sin(ph * 0.5f) * 0.6f;
            return (voice * 0.35f + raspy * 2.5f + sub) * am;
        }

        public static AudioClip AlienScreechV2() => Get("Alien_Stalker_Screech_v2", () =>
        {
            float a = 0f, b = 0f;
            return OneShot(1.5f, (t, rnd, s) =>
            {
                float f0 = Mathf.Lerp(310f, 520f, Mathf.Clamp01(t / 0.35f)) - Mathf.Clamp01((t - 0.6f) / 0.9f) * 260f;
                float env = Mathf.Clamp01(t / 0.06f) * (t < 0.9f ? 1f : Mathf.Exp(-(t - 0.9f) * 5f));
                return (float)System.Math.Tanh(env * AlienVoice(t, f0, 1.4f, 0.55f, ref a, ref b, rnd, s) * 1.4f);
            });
        });

        public static AudioClip AlienAttackV2() => Get("Alien_Stalker_Attack_v2", () =>
        {
            float a = 0f, b = 0f;
            return OneShot(0.6f, (t, rnd, s) =>
            {
                float env = Mathf.Clamp01(t / 0.015f) * Mathf.Exp(-t * 5f);
                float f0 = Mathf.Lerp(190f, 120f, t / 0.6f);
                return (float)System.Math.Tanh(env * AlienVoice(t, f0, 2.2f, 0.8f, ref a, ref b, rnd, s) * 1.8f);
            });
        });

        public static AudioClip AlienDeathV2() => Get("Alien_Stalker_Death_v2", () =>
        {
            float a = 0f, b = 0f;
            return OneShot(2.2f, (t, rnd, s) =>
            {
                float f0 = Mathf.Lerp(260f, 55f, Mathf.Clamp01(t / 1.8f)) * (1f + 0.15f * Mathf.Sin(2f * Mathf.PI * 9f * t));
                float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 1.4f);
                float gurgle = s.Brown(rnd) * (0.5f + 0.5f * Mathf.Sin(2f * Mathf.PI * 13f * t));
                return (float)System.Math.Tanh(env * (AlienVoice(t, f0, 1.8f, 0.7f, ref a, ref b, rnd, s) + gurgle * 1.5f) * 1.3f);
            });
        });

        public static AudioClip AlienHurtV2() => Get("Alien_Stalker_Hurt_v2", () =>
        {
            float a = 0f, b = 0f;
            return OneShot(0.4f, (t, rnd, s) =>
            {
                float env = Mathf.Clamp01(t / 0.01f) * Mathf.Exp(-t * 9f);
                return (float)System.Math.Tanh(env * AlienVoice(t, Mathf.Lerp(480f, 330f, t / 0.4f), 1.6f, 0.4f, ref a, ref b, rnd, s) * 1.5f);
            });
        });

        public static AudioClip AlienAttack() => Get("Alien_Stalker_Attack", () => OneShot(0.45f, (t, rnd, s) =>
        {
            float env = Mathf.Clamp01(t / 0.02f) * Mathf.Exp(-t * 6f);
            return env * (0.7f * s.Pink(rnd) + 0.5f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(260f, 140f, t / 0.45f) * t));
        }));

        public static AudioClip AlienDeath() => Get("Alien_Stalker_Death", () => OneShot(1.4f, (t, rnd, s) =>
        {
            float f = Mathf.Lerp(380f, 70f, t / 1.4f);
            float env = Mathf.Clamp01(t / 0.03f) * Mathf.Exp(-t * 2f);
            return env * (0.6f * Mathf.Sin(2f * Mathf.PI * f * t + 3f * Mathf.Sin(2f * Mathf.PI * 13f * t)) + 0.3f * s.Brown(rnd));
        }));

        public static AudioClip AlienHurt() => Get("Alien_Stalker_Hurt", () => OneShot(0.3f, (t, rnd, s) =>
        {
            float env = Mathf.Exp(-t * 12f);
            return env * (0.6f * Mathf.Sin(2f * Mathf.PI * Mathf.Lerp(700f, 500f, t / 0.3f) * t) + 0.4f * s.Pink(rnd));
        }));

        public static AudioClip SparkCrackle() => Get("Sfx_SparkCrackle", () => OneShot(0.5f, (t, rnd, s) =>
        {
            float env = Mathf.Exp(-t * 7f);
            float click = rnd.NextDouble() < 0.02 ? (float)(rnd.NextDouble() * 2 - 1) : 0f;
            return env * (0.9f * click + 0.25f * s.White(rnd) * (rnd.NextDouble() < 0.3 ? 1f : 0.2f));
        }));

        // ------------------------------------------------------------------ synthesis

        private sealed class State
        {
            private float _brown, _b0, _b1, _b2;
            public float White(System.Random r) => (float)(r.NextDouble() * 2.0 - 1.0);

            public float Brown(System.Random r)
            {
                _brown = Mathf.Clamp(_brown + White(r) * 0.02f, -1f, 1f) * 0.998f;
                return _brown * 3f;
            }

            public float Pink(System.Random r)
            {
                float w = White(r);
                _b0 = 0.99765f * _b0 + w * 0.0990460f;
                _b1 = 0.96300f * _b1 + w * 0.2965164f;
                _b2 = 0.57000f * _b2 + w * 1.0526913f;
                return (_b0 + _b1 + _b2 + w * 0.1848f) * 0.2f;
            }
        }

        private delegate float Sample(float t, System.Random rnd, State s);

        private static float[] OneShot(float seconds, Sample f)
        {
            var rnd = new System.Random(7);
            var s = new State();
            var data = new float[(int)(seconds * Rate)];
            for (int i = 0; i < data.Length; i++) data[i] = f((float)i / Rate, rnd, s);
            return data;
        }

        private static float[] Loop(float seconds, Sample f)
        {
            const float fade = 0.4f;
            int n = (int)(seconds * Rate), nf = (int)(fade * Rate);
            var raw = OneShot(seconds + fade, f);
            var data = new float[n];
            Array.Copy(raw, data, n);
            // Cross-fade the tail beyond the loop point into the start.
            for (int i = 0; i < nf; i++)
            {
                float a = (float)i / nf;
                data[i] = Mathf.Lerp(raw[n + i], raw[i], a);
            }
            return data;
        }

        private static AudioClip Get(string name, Func<float[]> make)
        {
            string path = $"{Folder}/{name}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null) return clip;

            Directory.CreateDirectory(Folder);
            WriteWav(path, Normalize(make(), 0.8f));
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }

        private static float[] Normalize(float[] d, float peak)
        {
            float max = 0.0001f;
            foreach (float v in d) max = Mathf.Max(max, Mathf.Abs(v));
            float k = peak / max;
            for (int i = 0; i < d.Length; i++) d[i] *= k;
            return d;
        }

        private static void WriteWav(string path, float[] samples)
        {
            using var fs = new FileStream(path, FileMode.Create);
            using var w = new BinaryWriter(fs);
            int byteCount = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' });
            w.Write(36 + byteCount);
            w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(Rate);
            w.Write(Rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' });
            w.Write(byteCount);
            foreach (float s in samples) w.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767f));
        }
    }
}
