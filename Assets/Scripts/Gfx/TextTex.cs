using UnityEngine;

namespace Bartender
{
    /// <summary>Draws text and simple shapes into textures (bottle labels, neon sign).</summary>
    public static class TextTex
    {
        public static Font Sans, Serif, Script;
        static Material drawMat;
        static int w, h;

        public static void Init()
        {
            if (Sans != null) return;
            Sans = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Segoe UI", "Segoe UI Symbol", "Arial" }, 48);
            Serif = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Palatino Linotype", "Times New Roman", "Segoe UI Symbol" }, 48);
            Script = Font.CreateDynamicFontFromOSFont(new[] { "Segoe Script", "Gabriola", "Georgia" }, 48);
            var fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (Sans == null) Sans = fallback;
            if (Serif == null) Serif = Sans;
            if (Script == null) Script = Serif;
            var tpl = Resources.Load<Material>("Materials/T_UIDraw");
            drawMat = tpl != null ? new Material(tpl) : new Material(Shader.Find("UI/Default"));
        }

        // ------------------------------------------------------------ canvas

        public static RenderTexture Begin(int width, int height, Color bg)
        {
            Init();
            w = width; h = height;
            var rt = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            rt.Create();
            RenderTexture.active = rt;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0, width, height, 0);
            GL.Clear(true, true, Color.clear);
            Rect(0, 0, width, height, bg);
            return rt;
        }

