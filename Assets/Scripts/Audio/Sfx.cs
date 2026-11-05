using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Bartender
{
    /// <summary>All sounds are synthesised at start-up (no audio files are shipped).</summary>
    public static class Sfx
    {
        public const int Rate = 44100;
        static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
        static AudioSource[] pool;
        static int next;
        static GameObject host;
        public static AudioSource Pour, Fizz, Shake, Stir, Music, Ambience;
        static Task<float[]> musicTask, ambienceTask;
        static float musicTarget = 1f;

        public static void Init(GameObject h)
        {
            host = h;
            pool = new AudioSource[24];
            for (int i = 0; i < pool.Length; i++)
            {
                var go = new GameObject("sfx" + i);
                go.transform.SetParent(h.transform, false);
                pool[i] = go.AddComponent<AudioSource>();
                pool[i].playOnAwake = false;
                pool[i].spatialBlend = 0.6f;
                pool[i].minDistance = 1.2f;
                pool[i].maxDistance = 25f;
                pool[i].rolloffMode = AudioRolloffMode.Linear;
                pool[i].dopplerLevel = 0;
            }
            Make("pour", 1, () => PourLoop(2.4f, 900f, 7));
            Make("fizz", 1, () => FizzLoop(2f));
            Make("shake", 1, () => ShakeLoop());
            Make("stir", 1, () => StirLoop());
            Make("clink", 4, () => Clink(UnityEngine.Random.Range(2600f, 3900f), 0.35f, 0.5f));
            Make("glassPlace", 3, () => Mix(Thud(170f, 0.07f, 0.6f), Clink(UnityEngine.Random.Range(2200f, 3000f), 0.18f, 0.25f)));
            Make("glassSoft", 2, () => Mix(Thud(150f, 0.05f, 0.35f), Clink(UnityEngine.Random.Range(2600f, 3200f), 0.12f, 0.12f)));
            Make("glassClink", 2, () => Mix(Clink(3300f, 0.3f, 0.45f), Clink(4100f, 0.25f, 0.3f)));
            Make("bottlePlace", 3, () => Mix(Thud(120f, 0.09f, 0.7f), Clink(UnityEngine.Random.Range(800f, 1100f), 0.25f, 0.25f)));
            Make("metalPlace", 3, () => Metal(UnityEngine.Random.Range(1300f, 1800f), 0.35f, 0.4f));
            Make("capOn", 2, () => Mix(Metal(UnityEngine.Random.Range(900f, 1200f), 0.25f, 0.45f), Thud(220f, 0.04f, 0.4f)));
            Make("pick", 3, () => Whoosh(0.12f, 0.18f));
            Make("scoop", 2, () => IceBurst(0.45f, 14, 0.5f));
            Make("iceDrop", 3, () => IceBurst(0.35f, 5, 0.6f));
            Make("rinse", 1, () => Water(0.7f));
            Make("muddle", 3, () => Mix(Thud(110f, 0.09f, 0.8f), Crunch(0.08f, 0.3f)));
            Make("cash", 1, () => Cash());
            Make("bell", 1, () => Bell());
            Make("success", 1, () => Arp(new[] { 659.3f, 830.6f, 987.8f, 1318.5f }, 0.08f, 0.5f));
            Make("great", 1, () => Arp(new[] { 523.3f, 659.3f, 784f, 1046.5f, 1318.5f }, 0.07f, 0.55f));
            Make("fail", 1, () => Arp(new[] { 392f, 311.1f }, 0.16f, 0.4f));
            Make("ui", 2, () => Clink(1800f, 0.06f, 0.25f));
            Make("hover", 1, () => Clink(2600f, 0.03f, 0.06f));
            Make("splash", 2, () => Water(0.25f));
            Make("grind", 1, () => Grind(1.15f));
            Make("brew", 1, () => Brew(0.95f));
            Make("flip", 2, () => Mix(Whoosh(0.1f, 0.12f), Metal(UnityEngine.Random.Range(1700f, 2100f), 0.12f, 0.25f)));

            Pour = Loop("pour", 0.8f);
            Fizz = Loop("fizz", 0.5f);
            Shake = Loop("shake", 0.9f);
            Stir = Loop("stir", 0.55f);

            musicTask = Task.Run(() => LoungeMusic(7));
            ambienceTask = Task.Run(() => AmbienceLoop(26f));
        }

        static void Make(string name, int variants, Func<float[]> gen)
        {
            var arr = new AudioClip[variants];
            for (int i = 0; i < variants; i++)
            {
                var data = gen();
                var c = AudioClip.Create(name + i, data.Length, 1, Rate, false);
                c.SetData(data, 0);
                arr[i] = c;
            }
            clips[name] = arr;
        }

        static AudioSource Loop(string clip, float spatial)
        {
            var go = new GameObject("loop_" + clip);
            go.transform.SetParent(host.transform, false);
            var s = go.AddComponent<AudioSource>();
            s.clip = clips[clip][0];
            s.loop = true;
            s.playOnAwake = false;
            s.volume = 0;
            s.spatialBlend = spatial * 0.5f;
            s.Play();
            return s;
        }

        /// <summary>Called every frame: finishes background generation and fades music.</summary>
        public static void Tick()
        {
            if (musicTask != null && musicTask.IsCompleted)
            {
                var d = musicTask.Result;
                musicTask = null;
                var c = AudioClip.Create("music", d.Length / 2, 2, Rate, false);
                c.SetData(d, 0);
                Music = host.AddComponent<AudioSource>();
                Music.clip = c; Music.loop = true; Music.spatialBlend = 0; Music.volume = 0;
                Music.Play();
            }
            if (ambienceTask != null && ambienceTask.IsCompleted)
            {
                var d = ambienceTask.Result;
                ambienceTask = null;
                var c = AudioClip.Create("ambience", d.Length / 2, 2, Rate, false);
                c.SetData(d, 0);
                Ambience = host.AddComponent<AudioSource>();
                Ambience.clip = c; Ambience.loop = true; Ambience.spatialBlend = 0; Ambience.volume = 0;
                Ambience.Play();
            }
            AudioListener.volume = Save.Master;
            if (Music != null) Music.volume = Mathf.MoveTowards(Music.volume, Save.MusicVol * 0.42f * musicTarget, Time.unscaledDeltaTime * 0.4f);
            if (Ambience != null) Ambience.volume = Mathf.MoveTowards(Ambience.volume, Save.SfxVol * 0.55f, Time.unscaledDeltaTime * 0.3f);
        }

        public static void MusicDuck(float k) => musicTarget = k;

        public static void Play(string name, Vector3 pos, float vol = 1f, float pitch = 1f)
        {
            if (pool == null || !clips.TryGetValue(name, out var arr)) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.transform.position = pos;
            s.clip = arr[UnityEngine.Random.Range(0, arr.Length)];
            s.volume = vol * Save.SfxVol;
            s.pitch = pitch * UnityEngine.Random.Range(0.96f, 1.04f);
            s.spatialBlend = 0.6f;
            s.Play();
        }

        public static void Ui(string name = "ui", float vol = 0.6f)
        {
            if (pool == null || !clips.TryGetValue(name, out var arr)) return;
            var s = pool[next];
            next = (next + 1) % pool.Length;
            s.clip = arr[UnityEngine.Random.Range(0, arr.Length)];
            s.volume = vol * Save.SfxVol;
            s.pitch = 1f;
            s.spatialBlend = 0f;
            s.Play();
        }

        public static void SetLoop(AudioSource s, float vol, float pitch, Vector3 pos)
        {
            if (s == null) return;
            s.transform.position = pos;
            s.volume = Mathf.MoveTowards(s.volume, vol * Save.SfxVol, Time.deltaTime * 6f);
            s.pitch = Mathf.Lerp(s.pitch, pitch, Time.deltaTime * 8f);
        }

        // ================================================================== synthesis

        static float[] Buf(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];

        static float[] Mix(float[] a, float[] b)
        {
            var o = new float[Mathf.Max(a.Length, b.Length)];
            for (int i = 0; i < a.Length; i++) o[i] += a[i];
            for (int i = 0; i < b.Length; i++) o[i] += b[i];
            return o;
        }

        /// <summary>Simple state-variable band-pass.</summary>
        sealed class Svf
        {
            float low, band;
            readonly float f, q;
            public Svf(float freq, float res) { f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(freq, Rate / 6f) / Rate); q = 1f / res; }
            public float Band(float x) { low += f * band; float high = x - low - q * band; band += f * high; return band; }
            public float Low(float x) { Band(x); return low; }
            public float High(float x) { low += f * band; float high = x - low - q * band; band += f * high; return high; }
        }

        static float[] Clink(float freq, float decay, float amp)
        {
            var b = Buf(decay * 4f);
            var rnd = new System.Random((int)(freq * 13));
            float[] ratios = { 1f, 2.32f, 4.25f, 6.63f };
            float[] amps = { 1f, 0.45f, 0.25f, 0.12f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0;
                for (int k = 0; k < ratios.Length; k++)
                    v += Mathf.Sin(2f * Mathf.PI * freq * ratios[k] * t) * amps[k] * Mathf.Exp(-t / (decay / (1f + k * 0.8f)));
                float click = i < 120 ? ((float)rnd.NextDouble() * 2 - 1) * (1 - i / 120f) * 0.6f : 0;
                b[i] = (v * 0.5f + click) * amp;
            }
            return b;
        }

        static float[] Metal(float freq, float decay, float amp)
        {
            var b = Buf(decay * 4f);
            float[] ratios = { 1f, 1.47f, 2.09f, 2.56f, 3.9f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0;
                for (int k = 0; k < ratios.Length; k++)
                    v += Mathf.Sin(2f * Mathf.PI * freq * ratios[k] * t + k) * Mathf.Exp(-t / (decay / (1f + k * 0.5f))) / (1 + k * 0.6f);
                b[i] = v * amp * 0.45f * Mathf.Min(1f, i / 30f);
            }
            return b;
        }

        static float[] Thud(float freq, float decay, float amp)
        {
            var b = Buf(decay * 5f);
            var rnd = new System.Random((int)freq);
            var lp = new Svf(900f, 0.7f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float f = freq * (1f + 1.5f * Mathf.Exp(-t * 60f));
                float v = Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / decay);
                float n = lp.Low(((float)rnd.NextDouble() * 2 - 1)) * Mathf.Exp(-t * 90f);
                b[i] = (v * 0.8f + n * 1.6f) * amp;
            }
            return b;
        }

        static float[] Crunch(float len, float amp)
        {
            var b = Buf(len * 2.5f);
            var rnd = new System.Random(5);
            var bp = new Svf(2400f, 1.2f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float n = (float)rnd.NextDouble() * 2 - 1;
                float grit = rnd.NextDouble() < 0.06 ? 1f : 0.2f;
                b[i] = bp.Band(n * grit) * Mathf.Exp(-t / len) * amp * 2f;
            }
            return b;
        }

        /// <summary>Coffee grinder: motor buzz with crunching beans.</summary>
        static float[] Grind(float len)
        {
            var b = Buf(len);
            var rnd = new System.Random(41);
            var bp = new Svf(3000f, 1.1f);
            float ph = 0;
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 12f) * Mathf.Min(1f, (len - t) * 8f);
                ph += (118f + Mathf.Sin(t * 9f) * 3f) / Rate;
                float saw = (ph - Mathf.Floor(ph)) * 2f - 1f;
                float crunch = bp.Band(((float)rnd.NextDouble() * 2 - 1) * (rnd.NextDouble() < 0.08 ? 1f : 0.25f));
                b[i] = (saw * 0.22f + crunch * 0.9f) * env * 0.6f;
            }
            return b;
        }

        /// <summary>Espresso pump hum with a soft hiss.</summary>
        static float[] Brew(float len)
        {
            var b = Buf(len);
            var rnd = new System.Random(43);
            var hp = new Svf(4500f, 0.7f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 10f) * Mathf.Min(1f, (len - t) * 10f);
                float hum = Mathf.Sin(2 * Mathf.PI * 50f * t) * 0.5f + Mathf.Sin(2 * Mathf.PI * 100f * t) * 0.3f;
                b[i] = (hum * 0.25f + hp.High((float)rnd.NextDouble() * 2 - 1) * 0.3f) * env * 0.6f;
            }
            return b;
        }

        static float[] Whoosh(float len, float amp)
        {
            var b = Buf(len);
            var rnd = new System.Random(9);
            var bp = new Svf(1200f, 0.6f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / b.Length;
                float env = Mathf.Sin(Mathf.PI * t);
                b[i] = bp.Band((float)rnd.NextDouble() * 2 - 1) * env * amp;
            }
            return b;
        }

        static float[] IceBurst(float len, int clinks, float amp)
        {
            var b = Buf(len + 0.4f);
            var rnd = new System.Random(clinks * 7 + (int)(len * 100));
            for (int c = 0; c < clinks; c++)
            {
                int start = (int)(rnd.NextDouble() * len * Rate);
                var k = Clink(2200f + (float)rnd.NextDouble() * 2600f, 0.04f + (float)rnd.NextDouble() * 0.06f, 0.3f + (float)rnd.NextDouble() * 0.4f);
                for (int i = 0; i < k.Length && start + i < b.Length; i++) b[start + i] += k[i];
            }
            var cr = Crunch(len * 0.5f, 0.25f);
            for (int i = 0; i < cr.Length && i < b.Length; i++) b[i] += cr[i];
            for (int i = 0; i < b.Length; i++) b[i] *= amp;
            return b;
        }

        static float[] Water(float len)
        {
            var b = Buf(len + 0.3f);
            var rnd = new System.Random(21);
            var bp = new Svf(1100f, 0.5f);
            var bp2 = new Svf(3200f, 0.8f);
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float env = Mathf.Min(1f, t * 30f) * Mathf.Exp(-Mathf.Max(0, t - len * 0.5f) * 6f);
                float n = (float)rnd.NextDouble() * 2 - 1;
                b[i] = (bp.Band(n) * 0.9f + bp2.Band(n) * 0.35f) * env * 0.7f;
            }
            return b;
        }

        /// <summary>Liquid pouring: filtered noise with gurgles and tiny bubble pops (seamless loop).</summary>
        static float[] PourLoop(float len, float centre, int seed)
        {
            int n = (int)(len * Rate);
            var b = new float[n];
            var rnd = new System.Random(seed);
            var bp = new Svf(centre, 0.9f);
            var bp2 = new Svf(centre * 2.6f, 1.5f);
            var slow = new Svf(9f, 0.7f);
            for (int i = 0; i < n; i++)
            {
                float x = (float)rnd.NextDouble() * 2 - 1;
                float mod = 0.6f + Mathf.Abs(slow.Low((float)rnd.NextDouble() * 2 - 1)) * 6f;
                b[i] = (bp.Band(x) * 0.9f + bp2.Band(x) * 0.3f) * Mathf.Clamp(mod, 0.3f, 1.6f);
            }
            // bubble pops
            for (int k = 0; k < len * 22; k++)
            {
                int s = rnd.Next(n);
                float f = 400f + (float)rnd.NextDouble() * 900f;
                for (int i = 0; i < 900; i++)
                {
                    float t = (float)i / Rate;
                    b[(s + i) % n] += Mathf.Sin(2 * Mathf.PI * f * (1 + t * 12f) * t) * Mathf.Exp(-t * 90f) * 0.25f;
                }
            }
            b = CrossfadeLoop(b, 2000);
            Normalize(b, 0.6f);
            return b;
        }

        static float[] FizzLoop(float len)
        {
            int n = (int)(len * Rate);
            var b = new float[n];
            var rnd = new System.Random(3);
            var hp = new Svf(5000f, 0.7f);
            for (int i = 0; i < n; i++)
            {
                float x = (float)rnd.NextDouble() * 2 - 1;
                float crackle = rnd.NextDouble() < 0.004 ? 3f : 0.15f;
                b[i] = hp.High(x * crackle);
            }
            b = CrossfadeLoop(b, 1500);
            Normalize(b, 0.35f);
            return b;
        }

        static float[] ShakeLoop()
        {
            const float hit = 0.155f; // 6.45 Hz
            int hits = 8;
            int n = (int)(hit * hits * Rate);
            var b = new float[n];
            var rnd = new System.Random(77);
            for (int h = 0; h < hits; h++)
            {
                int start = (int)(h * hit * Rate);
                var bp = new Svf(h % 2 == 0 ? 1800f : 2600f, 1f);
                for (int i = 0; i < 0.12f * Rate; i++)
                {
                    float t = (float)i / Rate;
                    float x = (float)rnd.NextDouble() * 2 - 1;
                    b[(start + i) % n] += bp.Band(x) * Mathf.Exp(-t * 28f) * 1.4f;
                }
                for (int c = 0; c < 5; c++)
                {
                    var k = Clink(2000f + (float)rnd.NextDouble() * 3000f, 0.03f, 0.25f);
                    int s = start + rnd.Next(1200);
                    for (int i = 0; i < k.Length; i++) b[(s + i) % n] += k[i];
                }
                var th = Thud(140f, 0.03f, 0.35f);
                for (int i = 0; i < th.Length; i++) b[(start + i) % n] += th[i];
            }
            Normalize(b, 0.7f);
            return b;
        }

        static float[] StirLoop()
        {
            int n = (int)(1.6f * Rate);
            var b = new float[n];
            var rnd = new System.Random(31);
            for (int c = 0; c < 11; c++)
            {
                var k = Clink(2600f + (float)rnd.NextDouble() * 2200f, 0.05f, 0.15f + (float)rnd.NextDouble() * 0.15f);
                int s = (int)(c / 11f * n) + rnd.Next(1500);
                for (int i = 0; i < k.Length; i++) b[(s + i) % n] += k[i];
            }
            var bp = new Svf(700f, 0.5f);
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / n;
                b[i] += bp.Band((float)rnd.NextDouble() * 2 - 1) * 0.12f * (0.6f + 0.4f * Mathf.Sin(t * Mathf.PI * 2 * 4));
            }
            Normalize(b, 0.5f);
            return b;
        }

        static float[] Cash()
        {
            var b = Buf(1.1f);
            var rnd = new System.Random(4);
            for (int c = 0; c < 9; c++)
            {
                var k = Metal(2400f + (float)rnd.NextDouble() * 2500f, 0.08f, 0.35f);
                int s = (int)(c * 0.035f * Rate) + rnd.Next(400);
                for (int i = 0; i < k.Length && s + i < b.Length; i++) b[s + i] += k[i];
            }
            var bell = Bell();
            int off = (int)(0.18f * Rate);
            for (int i = 0; i < bell.Length && off + i < b.Length; i++) b[off + i] += bell[i] * 0.6f;
            Normalize(b, 0.7f);
            return b;
        }

        static float[] Bell()
        {
            var b = Buf(1.6f);
            float f = 1760f;
            float[] r = { 1f, 2.0f, 2.76f, 5.4f };
            float[] a = { 1f, 0.4f, 0.35f, 0.15f };
            for (int i = 0; i < b.Length; i++)
            {
                float t = (float)i / Rate;
                float v = 0;
                for (int k = 0; k < r.Length; k++) v += Mathf.Sin(2 * Mathf.PI * f * r[k] * t) * a[k] * Mathf.Exp(-t * (2.2f + k * 1.5f));
                b[i] = v * 0.35f * Mathf.Min(1, i / 40f);
            }
            return b;
        }

        static float[] Arp(float[] notes, float step, float amp)
        {
            var b = Buf(step * notes.Length + 0.8f);
            for (int n = 0; n < notes.Length; n++)
            {
                int s = (int)(n * step * Rate);
                for (int i = 0; s + i < b.Length; i++)
                {
                    float t = (float)i / Rate;
                    float env = Mathf.Exp(-t * 5f) * Mathf.Min(1, i / 60f);
                    float m = Mathf.Sin(2 * Mathf.PI * notes[n] * t) * 1.2f * Mathf.Exp(-t * 8f);
                    b[s + i] += Mathf.Sin(2 * Mathf.PI * notes[n] * t + m) * env * amp * 0.4f;
                }
            }
            return b;
        }

        /// <summary>Blends the tail into the head and cuts the tail off, so the clip loops without a click.</summary>
        static float[] CrossfadeLoop(float[] b, int fade, int channels = 1)
        {
            int frames = b.Length / channels;
            for (int i = 0; i < fade; i++)
            {
                float t = (float)i / fade;
                for (int c = 0; c < channels; c++)
                    b[i * channels + c] = b[i * channels + c] * t + b[(frames - fade + i) * channels + c] * (1 - t);
            }
            var o = new float[(frames - fade) * channels];
            Array.Copy(b, o, o.Length);
            return o;
        }

        static void Normalize(float[] b, float peak)
        {
            float m = 0.0001f;
            for (int i = 0; i < b.Length; i++) m = Mathf.Max(m, Mathf.Abs(b[i]));
            float k = peak / m;
            for (int i = 0; i < b.Length; i++) b[i] *= k;
        }

        // ================================================================== music and ambience (background thread, no Unity API)

        static float Midi(int n) => 440f * (float)Math.Pow(2, (n - 69) / 12.0);

        /// <summary>Lounge jazz loop: electric piano, upright-ish bass, brushed drums, vibes. Stereo interleaved.</summary>
        static float[] LoungeMusic(int seed)
        {
            const float bpm = 82f;
            float beat = 60f / bpm;
            int bars = 16;
            int n = (int)(bars * 4 * beat * Rate);
            var L = new float[n];
            var R = new float[n];
            var rnd = new System.Random(seed);
            int[][] chords =
            {
                new[] { 50, 53, 57, 60, 64 }, new[] { 43, 53, 59, 64, 69 }, new[] { 48, 52, 55, 59, 62 }, new[] { 45, 55, 59, 60, 64 },
                new[] { 50, 53, 57, 60, 64 }, new[] { 43, 53, 59, 64, 69 }, new[] { 40, 55, 59, 62, 67 }, new[] { 45, 55, 61, 65, 69 },
                new[] { 41, 57, 60, 64, 67 }, new[] { 41, 56, 60, 63, 67 }, new[] { 40, 55, 59, 62, 66 }, new[] { 45, 55, 61, 64, 69 },
                new[] { 50, 53, 57, 60, 64 }, new[] { 43, 53, 59, 64, 69 }, new[] { 48, 52, 55, 59, 62 }, new[] { 48, 52, 55, 59, 62 },
            };
            float Swing(float b8) => (b8 % 1f) > 0.01f ? 0.64f : 0f; // swung offbeat position
            for (int bar = 0; bar < bars; bar++)
            {
                var ch = chords[bar];
                float barStart = bar * 4 * beat;
                // electric piano comping: beat 1 and the swung "and" of 2
                float[] hits = { 0f, 1f + 0.64f, 3f + 0.64f };
                for (int h = 0; h < hits.Length; h++)
                {
                    if (h == 2 && rnd.NextDouble() < 0.5) continue;
                    float t0 = barStart + hits[h] * beat;
                    float vel = h == 0 ? 0.9f : 0.65f;
                    float dur = h == 0 ? beat * 1.5f : beat * 0.9f;
                    for (int k = 1; k < ch.Length; k++) Rhodes(L, R, t0 + k * 0.006f, Midi(ch[k]), vel * 0.17f, dur, (k - 2.5f) * 0.25f);
                }
                // bass: root, fifth, approach note
                int root = ch[0] - 12 + 12;
                int nextRoot = chords[(bar + 1) % bars][0];
                Bass(L, R, barStart, Midi(root - 12), beat * 1.8f, 0.5f);
                Bass(L, R, barStart + 2 * beat, Midi(root - 12 + 7), beat * 1.2f, 0.4f);
                Bass(L, R, barStart + 3 * beat + 0.64f * beat, Midi(nextRoot - 12 + (rnd.NextDouble() < 0.5 ? 1 : -1)), beat * 0.35f, 0.35f);
                // drums
                for (int bt = 0; bt < 4; bt++)
                {
                    float tb = barStart + bt * beat;
                    if (bt == 0 || (bt == 2 && rnd.NextDouble() < 0.7)) Kick(L, R, tb, 0.55f);
                    if (bt == 2 && rnd.NextDouble() < 0.3) Kick(L, R, tb + 0.64f * beat, 0.35f);
                    if (bt == 1 || bt == 3) Brush(L, R, tb, 0.28f);
                    Hat(L, R, tb, 0.12f + (float)rnd.NextDouble() * 0.04f, rnd.Next());
                    Hat(L, R, tb + 0.64f * beat, 0.08f + (float)rnd.NextDouble() * 0.04f, rnd.Next());
                }
                // vibes: sparse chord-tone melody
                for (int s = 0; s < 4; s++)
                {
                    if (rnd.NextDouble() < 0.45) continue;
                    int note = ch[1 + rnd.Next(ch.Length - 1)] + 12;
                    float tv = barStart + (s + (rnd.NextDouble() < 0.4 ? 0.64f : 0f)) * beat;
                    Vibes(L, R, tv, Midi(note), 0.09f, beat * 1.6f, 0.35f);
                }
            }
            // soft vinyl texture
            var crack = new System.Random(seed + 1);
            for (int i = 0; i < n; i++)
            {
                if (crack.NextDouble() < 0.00025) { float c = ((float)crack.NextDouble() - 0.5f) * 0.05f; L[i] += c; R[i] += c * 0.8f; }
            }
            // gentle bus compression + normalise
            float peak = 0.001f;
            for (int i = 0; i < n; i++) { L[i] = (float)Math.Tanh(L[i] * 1.6f); R[i] = (float)Math.Tanh(R[i] * 1.6f); peak = Math.Max(peak, Math.Max(Math.Abs(L[i]), Math.Abs(R[i]))); }
            var o = new float[n * 2];
            float g = 0.85f / peak;
            for (int i = 0; i < n; i++) { o[i * 2] = L[i] * g; o[i * 2 + 1] = R[i] * g; }
            _ = Swing(0);
            return o;
        }

        static void Put(float[] L, float[] R, int i, float v, float pan)
        {
            int n = L.Length;
            int j = ((i % n) + n) % n;
            L[j] += v * (1 - pan) * 0.5f * 1.4f;
            R[j] += v * (1 + pan) * 0.5f * 1.4f;
        }

        static void Rhodes(float[] L, float[] R, float t0, float f, float amp, float dur, float pan)
        {
            int s = (int)(t0 * Rate);
            int len = (int)((dur + 1.4f) * Rate);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * 1.6f) * (t < dur ? 1f : (float)Math.Exp(-(t - dur) * 6f)) * Math.Min(1f, i / 80f);
                float idx = 1.8f * (float)Math.Exp(-t * 6f) + 0.25f;
                float mod = (float)Math.Sin(2 * Math.PI * f * t) * idx;
                float v = (float)Math.Sin(2 * Math.PI * f * t + mod) + 0.15f * (float)Math.Sin(2 * Math.PI * f * 2 * t);
                float trem = 1f + 0.12f * (float)Math.Sin(2 * Math.PI * 4.5 * t);
                Put(L, R, s + i, v * env * amp, pan * trem * 0.6f);
            }
        }

        static void Bass(float[] L, float[] R, float t0, float f, float dur, float amp)
        {
            int s = (int)(t0 * Rate);
            int len = (int)((dur + 0.3f) * Rate);
            float ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * 2.2f) * (t < dur ? 1f : (float)Math.Exp(-(t - dur) * 14f)) * Math.Min(1f, i / 60f);
                ph += f / Rate;
                float v = (float)Math.Sin(2 * Math.PI * ph) + 0.25f * (float)Math.Sin(4 * Math.PI * ph) + 0.08f * (float)Math.Sin(6 * Math.PI * ph);
                float pluck = (float)Math.Exp(-t * 30f) * 0.4f * (float)Math.Sin(2 * Math.PI * ph * 3);
                Put(L, R, s + i, (v + pluck) * env * amp * 0.55f, 0f);
            }
        }

        static void Kick(float[] L, float[] R, float t0, float amp)
        {
            int s = (int)(t0 * Rate);
            int len = (int)(0.35f * Rate);
            float ph = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float f = 48f + 70f * (float)Math.Exp(-t * 35f);
                ph += f / Rate;
                Put(L, R, s + i, (float)Math.Sin(2 * Math.PI * ph) * (float)Math.Exp(-t * 9f) * amp * 0.7f, 0f);
            }
        }

        static void Brush(float[] L, float[] R, float t0, float amp)
        {
            int s = (int)(t0 * Rate);
            int len = (int)(0.25f * Rate);
            var rnd = new System.Random(s);
            float lp = 0, prev = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float x = (float)rnd.NextDouble() * 2 - 1;
                lp += (x - lp) * 0.35f;
                float hp = lp - prev; prev = lp;
                float env = (float)Math.Exp(-t * 14f) * Math.Min(1f, i / 200f);
                Put(L, R, s + i, (lp * 0.6f + hp * 0.8f) * env * amp, 0.15f);
            }
        }

        static void Hat(float[] L, float[] R, float t0, float amp, int seed)
        {
            int s = (int)(t0 * Rate);
            int len = (int)(0.06f * Rate);
            var rnd = new System.Random(seed);
            float prev = 0;
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float x = (float)rnd.NextDouble() * 2 - 1;
                float hp = x - prev; prev = x;
                Put(L, R, s + i, hp * (float)Math.Exp(-t * 70f) * amp * 0.5f, -0.3f);
            }
        }

        static void Vibes(float[] L, float[] R, float t0, float f, float amp, float dur, float pan)
        {
            int s = (int)(t0 * Rate);
            int len = (int)((dur + 1f) * Rate);
            for (int i = 0; i < len; i++)
            {
                float t = (float)i / Rate;
                float env = (float)Math.Exp(-t * 2.5f) * Math.Min(1f, i / 30f);
                float trem = 1f + 0.35f * (float)Math.Sin(2 * Math.PI * 5.5 * t);
                float v = (float)Math.Sin(2 * Math.PI * f * t) + 0.2f * (float)Math.Sin(2 * Math.PI * f * 4 * t) * (float)Math.Exp(-t * 12f);
                Put(L, R, s + i, v * env * amp * trem, pan);
            }
        }

        /// <summary>Rooftop ambience: distant city rumble, breeze and a soft crowd murmur. Stereo interleaved.</summary>
        static float[] AmbienceLoop(float len)
        {
            int n = (int)(len * Rate);
            var o = new float[n * 2];
            var rnd = new System.Random(12);
            float b1 = 0, b2 = 0, w1 = 0, w2 = 0;
            float m1 = 0, m2 = 0, mm = 0;
            float bpL = 0, bpL2 = 0, bpR = 0, bpR2 = 0;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                float xa = (float)rnd.NextDouble() * 2 - 1, xb = (float)rnd.NextDouble() * 2 - 1;
                // brown noise rumble
                b1 = (b1 + xa * 0.02f) * 0.998f; b2 = (b2 + xb * 0.02f) * 0.998f;
                // breeze: low-passed noise with slow swell
                w1 += (xa - w1) * 0.02f; w2 += (xb - w2) * 0.02f;
                float swell = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * t / len * 3 + 1) * (float)Math.Sin(2 * Math.PI * t / len * 2);
                // murmur: band-limited noise with syllable-like modulation
                float xm = (float)rnd.NextDouble() * 2 - 1;
                m1 += (xm - m1) * 0.08f; m2 += (m1 - m2) * 0.08f;
                float band = m1 - m2;
                mm += (((float)rnd.NextDouble()) - mm) * 0.0004f;
                float syll = 0.5f + 0.5f * (float)Math.Sin(2 * Math.PI * (3.1f + mm * 2f) * t + Math.Sin(t * 1.7f) * 2);
                bpL += (band - bpL) * 0.5f; bpL2 += (bpL - bpL2) * 0.5f;
                bpR += (band * 0.9f - bpR) * 0.45f; bpR2 += (bpR - bpR2) * 0.45f;
                float murmur = (bpL2 * syll) * 0.9f;
                // seamless loop fade
                const float edge = 1f;
                o[i * 2] = (b1 * 1.2f + w1 * 0.35f * swell + murmur) * edge;
                o[i * 2 + 1] = (b2 * 1.2f + w2 * 0.35f * swell + bpR2 * syll * 0.9f) * edge;
            }
            o = CrossfadeLoop(o, Rate * 2, 2);
            float peak = 0.001f;
            for (int i = 0; i < o.Length; i++) peak = Math.Max(peak, Math.Abs(o[i]));
            for (int i = 0; i < o.Length; i++) o[i] *= 0.5f / peak;
            return o;
        }
    }
}
