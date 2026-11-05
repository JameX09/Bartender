using UnityEngine;

namespace Bartender
{
    /// <summary>Generated surface: colour (alpha = smoothness), optional height for a normal map and emission.</summary>
    public sealed class GenTex
    {
        public int W, H;
        public Color[] Albedo;
        public float[] Height;
        public Color[] Emission;
        public float NormalStrength = 2f;
    }

    /// <summary>Procedural materials. They are baked to PNG files by the editor build script.</summary>
    public static class TexGen
    {
        static float S(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3f - 2f * t); }

        static GenTex New(int w, int h, bool height = true)
        {
            return new GenTex { W = w, H = h, Albedo = new Color[w * h], Height = height ? new float[w * h] : null };
        }

        // ---------------------------------------------------------------- stone

        /// <summary>White marble with grey and gold veins.</summary>
        public static GenTex Marble(int n)
        {
            Noise.Seed(11);
            var t = New(n, n);
            t.NormalStrength = 1.2f;
            var baseC = new Color(0.93f, 0.92f, 0.9f);
            var cloud = new Color(0.8f, 0.8f, 0.81f);
            var vein = new Color(0.5f, 0.48f, 0.47f);
            var gold = new Color(0.78f, 0.6f, 0.32f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    // turbulence (sum of absolute noise) bends the veins gently along one direction
                    float turb = 0, amp = 0.5f;
                    int f = 2;
                    for (int o = 0; o < 6; o++) { turb += Mathf.Abs(Noise.Perlin(u * f, v * f, f, f)) * amp; amp *= 0.5f; f *= 2; }
                    float c = Noise.Fbm(u + 0.2f, v + 0.7f, 2, 2, 5) * 0.5f + 0.5f;
                    float s1 = Mathf.Sin(Mathf.PI * 2f * (1f * u + 1f * v) + turb * 5.5f);
                    float s2 = Mathf.Sin(Mathf.PI * 2f * (1f * u + 2f * v) + turb * 7f + 1.7f);
                    float thick = Mathf.Pow(1f - Mathf.Abs(s1), 14f);
                    float soft = Mathf.Pow(1f - Mathf.Abs(s1), 3f) * 0.18f;
                    float thin = Mathf.Pow(1f - Mathf.Abs(s2), 48f) * 0.45f;
                    // crack network: ridged noise
                    float ridge = 1f - Mathf.Abs(Noise.Fbm(u + 0.41f, v + 0.13f, 4, 4, 4));
                    float cracks = Mathf.Pow(ridge, 60f) * 0.3f;
                    float ridgeG = 1f - Mathf.Abs(Noise.Fbm(u + 0.77f, v + 0.29f, 3, 3, 4));
                    float g = Mathf.Pow(ridgeG, 140f);
                    Color col = Color.Lerp(baseC, cloud, S(0.4f, 0.95f, c) * 0.5f);
                    col = Color.Lerp(col, cloud * 0.92f, soft);
                    col = Color.Lerp(col, vein, Mathf.Clamp01(thick * 0.6f + thin + cracks));
                    col = Color.Lerp(col, gold, Mathf.Clamp01(g * 0.9f));
                    col.a = 0.92f - (thick + thin) * 0.1f;
                    int i = y * n + x;
                    t.Albedo[i] = col;
                    t.Height[i] = -(thick * 0.25f + thin * 0.15f + cracks * 0.1f) + c * 0.04f;
                }
            return t;
        }

