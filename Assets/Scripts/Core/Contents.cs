using System.Collections.Generic;
using UnityEngine;

namespace Bartender
{
    public enum Garnish { LimeWedge, LemonTwist, OrangePeel, Cherry, Mint, Olive }

    public enum GlassType { Highball, Rocks, Coupe, Martini, Wine, Mug }

    /// <summary>What is inside a container: millilitres per ingredient plus how it was prepared.</summary>
    public sealed class Contents
    {
        public readonly Dictionary<Ingredient, float> Ml = new Dictionary<Ingredient, float>();
        public float Shake;      // 0..1+ how long it was shaken
        public float Stir;       // 0..1+ how long it was stirred
        public float Chill;      // 0..1 how cold it is (shaken/stirred with ice)
        public bool Unstrained;  // poured from a shaker/mixing glass without a strainer
        public int Version;      // bumps on every change, used for redraws

        public float Total
        {
            get { float t = 0; foreach (var kv in Ml) t += kv.Value; return t; }
        }

        /// <summary>Total without melted ice water.</summary>
        public float Spirit
        {
            get { float t = 0; foreach (var kv in Ml) if (kv.Key.Kind != Kind.Water) t += kv.Value; return t; }
        }

        public float Get(Ingredient i) => i != null && Ml.TryGetValue(i, out var v) ? v : 0f;

        public void Add(Ingredient ing, float ml)
        {
            if (ml <= 0f) return;
            float before = Total;
            Ml[ing] = Get(ing) + ml;
            // a fresh pour into a prepared drink dilutes its "prepared" state
            float after = before + ml;
            Shake = Shake * before / after;
            Stir = Stir * before / after;
            Chill = Chill * before / after;
            Version++;
        }

        /// <summary>Takes ml out (proportionally) and returns it as a new Contents.</summary>
        public Contents Take(float ml)
        {
            var part = new Contents();
            float total = Total;
            if (total <= 0.0001f || ml <= 0f) return part;
            float f = Mathf.Clamp01(ml / total);
            var keys = new List<Ingredient>(Ml.Keys);
            foreach (var k in keys)
            {
                float v = Ml[k] * f;
                part.Ml[k] = v;
                Ml[k] -= v;
                if (Ml[k] < 0.01f) Ml.Remove(k);
            }
            part.Shake = Shake; part.Stir = Stir; part.Chill = Chill; part.Unstrained = Unstrained;
            Version++;
            return part;
        }

        /// <summary>Mixes another portion into this one (state is weighted by volume).</summary>
        public void Merge(Contents src, bool unstrained)
        {
            float a = Total, b = src.Total;
            if (b <= 0f) return;
            float t = a + b;
            Shake = (Shake * a + src.Shake * b) / t;
            Stir = (Stir * a + src.Stir * b) / t;
            Chill = (Chill * a + src.Chill * b) / t;
            Unstrained |= unstrained || src.Unstrained;
            foreach (var kv in src.Ml) Ml[kv.Key] = Get(kv.Key) + kv.Value;
            Version++;
        }

        public void Clear()
        {
            Ml.Clear();
            Shake = Stir = Chill = 0f;
            Unstrained = false;
            Version++;
        }

        public void Dilute(float ml)
        {
            if (ml <= 0f) return;
            Ml[Ingredients.Water] = Get(Ingredients.Water) + ml;
            Version++;
        }

        public bool IsFizzy
        {
            get
            {
                float fizzy = 0, total = 0;
                foreach (var kv in Ml) { total += kv.Value; if (kv.Key.Fizzy) fizzy += kv.Value; }
                return total > 0 && fizzy / total > 0.25f && Shake < 0.3f;
            }
        }

        /// <summary>Colour of the mix. Clear ingredients dilute the colour instead of tinting it.</summary>
        public Color MixColor()
        {
            float total = 0, wsum = 0;
            Color c = Color.black;
            float alpha = 0;
            foreach (var kv in Ml)
            {
                float ml = kv.Value;
                total += ml;
                float w = ml * (0.02f + kv.Key.Alpha);
                c += kv.Key.Color * w;
                wsum += w;
                alpha += kv.Key.Alpha * ml;
            }
            if (total <= 0f) return new Color(1, 1, 1, 0);
            c /= Mathf.Max(wsum, 0.0001f);
            alpha /= total;
            // shaking makes drinks cloudy and lighter
            float cloud = Mathf.Clamp01(Shake) * 0.22f;
            c = Color.Lerp(c, Color.white, cloud * 0.35f);
            alpha = Mathf.Clamp01(alpha + cloud);
            c.a = Mathf.Lerp(0.05f, 0.96f, alpha);
            return c;
        }

        public void CopyFrom(Contents o)
        {
            Ml.Clear();
            foreach (var kv in o.Ml) Ml[kv.Key] = kv.Value;
            Shake = o.Shake; Stir = o.Stir; Chill = o.Chill; Unstrained = o.Unstrained;
            Version++;
        }
    }
}
