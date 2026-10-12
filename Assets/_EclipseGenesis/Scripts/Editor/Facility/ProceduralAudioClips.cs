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
