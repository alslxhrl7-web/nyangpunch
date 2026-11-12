using System.Collections.Generic;
using UnityEngine;

namespace NyangPunch
{
    /// <summary>Sound effects: recorded clips from Resources/Sfx, or synth beeps recreating the prototype's WebAudio sounds.</summary>
    public class Sfx
    {
        struct Note { public float freq, dur, vol, slide, delay; public string wave; }

        const int Rate = 44100;
        readonly AudioSource src;
        readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
        public bool Muted;

        // every key the game plays (rules and view)
        static readonly string[] Keys = { "click", "pop", "drop", "merge", "level", "step", "punch", "bump", "hiss", "throw", "hurt", "churu", "boss",
                                          "clear", "bump_pot", "squeak", "kill_fly", "kill_cucumber", "kill_boss" };

        public Sfx(GameObject host)
        {
            src = host.AddComponent<AudioSource>(); src.playOnAwake = false;
            for (int i = 0; i < variedSrc.Length; i++) { variedSrc[i] = host.AddComponent<AudioSource>(); variedSrc[i].playOnAwake = false; }
            // Load and decode every clip now. On the web a clip is decoded asynchronously (decodeAudioData) the first
            // time its data is loaded, so loading it on the first Play could make that first punch or squeak late or silent.
            foreach (var k in Keys)
            {
                var c = Get(k);
                if (c != null && recorded.Contains(k) && c.loadState == AudioDataLoadState.Unloaded) c.LoadAudioData();
            }
        }

        static Note N(float f, float d, string w, float v, float slide = 0, float delay = 0) => new Note { freq = f, dur = d, wave = w, vol = v, slide = slide, delay = delay };

        static Note[] Recipe(string k)
        {
            switch (k)
            {
                case "click": return new[] { N(600, 0.04f, "square", 0.025f) };
                case "pop": return new[] { N(800, 0.07f, "triangle", 0.035f, 1.8f) };
                case "drop": return new[] { N(260, 0.1f, "triangle", 0.04f, 0.6f) };
                case "merge": return new[] { N(523, 0.1f, "triangle", 0.045f, 0, 0), N(784, 0.1f, "triangle", 0.045f, 0, 0.06f), N(1047, 0.1f, "triangle", 0.045f, 0, 0.12f) };
                case "level": return new[] { N(523, 0.12f, "triangle", 0.045f, 0, 0), N(659, 0.12f, "triangle", 0.045f, 0, 0.07f), N(784, 0.12f, "triangle", 0.045f, 0, 0.14f), N(1047, 0.12f, "triangle", 0.045f, 0, 0.21f) };
                case "step": return new[] { N(330, 0.04f, "sine", 0.025f, 1.3f) };
                case "punch": return new[] { N(180, 0.12f, "sawtooth", 0.04f, 0.4f) };
                case "bump": return new[] { N(120, 0.1f, "square", 0.04f, 0.5f) };
                case "hiss": return new[] { N(900, 0.25f, "sawtooth", 0.025f, 0.3f) };
                case "throw": return new[] { N(330, 0.08f, "sine", 0.03f, 1.8f) };
                case "hurt": return new[] { N(220, 0.18f, "square", 0.05f, 0.5f) };
                case "churu": return new[] { N(1300, 0.08f, "triangle", 0.04f, 1.3f), N(1700, 0.1f, "triangle", 0.035f, 0, 0.07f) };
                case "boss": return new[] { N(110, 0.6f, "sawtooth", 0.05f, 0.7f) };
            }
            return null;
        }

        AudioClip Build(string key)
        {
            var notes = Recipe(key); if (notes == null) return null;
            float total = 0; foreach (var n in notes) total = Mathf.Max(total, n.delay + n.dur + 0.02f);
            var data = new float[Mathf.CeilToInt(total * Rate)];
            foreach (var n in notes)
            {
                int start = Mathf.RoundToInt(n.delay * Rate), len = Mathf.RoundToInt(n.dur * Rate);
                double phase = 0;
                for (int i = 0; i < len && start + i < data.Length; i++)
                {
                    float t = (float)i / len;
                    float f = n.slide > 0 ? n.freq * Mathf.Pow(Mathf.Max(40f / n.freq, n.slide), t) : n.freq;
                    phase += f / Rate;
                    float p = (float)(phase - System.Math.Floor(phase)), s;
                    switch (n.wave)
                    {
                        case "square": s = p < 0.5f ? 1 : -1; break;
                        case "triangle": s = 4 * Mathf.Abs(p - 0.5f) - 1; break;
                        case "sawtooth": s = 2 * p - 1; break;
                        default: s = Mathf.Sin(p * Mathf.PI * 2); break;
                    }
                    float g = n.vol * Mathf.Pow(0.0001f / n.vol, t);   // exponential decay like the WebAudio ramp
                    data[start + i] += s * g * 6f;
                }
            }
            var clip = AudioClip.Create("sfx_" + key, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Recorded one-shots live in Resources/Sfx/<key>.wav (cut from Varco Sound results by Tools/sound/cut.html).
        // A key without a recording uses the synth recipe; a key with neither falls back to a related key.
        static readonly Dictionary<string, string> Fallback = new Dictionary<string, string> { { "clear", "level" }, { "bump_pot", "bump" }, { "squeak", "pop" }, { "kill_fly", "pop" }, { "kill_cucumber", "pop" }, { "kill_boss", "boss" } };
        static readonly HashSet<string> Varied = new HashSet<string> { "punch", "squeak", "pop", "kill_fly", "kill_cucumber", "step", "bump", "bump_pot", "drop" };   // repeated often: vary the pitch a little
        const float ClipVolume = 0.55f;   // recordings are peak-normalised, the synth beeps are quiet
        readonly HashSet<string> recorded = new HashSet<string>();
        // Pitch-varied one-shots rotate over a few sources: a source's pitch also applies to the one-shots it is
        // still playing, so with a single source each new sound would re-pitch the ones still ringing.
        readonly AudioSource[] variedSrc = new AudioSource[3];
        int nextVaried;

        AudioClip Get(string key)
        {
            if (cache.TryGetValue(key, out var c)) return c;
            c = Resources.Load<AudioClip>("Sfx/" + key);
            if (c != null) recorded.Add(key);
            else
            {
                c = Build(key);
                if (c == null && Fallback.TryGetValue(key, out var fb)) { c = Get(fb); if (recorded.Contains(fb)) recorded.Add(key); }
            }
            cache[key] = c;
            return c;
        }

        public bool IsRecorded(string key) { Get(key); return recorded.Contains(key); }
        public bool Has(string key) => Get(key) != null;

        public void Play(string key)
        {
            if (Muted) return;
            var c = Get(key);
            if (c == null) return;
            if (!recorded.Contains(key)) { src.PlayOneShot(c, key == "pop" ? Random.Range(0.85f, 1f) : 1f); return; }
            if (Varied.Contains(key))
            {
                var vs = variedSrc[nextVaried]; nextVaried = (nextVaried + 1) % variedSrc.Length;
                vs.pitch = Random.Range(0.94f, 1.06f);
                vs.PlayOneShot(c, ClipVolume * Random.Range(0.9f, 1f) * (key == "step" ? 0.55f : 1f));   // steps play every move, keep them soft
            }
            else src.PlayOneShot(c, ClipVolume);
        }
    }
}