        /// <summary>Dark terrazzo with marble and brass chips.</summary>
        public static GenTex Terrazzo(int n)
        {
            Noise.Seed(23);
            var t = New(n, n);
            t.NormalStrength = 0.8f;
            var baseC = new Color(0.075f, 0.075f, 0.085f);
            Color[] chips = { new Color(0.62f, 0.61f, 0.58f), new Color(0.34f, 0.34f, 0.36f), new Color(0.5f, 0.4f, 0.26f), new Color(0.22f, 0.21f, 0.21f), new Color(0.42f, 0.42f, 0.44f) };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float grain = Noise.Fbm(u, v, 64, 64, 2) * 0.03f;
                    Color col = baseC + new Color(grain, grain, grain);
                    float h = 0;
                    Noise.Cell(u, v, 72, out float f1, out float f2, out int id);
                    float r = 0.14f + (id & 7) * 0.025f;
                    if ((id & 3) == 0 && f1 < r)
                    {
                        col = chips[id % chips.Length] * (0.85f + ((id >> 3) & 7) * 0.04f);
                        h = 0.1f;
                    }
                    Noise.Cell(u + 0.5f, v + 0.3f, 160, out float g1, out _, out int gid);
                    if ((gid & 3) == 0 && g1 < 0.2f) { col = chips[(gid >> 2) % chips.Length] * 0.9f; h = 0.06f; }
                    col.a = 0.72f + grain;
                    t.Albedo[y * n + x] = col;
                    t.Height[y * n + x] = h;
                }
            return t;
        }

        /// <summary>Smooth grey concrete with pores.</summary>
        public static GenTex Concrete(int n)
        {
            Noise.Seed(31);
            var t = New(n, n);
            t.NormalStrength = 1.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float b = Noise.Fbm(u, v, 4, 4, 6) * 0.5f + 0.5f;
                    float f = Noise.Fbm(u, v, 64, 64, 3);
                    Noise.Cell(u, v, 90, out float f1, out _, out int id);
                    float pore = (id & 7) == 0 ? S(0.25f, 0.05f, f1) : 0f;
                    float g = 0.42f + b * 0.12f + f * 0.03f - pore * 0.15f;
                    var col = new Color(g, g * 0.99f, g * 0.97f, 0.22f + f * 0.05f);
                    t.Albedo[y * n + x] = col;
                    t.Height[y * n + x] = f * 0.08f - pore * 0.3f;
                }
            return t;
        }

        /// <summary>Backlit amber onyx: bright translucent bands (used as emission).</summary>
        public static GenTex Onyx(int n)
        {
            Noise.Seed(47);
            var t = New(n, n, false);
            t.Emission = new Color[n * n];
            var deep = new Color(0.35f, 0.12f, 0.03f);
            var amber = new Color(0.95f, 0.5f, 0.15f);
            var honey = new Color(1f, 0.82f, 0.5f);
            var cream = new Color(1f, 0.95f, 0.85f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float w = Noise.Fbm(u, v, 2, 2, 6);
                    float w2 = Noise.Fbm(u + w * 0.3f, v, 3, 3, 5);
                    float band = Mathf.Sin(Mathf.PI * 2f * (1f * u + 2f * v) + w * 6f + w2 * 4f) * 0.5f + 0.5f;
                    float fine = Mathf.Sin(Mathf.PI * 2f * (4f * u + 7f * v) + w2 * 10f) * 0.5f + 0.5f;
                    float k = Mathf.Clamp01(band * 0.75f + fine * 0.25f);
                    Color c = k < 0.35f ? Color.Lerp(deep, amber, k / 0.35f)
                            : k < 0.75f ? Color.Lerp(amber, honey, (k - 0.35f) / 0.4f)
                            : Color.Lerp(honey, cream, (k - 0.75f) / 0.25f);
                    float vein = Mathf.Pow(1f - Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * (3f * u - 1f * v) + w * 9f)), 30f);
                    c = Color.Lerp(c, cream, vein * 0.6f);
                    c.a = 0.85f;
                    t.Albedo[y * n + x] = c * 0.4f + new Color(0, 0, 0, 0.85f);
                    t.Emission[y * n + x] = c;
                }
            return t;
        }

        // ---------------------------------------------------------------- wood

        /// <summary>Lacquered dark walnut (grain runs along V).</summary>
        public static GenTex Walnut(int n)
        {
            Noise.Seed(5);
            var t = New(n, n);
            t.NormalStrength = 1.6f;
            var dark = new Color(0.1f, 0.055f, 0.032f);
            var mid = new Color(0.24f, 0.13f, 0.07f);
            var light = new Color(0.36f, 0.2f, 0.1f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float warp = Noise.Fbm(u, v, 4, 1, 5) * 1.6f;
                    float rings = Mathf.Sin(Mathf.PI * 2f * (6f * u + warp)) * 0.5f + 0.5f;
                    float fibre = Noise.Perlin(u * 256f, v * 6f, 256, 6) * 0.5f + 0.5f;
                    float fibre2 = Noise.Perlin(u * 512f, v * 3f, 512 > 256 ? 256 : 512, 3);
                    float k = Mathf.Clamp01(rings * 0.6f + fibre * 0.4f);
                    Color c = k < 0.5f ? Color.Lerp(dark, mid, k * 2f) : Color.Lerp(mid, light, (k - 0.5f) * 2f);
                    c *= 0.92f + fibre2 * 0.06f;
                    c.a = 0.62f + fibre * 0.08f;
                    t.Albedo[y * n + x] = c;
                    t.Height[y * n + x] = fibre * 0.12f + rings * 0.04f;
                }
            return t;
        }

        /// <summary>Weathered teak deck planks running along U. Texture covers 2 x 2 metres.</summary>
        public static GenTex Deck(int n)
        {
            Noise.Seed(7);
            var t = New(n, n);
            t.NormalStrength = 2.5f;
            const int rows = 14;
            var a = new Color(0.36f, 0.22f, 0.12f);
            var b = new Color(0.5f, 0.33f, 0.19f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float rv = v * rows;
                    int row = Mathf.FloorToInt(rv);
                    float fy = rv - row;
                    float offset = Noise.Rand01(row, 3);
                    float pu = u + offset;
                    pu -= Mathf.Floor(pu);
                    int seg = pu < 0.5f ? 0 : 1;
                    float fx = pu * 2f - seg;
                    int plank = row * 2 + seg;
                    float tint = Noise.Rand01(plank, 9);
                    float grain = Noise.Perlin(u * 8f, v * 220f, 8, 220) * 0.5f + 0.5f;
                    float warp = Noise.Fbm(u, v, 2, 14, 4);
                    float rings = Mathf.Sin((v * rows * 9f + warp * 3f + tint * 10f) * Mathf.PI * 2f) * 0.5f + 0.5f;
                    Color c = Color.Lerp(a, b, tint * 0.6f + rings * 0.25f + grain * 0.15f);
                    float gray = Noise.Fbm(u, v, 3, 3, 4) * 0.5f + 0.5f;
                    c = Color.Lerp(c, new Color(0.42f, 0.38f, 0.34f), gray * 0.25f);
                    float seamY = Mathf.Min(fy, 1f - fy) * n / rows;   // pixels to the long edge
                    float seamX = Mathf.Min(fx, 1f - fx) * n / 2f;     // pixels to the butt joint
                    float groove = S(1.8f, 0.5f, Mathf.Min(seamY, seamX));
                    c = Color.Lerp(c, new Color(0.05f, 0.035f, 0.025f), groove);
                    c.a = 0.28f + grain * 0.1f - groove * 0.2f;
                    t.Albedo[y * n + x] = c;
                    t.Height[y * n + x] = grain * 0.05f + rings * 0.02f - groove * 0.4f;
                }
            return t;
        }

        // ---------------------------------------------------------------- metal, leather, cloth

        /// <summary>Brushed metal (grey, tinted by material colour). Streaks along U.</summary>
        public static GenTex Brushed(int n)
        {
            Noise.Seed(13);
            var t = New(n, n);
            t.NormalStrength = 0.6f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float s = Noise.Perlin(u * 4f, v * 256f, 4, 256) * 0.5f + Noise.Perlin(u * 2f, v * 128f, 2, 128) * 0.5f;
                    float b = Noise.Fbm(u, v, 3, 3, 3) * 0.5f + 0.5f;
                    float g = 0.86f + s * 0.06f + b * 0.04f;
                    t.Albedo[y * n + x] = new Color(g, g, g, 0.72f + s * 0.08f - b * 0.06f);
                    t.Height[y * n + x] = s * 0.1f;
                }
            return t;
        }

        /// <summary>Hammered metal (for copper mug, ice bucket).</summary>
        public static GenTex Hammered(int n)
        {
            Noise.Seed(17);
            var t = New(n, n);
            t.NormalStrength = 3f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    Noise.Cell(u, v, 14, out float f1, out float f2, out int id);
                    float dent = 1f - f1 * f1 * 1.4f;
                    float b = Noise.Fbm(u, v, 4, 4, 3) * 0.5f + 0.5f;
                    float g = 0.9f + b * 0.08f;
                    t.Albedo[y * n + x] = new Color(g, g, g, 0.8f - (f2 - f1) * 0.05f);
                    t.Height[y * n + x] = -dent * 0.15f;
                }
            return t;
        }

        /// <summary>Grained leather (white, tinted by material colour).</summary>
        public static GenTex Leather(int n)
        {
            Noise.Seed(19);
            var t = New(n, n);
            t.NormalStrength = 2.2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    Noise.Cell(u, v, 60, out float f1, out float f2, out _);
                    float crease = S(0.0f, 0.12f, f2 - f1);
                    float b = Noise.Fbm(u, v, 4, 4, 4) * 0.5f + 0.5f;
                    float g = 0.78f + b * 0.14f + crease * 0.06f;
                    t.Albedo[y * n + x] = new Color(g, g, g, 0.32f + crease * 0.15f + b * 0.08f);
                    t.Height[y * n + x] = crease * 0.12f;
                }
            return t;
        }

        /// <summary>Woven upholstery fabric (white, tinted by material colour).</summary>
        public static GenTex Fabric(int n)
        {
            Noise.Seed(29);
            var t = New(n, n);
            t.NormalStrength = 2f;
            const float k = 96f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n, v = (float)y / n;
                    float wu = Mathf.Sin(u * k * Mathf.PI * 2f), wv = Mathf.Sin(v * k * Mathf.PI * 2f);
                    bool over = (Mathf.FloorToInt(u * k * 2f) + Mathf.FloorToInt(v * k * 2f)) % 2 == 0;
                    float h = over ? Mathf.Abs(wu) : Mathf.Abs(wv);
                    float b = Noise.Fbm(u, v, 8, 8, 3) * 0.5f + 0.5f;
                    float g = 0.75f + h * 0.15f + b * 0.08f;
                    t.Albedo[y * n + x] = new Color(g, g, g, 0.08f);
                    t.Height[y * n + x] = h * 0.06f;
                }
            return t;
        }

        /// <summary>Black rubber bar mat with round nubs.</summary>
        public static GenTex RubberMat(int n)
        {
            var t = New(n, n);
            t.NormalStrength = 4f;
            const int cells = 24;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n * cells, v = (float)y / n * cells;
                    float fu = u - Mathf.Floor(u) - 0.5f, fv = v - Mathf.Floor(v) - 0.5f;
                    float d = Mathf.Sqrt(fu * fu + fv * fv);
                    float nub = S(0.34f, 0.26f, d);
                    float g = 0.05f + nub * 0.03f;
                    t.Albedo[y * n + x] = new Color(g, g, g * 1.05f, 0.25f + nub * 0.2f);
                    t.Height[y * n + x] = nub * 0.25f;
                }
            return t;
        }

        // ---------------------------------------------------------------- city and sky

        /// <summary>Facade with windows. Tile covers 16 windows across and 24 floors.</summary>
        public static GenTex Windows(int n, int seed)
        {
            Noise.Seed(seed);
            var t = New(n, n, false);
            t.Emission = new Color[n * n];
            const int cols = 24, rows = 36;
            Color[] lit = { new Color(1f, 0.78f, 0.45f), new Color(1f, 0.88f, 0.65f), new Color(0.75f, 0.85f, 1f), new Color(1f, 0.65f, 0.35f), new Color(0.95f, 0.95f, 0.9f) };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (float)x / n * cols, v = (float)y / n * rows;
                    int cx = Mathf.FloorToInt(u), cy = Mathf.FloorToInt(v);
                    float fx = u - cx, fy = v - cy;
                    bool glass = fx > 0.08f && fx < 0.92f && fy > 0.2f && fy < 0.9f && Mathf.Abs(fx - 0.5f) > 0.025f;
                    bool darkFloor = Noise.Rand01(cy * 7 + seed, 11) < 0.22f;
                    float r = Noise.Rand01(cx * 3 + seed, cy * 5);
                    float r2 = Noise.Rand01(cx + 77, cy * 3 + seed);
                    Color alb = new Color(0.05f, 0.055f, 0.065f, 0.3f);
                    Color em = Color.black;
                    if (glass)
                    {
                        alb = new Color(0.03f, 0.04f, 0.06f, 0.9f);
                        if (r < (darkFloor ? 0.06f : 0.3f))
                        {
                            Color c = lit[Mathf.FloorToInt(r2 * lit.Length) % lit.Length];
                            float bright = 0.35f + r2 * 0.9f;
                            // blinds and inner gradient
                            float blinds = (r > 0.25f) ? (Mathf.Sin(fy * 60f) * 0.25f + 0.75f) : 1f;
                            float grad = 0.7f + 0.3f * (1f - Mathf.Abs(fx - 0.5f) * 2f);
                            em = c * bright * blinds * grad;
                        }
                        else if (r < 0.42f) em = new Color(0.12f, 0.16f, 0.3f) * 0.6f; // TV glow
                    }
                    t.Albedo[y * n + x] = alb;
                    t.Emission[y * n + x] = em;
                }
            return t;
        }

        /// <summary>Equirectangular night sky: deep blue zenith, warm city glow near the horizon, stars.</summary>
        public static Color[] Sky(int w, int h)
        {
            var px = new Color[w * h];
            var zenith = new Color(0.006f, 0.008f, 0.025f);
            var mid = new Color(0.02f, 0.03f, 0.075f);
            var horizon = new Color(0.12f, 0.09f, 0.13f);
            var glow = new Color(0.32f, 0.17f, 0.1f);
            Noise.Seed(3);
            for (int y = 0; y < h; y++)
            {
                float lat = ((y + 0.5f) / h - 0.5f) * Mathf.PI; // -pi/2 .. pi/2
                float e = Mathf.Sin(lat);
                for (int x = 0; x < w; x++)
                {
                    float u = (float)x / w;
                    Color c;
                    if (e >= 0)
                    {
                        c = Color.Lerp(horizon, mid, S(0.0f, 0.22f, e));
                        c = Color.Lerp(c, zenith, S(0.15f, 0.9f, e));
                        float g = Mathf.Exp(-e * 14f) * (0.75f + 0.25f * Noise.Fbm(u, 0.5f, 8, 1, 3));
                        c += glow * g;
                        // faint high clouds
                        float cl = Noise.Fbm(u, (float)y / h, 6, 3, 5) * 0.5f + 0.5f;
                        c += new Color(0.05f, 0.04f, 0.06f) * S(0.55f, 0.85f, cl) * S(0.02f, 0.25f, e) * (1f - S(0.4f, 0.9f, e));
                    }
                    else c = Color.Lerp(horizon + glow * 0.8f, new Color(0.05f, 0.04f, 0.05f), S(0f, 0.3f, -e));
                    px[y * w + x] = c;
                }
            }
            // stars, drawn in direction space so they stay round near the zenith
            var rnd = new System.Random(99);
            for (int s = 0; s < 5200; s++)
            {
                float z = (float)rnd.NextDouble();
                float e = Mathf.Pow(z, 0.8f) * 0.98f + 0.02f; // more stars higher up
                float lat = Mathf.Asin(e);
                float lon = (float)rnd.NextDouble();
                float mag = Mathf.Pow((float)rnd.NextDouble(), 6f);
                float bright = (0.15f + mag * 2.4f) * S(0.03f, 0.3f, e);
                float temp = (float)rnd.NextDouble();
                Color sc = temp < 0.2f ? new Color(1f, 0.8f, 0.65f) : temp > 0.8f ? new Color(0.75f, 0.85f, 1f) : Color.white;
                float cy = (lat / Mathf.PI + 0.5f) * h;
                float cx = lon * w;
                float rad = 0.6f + mag * 1.3f;
                float sx = rad / Mathf.Max(0.05f, Mathf.Cos(lat));
                int x0 = Mathf.FloorToInt(cx - sx - 1), x1 = Mathf.CeilToInt(cx + sx + 1);
                int y0 = Mathf.FloorToInt(cy - rad - 1), y1 = Mathf.CeilToInt(cy + rad + 1);
                for (int yy = y0; yy <= y1; yy++)
                {
                    if (yy < 0 || yy >= h) continue;
                    for (int xx = x0; xx <= x1; xx++)
                    {
                        float dx = (xx + 0.5f - cx) / sx, dy = (yy + 0.5f - cy) / rad;
                        float d2 = dx * dx + dy * dy;
                        float k = Mathf.Exp(-d2 * 2.5f) * bright;
                        if (k < 0.002f) continue;
                        int wx = ((xx % w) + w) % w;
                        px[yy * w + wx] += sc * k;
                    }
                }
            }
            return px;
        }

        /// <summary>Moon disc with soft maria, alpha outside the disc is 0.</summary>
        public static Color[] Moon(int n)
        {
            Noise.Seed(61);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float a = S(0.86f, 0.82f, r);
                    float m = Noise.Fbm(u * 0.5f + 0.5f, v * 0.5f + 0.5f, 3, 3, 5) * 0.5f + 0.5f;
                    float limb = Mathf.Sqrt(Mathf.Max(0, 1f - (r / 0.86f) * (r / 0.86f)));
                    float g = (0.78f + 0.22f * S(0.55f, 0.35f, m)) * (0.65f + 0.35f * limb);
                    float halo = Mathf.Exp(-Mathf.Max(0, r - 0.84f) * 18f) * 0.25f * (1 - a);
                    px[y * n + x] = new Color(g, g * 0.98f, g * 0.93f, Mathf.Clamp01(a + halo));
                }
            return px;
        }

        // ---------------------------------------------------------------- helpers

        /// <summary>Normal map from height (tileable), stored as RGB with alpha 1.</summary>
        public static Color[] NormalFromHeight(GenTex t)
        {
            int w = t.W, h = t.H;
            var n = new Color[w * h];
            float s = t.NormalStrength * w / 256f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int xl = (x - 1 + w) % w, xr = (x + 1) % w, yd = (y - 1 + h) % h, yu = (y + 1) % h;
                    float dx = (t.Height[y * w + xr] - t.Height[y * w + xl]) * 0.5f;
                    float dy = (t.Height[yu * w + x] - t.Height[yd * w + x]) * 0.5f;
                    var v = new Vector3(-dx * s, -dy * s, 1f).normalized;
                    n[y * w + x] = new Color(v.x * 0.5f + 0.5f, v.y * 0.5f + 0.5f, v.z * 0.5f + 0.5f, 1f);
                }
            return n;
        }
    }
}
