using UnityEngine;

namespace Bartender
{
    /// <summary>Tileable gradient and cellular noise for texture generation.</summary>
    public static class Noise
    {
        static readonly int[] perm = new int[512];

        static Noise() => Seed(1337);

        public static void Seed(int seed)
        {
            var rnd = new System.Random(seed);
            var p = new int[256];
            for (int i = 0; i < 256; i++) p[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rnd.Next(i + 1);
                int t = p[i]; p[i] = p[j]; p[j] = t;
            }
            for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
        }

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        public static int Hash(int x, int y) => perm[(perm[x & 255] + y) & 255];
        public static int Hash(int x, int y, int z) => perm[(perm[(perm[x & 255] + y) & 255] + z) & 255];

        static float Grad(int h, float x, float y)
        {
            switch (h & 7)
            {
                case 0: return x + y;
                case 1: return -x + y;
                case 2: return x - y;
                case 3: return -x - y;
                case 4: return x;
                case 5: return -x;
                case 6: return y;
                default: return -y;
            }
        }

        /// <summary>Gradient noise in about [-1, 1], repeating every px / py units.</summary>
        public static float Perlin(float x, float y, int px, int py)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            int ax = Mod(x0, px), bx = Mod(x0 + 1, px);
            int ay = Mod(y0, py), by = Mod(y0 + 1, py);
            float u = Fade(fx), v = Fade(fy);
            float n00 = Grad(Hash(ax, ay), fx, fy);
            float n10 = Grad(Hash(bx, ay), fx - 1f, fy);
            float n01 = Grad(Hash(ax, by), fx, fy - 1f);
            float n11 = Grad(Hash(bx, by), fx - 1f, fy - 1f);
            return Mathf.Lerp(Mathf.Lerp(n00, n10, u), Mathf.Lerp(n01, n11, u), v) * 1.3f;
        }

        /// <summary>Fractal noise. u, v in [0,1) tile; base frequency fx/fy must be whole numbers.</summary>
        public static float Fbm(float u, float v, int fx, int fy, int octaves, float gain = 0.5f)
        {
            float sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * Perlin(u * fx, v * fy, fx, fy);
                norm += amp;
                amp *= gain;
                fx *= 2; fy *= 2;
            }
            return sum / norm;
        }

        /// <summary>Worley cellular noise: f1 = distance to nearest point, f2 = second nearest, id = cell hash.</summary>
        public static void Cell(float u, float v, int cells, out float f1, out float f2, out int id)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            f1 = 9f; f2 = 9f; id = 0;
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int hx = Mod(gx, cells), hy = Mod(gy, cells);
                    int h = Hash(hx, hy);
                    float ox = (Hash(hx, hy, 17) & 255) / 255f, oy = (Hash(hx, hy, 91) & 255) / 255f;
                    float px = gx + ox - x, py = gy + oy - y;
                    float d = Mathf.Sqrt(px * px + py * py);
                    if (d < f1) { f2 = f1; f1 = d; id = h; }
                    else if (d < f2) f2 = d;
                }
        }

        public static float Rand01(int a, int b) => (Hash(a & 255, b & 255, (a * 7 + b * 13) & 255) & 255) / 255f;
    }
}