        /// <summary>Copies the canvas into a mip-mapped texture and frees the render target.</summary>
        public static Texture2D End(RenderTexture rt, string name)
        {
            GL.PopMatrix();
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true, false) { name = name, anisoLevel = 4, wrapMode = TextureWrapMode.Clamp };
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply(true, true);
            RenderTexture.active = null;
            rt.Release();
            Object.Destroy(rt);
            return tex;
        }

        public static void Rect(float x, float y, float rw, float rh, Color c)
        {
            drawMat.mainTexture = Texture2D.whiteTexture;
            drawMat.SetVector("_TextureSampleAdd", Vector4.zero);
            drawMat.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(c.linear);
            GL.TexCoord2(0, 0); GL.Vertex3(x, y, 0);
            GL.TexCoord2(1, 0); GL.Vertex3(x + rw, y, 0);
            GL.TexCoord2(1, 1); GL.Vertex3(x + rw, y + rh, 0);
            GL.TexCoord2(0, 1); GL.Vertex3(x, y + rh, 0);
            GL.End();
        }

        public static void Frame(float x, float y, float rw, float rh, float t, Color c)
        {
            Rect(x, y, rw, t, c);
            Rect(x, y + rh - t, rw, t, c);
            Rect(x, y, t, rh, c);
            Rect(x + rw - t, y, t, rh, c);
        }

        public static void Disc(float cx, float cy, float r, Color c, int seg = 40)
        {
            drawMat.mainTexture = Texture2D.whiteTexture;
            drawMat.SetVector("_TextureSampleAdd", Vector4.zero);
            drawMat.SetPass(0);
            GL.Begin(GL.TRIANGLES);
            GL.Color(c.linear);
            for (int i = 0; i < seg; i++)
            {
                float a0 = Mathf.PI * 2 * i / seg, a1 = Mathf.PI * 2 * (i + 1) / seg;
                GL.Vertex3(cx, cy, 0);
                GL.Vertex3(cx + Mathf.Cos(a0) * r, cy + Mathf.Sin(a0) * r, 0);
                GL.Vertex3(cx + Mathf.Cos(a1) * r, cy + Mathf.Sin(a1) * r, 0);
            }
            GL.End();
        }

        public static float Measure(Font f, string s, int size, FontStyle style = FontStyle.Normal, float spacing = 0f)
        {
            f.RequestCharactersInTexture(s, size, style);
            float x = 0;
            foreach (char ch in s)
                if (f.GetCharacterInfo(ch, out var ci, size, style)) x += ci.advance + spacing;
            return x - spacing;
        }

        /// <summary>Draws text; align 0 = left, 0.5 = centre, 1 = right. y is the baseline.</summary>
        public static void Text(Font f, string s, float x, float y, int size, Color c, float align = 0.5f, FontStyle style = FontStyle.Normal, float spacing = 0f, float maxWidth = 0f)
        {
            if (string.IsNullOrEmpty(s)) return;
            float width = Measure(f, s, size, style, spacing);
            float scale = 1f;
            if (maxWidth > 0 && width > maxWidth) scale = maxWidth / width;
            f.RequestCharactersInTexture(s, size, style);
            float px = x - width * scale * align;
            drawMat.mainTexture = f.material.mainTexture;
            drawMat.SetVector("_TextureSampleAdd", new Vector4(1, 1, 1, 0));
            drawMat.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(c.linear);
            foreach (char ch in s)
            {
                if (!f.GetCharacterInfo(ch, out var ci, size, style)) continue;
                float x0 = px + ci.minX * scale, x1 = px + ci.maxX * scale;
                float y0 = y - ci.maxY * scale, y1 = y - ci.minY * scale;
                GL.TexCoord(ci.uvTopLeft); GL.Vertex3(x0, y0, 0);
                GL.TexCoord(ci.uvTopRight); GL.Vertex3(x1, y0, 0);
                GL.TexCoord(ci.uvBottomRight); GL.Vertex3(x1, y1, 0);
                GL.TexCoord(ci.uvBottomLeft); GL.Vertex3(x0, y1, 0);
                px += (ci.advance + spacing) * scale;
            }
            GL.End();
        }

        // ------------------------------------------------------------ ready-made textures

        /// <summary>Bottle label for an ingredient. The visible front is the middle of the texture.</summary>
        public static Texture2D Label(Ingredient ing)
        {
            const int W = 512, H = 256;
            var bg = ing.LabelColor;
            var ink = ing.LabelInk;
            var rt = Begin(W, H, bg);
            bool small = ing.Kind == Kind.Bitters;
            bool soda = ing.Kind == Kind.Mixer;
            bool syrup = ing.Kind == Kind.Syrup;
            // borders
            Frame(70, 14, W - 140, H - 28, 3, ink);
            Frame(80, 24, W - 160, H - 48, 1.2f, Color.Lerp(ink, bg, 0.4f));
            // ornament dots
            Disc(W / 2f, 52, 5, ink);
            Rect(W / 2f - 70, 51, 52, 2, ink);
            Rect(W / 2f + 18, 51, 52, 2, ink);
            if (syrup)
            {
                Text(Sans, "BAR SYRUP", W / 2f, 92, 26, Color.Lerp(ink, bg, 0.25f), 0.5f, FontStyle.Normal, 6f);
                Text(Serif, ing.NameEn.Replace(" Syrup", "").Replace(" Cream", "").ToUpper(), W / 2f, 160, 64, ink, 0.5f, FontStyle.Bold, 2f, W - 200);
                Text(Sans, "750 ml", W / 2f, 210, 22, Color.Lerp(ink, bg, 0.3f), 0.5f);
            }
            else if (soda)
            {
                Text(Sans, ing.Brand, W / 2f, 120, 46, ink, 0.5f, FontStyle.Bold, 3f, W - 200);
                Text(Sans, ing.NameEn.ToUpper(), W / 2f, 172, 28, Color.Lerp(ink, bg, 0.2f), 0.5f, FontStyle.Normal, 4f, W - 200);
                Text(Sans, "200 ml", W / 2f, 212, 20, Color.Lerp(ink, bg, 0.3f), 0.5f);
            }
            else
            {
                Text(Serif, ing.Brand, W / 2f, 112, small ? 40 : 44, ink, 0.5f, FontStyle.Bold, 2f, W - 200);
                Rect(W / 2f - 110, 128, 220, 1.5f, Color.Lerp(ink, bg, 0.3f));
                Text(Sans, ing.NameEn.ToUpper(), W / 2f, 168, small ? 26 : 30, ink, 0.5f, FontStyle.Normal, 5f, W - 200);
                string sub = ing.Kind == Kind.Spirit ? "40% vol  ·  700 ml" : ing.Kind == Kind.Bitters ? "44.7% vol  ·  200 ml" : ing.Kind == Kind.Wine ? "15% vol  ·  750 ml" : "25% vol  ·  700 ml";
                Text(Sans, sub, W / 2f, 210, 20, Color.Lerp(ink, bg, 0.35f), 0.5f, FontStyle.Normal, 1.5f);
            }
            return End(rt, "label_" + ing.Key);
        }

        /// <summary>White neon-style text on black, used as an emission map.</summary>
        public static Texture2D Neon(string text, string sub, int W, int H)
        {
            var rt = Begin(W, H, Color.black);
            Text(Script, text, W / 2f, H * 0.62f, (int)(H * 0.5f), Color.white, 0.5f, FontStyle.Normal, 0f, W * 0.92f);
            if (!string.IsNullOrEmpty(sub))
                Text(Sans, sub, W / 2f, H * 0.9f, (int)(H * 0.14f), new Color(1, 1, 1, 0.9f), 0.5f, FontStyle.Normal, H * 0.05f);
            return End(rt, "neon");
        }

        /// <summary>Chalk-style menu board.</summary>
        public static Texture2D MenuBoard(System.Collections.Generic.List<Recipe> recipes)
        {
            const int W = 512, H = 768;
            var rt = Begin(W, H, new Color(0.07f, 0.075f, 0.07f));
            var chalk = new Color(0.92f, 0.9f, 0.85f);
            Frame(14, 14, W - 28, H - 28, 2, new Color(0.75f, 0.6f, 0.35f));
            Text(Script, "Cocktails", W / 2f, 92, 64, chalk, 0.5f);
            float y = 150;
            int shown = 0;
            foreach (var r in recipes)
            {
                if (shown >= 12) break;
                Text(Serif, r.NameEn, 50, y, 26, chalk, 0f, FontStyle.Normal, 0f, 330);
                Text(Sans, "€" + r.Price.ToString("0"), W - 50, y, 24, new Color(0.95f, 0.78f, 0.45f), 1f);
                y += 50;
                shown++;
            }
            return End(rt, "menu");
        }
    }
}
