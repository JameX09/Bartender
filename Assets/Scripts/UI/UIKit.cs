using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Bartender
{
    /// <summary>Small helpers to build the uGUI interface in code.</summary>
    public static class UIKit
    {
        public static readonly Color Ink = new Color(0.96f, 0.93f, 0.87f);
        public static readonly Color Muted = new Color(0.72f, 0.68f, 0.62f);
        public static readonly Color Gold = new Color(0.93f, 0.74f, 0.42f);
        public static readonly Color Panel = new Color(0.06f, 0.055f, 0.07f, 0.86f);
        public static readonly Color PanelLight = new Color(0.12f, 0.11f, 0.13f, 0.92f);
        public static readonly Color Good = new Color(0.55f, 0.88f, 0.55f);
        public static readonly Color Bad = new Color(1f, 0.45f, 0.4f);

        static Sprite rounded, roundedSmall, circle, fade;

        /// <summary>Horizontal fade from opaque (left) to clear (right).</summary>
        public static Sprite FadeRight
        {
            get
            {
                if (fade) return fade;
                var t = new Texture2D(256, 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 256; x++)
                {
                    float a = 1f - Mathf.SmoothStep(0f, 1f, x / 255f);
                    for (int y = 0; y < 4; y++) t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
                t.Apply();
                return fade = Sprite.Create(t, new Rect(0, 0, 256, 4), new Vector2(0.5f, 0.5f));
            }
        }

        public static Sprite Rounded => rounded ? rounded : rounded = MakeRounded(64, 18);
        public static Sprite RoundedSmall => roundedSmall ? roundedSmall : roundedSmall = MakeRounded(32, 8);
        public static Sprite Circle => circle ? circle : circle = MakeRounded(64, 32, false);

        static Sprite MakeRounded(int n, int r, bool sliced = true)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(r - d + 0.5f);
                    px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            t.SetPixels32(px);
            t.Apply();
            var border = sliced ? new Vector4(r, r, r, r) : Vector4.zero;
            return Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        public static RectTransform Rect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        public static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Image Box(Transform parent, string name, Color c, Sprite sprite = null)
        {
            var go = Node(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = Image.Type.Sliced;
            img.color = c;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color c, TextAnchor align = TextAnchor.MiddleLeft, Font font = null, FontStyle style = FontStyle.Normal)
        {
            var go = Node("text", parent);
            var t = go.AddComponent<Text>();
            t.font = font ?? TextTex.Sans;
            t.fontSize = size;
            t.color = c;
            t.alignment = align;
            t.fontStyle = style;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static void Fill(Component c, float l = 0, float r = 0, float t = 0, float b = 0)
        {
            var rt = c.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(l, b); rt.offsetMax = new Vector2(-r, -t);
        }

        public static void Shadow(Graphic g, float d = 2f)
        {
            var s = g.gameObject.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.6f);
            s.effectDistance = new Vector2(d, -d);
        }

        /// <summary>Button with hover/press animation and a click sound.</summary>
        public static Button Button(Transform parent, string text, Action onClick, int size = 26, float width = 340, float height = 58, bool primary = false)
        {
            var bg = Box(parent, "button", primary ? new Color(0.62f, 0.42f, 0.18f, 0.95f) : new Color(1f, 1f, 1f, 0.07f));
            bg.raycastTarget = true;
            var rt = bg.rectTransform;
            rt.sizeDelta = new Vector2(width, height);
            var btn = bg.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            var label = Label(bg.transform, text, size, Ink, TextAnchor.MiddleCenter);
            Fill(label, 14, 14, 0, 0);
            btn.onClick.AddListener(() => { Sfx.Ui(); onClick?.Invoke(); });
            var hover = bg.gameObject.AddComponent<HoverSound>();
            hover.Img = bg;
            hover.Normal = bg.color;
            hover.Hover = primary ? new Color(0.78f, 0.55f, 0.25f, 1f) : new Color(1f, 1f, 1f, 0.17f);
            var le = bg.gameObject.AddComponent<LayoutElement>();
            le.preferredWidth = width; le.preferredHeight = height;
            return btn;
        }

        public static void SetButtonText(Button b, string s) => b.GetComponentInChildren<Text>().text = s;

        public static Slider Slider(Transform parent, string label, float min, float max, float value, Action<float> onChange, Func<float, string> fmt, float width = 560)
        {
            var row = Node("slider row", parent);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 46);
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 46; le.preferredWidth = width;
            var l = Label(row.transform, label, 22, Ink);
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            Rect(l.gameObject, new Vector2(0, 0), new Vector2(0.42f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var valueText = Label(row.transform, fmt(value), 20, Muted, TextAnchor.MiddleRight);
            valueText.horizontalOverflow = HorizontalWrapMode.Overflow;
            Rect(valueText.gameObject, new Vector2(0.86f, 0), new Vector2(1, 1), new Vector2(1, 0.5f), Vector2.zero, Vector2.zero);

            var sgo = Node("slider", row.transform);
            Rect(sgo, new Vector2(0.44f, 0.5f), new Vector2(0.84f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 20));
            var bg = Box(sgo.transform, "bg", new Color(1, 1, 1, 0.12f), RoundedSmall);
            Fill(bg, 0, 0, 6, 6);
            var fillArea = Node("fill area", sgo.transform);
            Fill(fillArea.GetComponent<RectTransform>(), 0, 0, 6, 6);
            var fill = Box(fillArea.transform, "fill", Gold, RoundedSmall);
            Fill(fill);
            var handleArea = Node("handle area", sgo.transform);
            Fill(handleArea.GetComponent<RectTransform>(), 8, 8, 0, 0);
            var handle = Box(handleArea.transform, "handle", Color.white, Circle);
            handle.rectTransform.sizeDelta = new Vector2(22, 22);
            handle.raycastTarget = true;
            var s = sgo.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(v => { valueText.text = fmt(v); onChange(v); });
            return s;
        }

        public static Button Toggle(Transform parent, string label, Func<string> state, Action onClick, float width = 560)
        {
            var row = Node("toggle row", parent);
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(width, 46);
            var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 46; le.preferredWidth = width;
            var l = Label(row.transform, label, 22, Ink);
            l.horizontalOverflow = HorizontalWrapMode.Overflow;
            Rect(l.gameObject, new Vector2(0, 0), new Vector2(0.6f, 1), new Vector2(0, 0.5f), Vector2.zero, Vector2.zero);
            var b = Button(row.transform, state(), null, 20, 200, 40);
            var brt = b.GetComponent<RectTransform>();
            brt.anchorMin = brt.anchorMax = new Vector2(1, 0.5f);
            brt.pivot = new Vector2(1, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            b.onClick.AddListener(() => { onClick(); SetButtonText(b, state()); });
            return b;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing, TextAnchor align = TextAnchor.UpperCenter, int pad = 0)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childControlWidth = false; v.childControlHeight = false;
            v.childForceExpandWidth = false; v.childForceExpandHeight = false;
            v.padding = new RectOffset(pad, pad, pad, pad);
            return v;
        }

        public static string Stars(int n) => new string('★', Mathf.Clamp(n, 0, 5)) + "<color=#ffffff40>" + new string('★', 5 - Mathf.Clamp(n, 0, 5)) + "</color>";

        public static string Money(float v) => "€" + v.ToString("0.00");
    }

    sealed class HoverSound : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        public Image Img;
        public Color Normal, Hover;
        bool over;
        public void OnPointerEnter(PointerEventData e) { over = true; if (Img) Img.color = Hover; Sfx.Ui("hover", 0.35f); }
        public void OnPointerExit(PointerEventData e) { over = false; if (Img) Img.color = Normal; }
        public void OnPointerDown(PointerEventData e) { if (Img) Img.color = Color.Lerp(Hover, Color.black, 0.25f); }
        public void OnPointerUp(PointerEventData e) { if (Img) Img.color = over ? Hover : Normal; }
        void OnDisable() { over = false; if (Img) Img.color = Normal; }
    }
}
