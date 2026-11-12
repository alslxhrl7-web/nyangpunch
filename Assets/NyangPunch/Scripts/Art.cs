using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.TextCore.LowLevel;

namespace NyangPunch
{
    /// <summary>Palette, sprites exported from the HTML prototype, and small uGUI builders.</summary>
    public static class Art
    {
        static Color Hex(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        public static readonly Color Ink = Hex("#2b2233"), InkSoft = Hex("#5d5368"), Paper = Hex("#fffaf2");
        public static readonly Color FloorA = Hex("#dfe4f4"), FloorB = Hex("#d1d8ee");
        public static readonly Color Cheese = Hex("#f5a142"), Tomato = Hex("#e8505b"), Butter = Hex("#ffd23f"), Mint = Hex("#5fbf98");
        public static readonly Color Pink = Hex("#ff8fb1"), Slot = Hex("#efe6d6"), SlotGood = Hex("#c9efd9"), SlotSwap = Hex("#f6e3b8"), SlotHint = Hex("#e3f4e9");
        public static readonly Color CellA = Hex("#f6ecdb"), CellB = Hex("#efe2cc"), Shade = new Color(0.17f, 0.13f, 0.2f, 0.45f);

        public struct Meta { public float w, h, ox, oy, scale; }

        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();
        static Dictionary<string, Meta> meta;

        public static Sprite Spr(string name)
        {
            if (!sprites.TryGetValue(name, out var s)) { s = Resources.Load<Sprite>("Sprites/" + name); sprites[name] = s; if (s == null) Debug.LogWarning("NyangPunch: missing sprite " + name); }
            return s;
        }

        public static Meta M(string name)
        {
            if (meta == null)
            {
                meta = new Dictionary<string, Meta>();
                var ta = Resources.Load<TextAsset>("Sprites/sprite_meta");
                if (ta != null)
                {
                    var rx = new Regex("\"(\\w+)\"\\s*:\\s*\\{\\s*\"w\"\\s*:\\s*([\\d.]+)\\s*,\\s*\"h\"\\s*:\\s*([\\d.]+)\\s*,\\s*\"ox\"\\s*:\\s*([\\d.]+)\\s*,\\s*\"oy\"\\s*:\\s*([\\d.]+)\\s*,\\s*\"scale\"\\s*:\\s*([\\d.]+)");
                    foreach (Match m in rx.Matches(ta.text))
                    {
                        float F(int i) => float.Parse(m.Groups[i].Value, CultureInfo.InvariantCulture);
                        meta[m.Groups[1].Value] = new Meta { w = F(2), h = F(3), ox = F(4), oy = F(5), scale = F(6) };
                    }
                }
            }
            return meta.TryGetValue(name, out var mm) ? mm : new Meta { w = 64, h = 64, ox = 32, oy = 32, scale = 3 };
        }

        // ---------- layout helpers (top-left anchored, y grows downward) ----------

        public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Img(Transform parent, string name, string sprite, Color color, float x, float y, float w, float h, bool sliced = false, bool raycast = false)
        {
            var rt = Rect(parent, name, x, y, w, h);
            var im = rt.gameObject.AddComponent<Image>();
            if (sprite != null) im.sprite = Spr(sprite);
            im.color = color;
            im.raycastTarget = raycast;
            if (sliced) { im.type = Image.Type.Sliced; im.pixelsPerUnitMultiplier = 2f; }
            return im;
        }

        /// <summary>Places an exported sprite so that its drawing origin lands on (px,py) of the parent, at a game-unit scale.</summary>
        public static Image Actor(Transform parent, string name, string sprite)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            var im = go.AddComponent<Image>();
            im.raycastTarget = false;
            SetActorSprite(im, sprite);
            return im;
        }

        public static void SetActorSprite(Image im, string sprite)
        {
            // compare the cached sprite by reference: Object.name allocates a new string on every call, and this runs per frame
            var s = Spr(sprite);
            if (s != null && im.sprite == s) return;
            im.sprite = s;
            var m = M(sprite);
            var rt = im.rectTransform;
            rt.pivot = new Vector2(m.ox / m.w, 1 - m.oy / m.h);
            rt.sizeDelta = new Vector2(m.w, m.h);
        }

        public static void PlaceActor(Image im, float px, float py, float unitScale, float flipX = 1)
        {
            var rt = im.rectTransform;
            rt.anchoredPosition = new Vector2(px, -py);
            rt.localScale = new Vector3(unitScale * flipX, unitScale, 1);
        }

        // ---------- TextMeshPro (SDF stays sharp at any canvas scale) ----------

        static TMP_FontAsset boldFont, regularFont;
        static Material outlineMat;

        static TMP_FontAsset MakeFont(string res)
        {
            var f = Resources.Load<Font>(res);
            if (f == null) { Debug.LogWarning("NyangPunch: missing font " + res); return TMP_Settings.defaultFontAsset; }
            var fa = TMP_FontAsset.CreateFontAsset(f, 64, 8, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            fa.name = f.name + " SDF";
            return fa;
        }

        // Jua (SIL OFL 1.1, Resources/Fonts/Jua-OFL.txt) has a single weight, so titles and body share it.
        public static TMP_FontAsset BoldFont => boldFont != null ? boldFont : (boldFont = MakeFont("Fonts/Jua"));
        public static TMP_FontAsset RegularFont => regularFont != null ? regularFont : (regularFont = BoldFont);

        /// <summary>Shared ink-outline material so outlined labels look like the canvas otext() strokes without per-text material copies.</summary>
        static Material OutlineMat
        {
            get
            {
                if (outlineMat == null)
                {
                    outlineMat = new Material(BoldFont.material) { name = "NyangFont Outline" };
                    outlineMat.EnableKeyword(ShaderUtilities.Keyword_Outline);
                    outlineMat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.28f);
                    outlineMat.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
                    outlineMat.SetFloat(ShaderUtilities.ID_FaceDilate, 0.28f);
                }
                return outlineMat;
            }
        }

        /// <summary>Jua draws smaller than the system font at the same point size; scale every label up a little.</summary>
        const float FontScale = 1.12f;

        static TextAlignmentOptions Align(TextAnchor a)
        {
            switch (a)
            {
                case TextAnchor.UpperLeft: return TextAlignmentOptions.TopLeft;
                case TextAnchor.UpperCenter: return TextAlignmentOptions.Top;
                case TextAnchor.UpperRight: return TextAlignmentOptions.TopRight;
                case TextAnchor.MiddleLeft: return TextAlignmentOptions.Left;
                case TextAnchor.MiddleRight: return TextAlignmentOptions.Right;
                case TextAnchor.LowerLeft: return TextAlignmentOptions.BottomLeft;
                case TextAnchor.LowerCenter: return TextAlignmentOptions.Bottom;
                case TextAnchor.LowerRight: return TextAlignmentOptions.BottomRight;
                default: return TextAlignmentOptions.Center;
            }
        }

        public static TMP_Text Txt(Transform parent, string str, int size, Color color, TextAnchor align, float x, float y, float w, float h, bool outline = false, FontStyle style = FontStyle.Bold)
        {
            var rt = Rect(parent, "Text", x, y, w, h);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            bool bold = style == FontStyle.Bold || outline;
            t.font = bold ? BoldFont : RegularFont;
            if (outline) t.fontSharedMaterial = OutlineMat;
            t.fontSize = size * FontScale; t.color = color; t.alignment = Align(align); t.text = str;
            t.textWrappingMode = TextWrappingModes.Normal; t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false; t.richText = true; t.lineSpacing = -8;
            return t;
        }

        /// <summary>Cut-paper card: ink drop shadow, fill and ink ring.</summary>
        public static Image Card(Transform parent, string name, float x, float y, float w, float h, Color fill, float shadow = 6, string fillSprite = "ui_fill", string ringSprite = "ui_ring")
        {
            var root = Rect(parent, name, x, y, w, h);
            if (shadow > 0) Img(root, "Shadow", fillSprite, Ink, shadow, shadow, w, h, true);
            var f = Img(root, "Fill", fillSprite, fill, 0, 0, w, h, true, true);
            Img(root, "Ring", ringSprite, Color.white, 0, 0, w, h, true);
            return f;
        }

        public class Btn
        {
            public RectTransform Root;
            public Button Button;
            public Image Fill;
            public TMP_Text Label, Sub;
            Color baseColor;
            public void SetColor(Color c) { baseColor = c; Fill.color = Button.interactable ? c : Color.Lerp(c, Color.gray, 0.45f); }
            public void SetInteractable(bool v) { if (Button.interactable == v) return; Button.interactable = v; SetColor(baseColor); Root.GetComponent<CanvasGroup>().alpha = v ? 1 : 0.55f; }
        }

        public static Btn Button(Transform parent, string label, string sub, float x, float y, float w, float h, Color fill, int size = 30, System.Action onClick = null, bool pill = false)
        {
            var b = new Btn();
            b.Root = Rect(parent, "Btn " + label, x, y, w, h);
            b.Root.gameObject.AddComponent<CanvasGroup>();
            b.Fill = Card(b.Root, "Card", 0, 0, w, h, fill, 6, pill ? "ui_pill" : "ui_fill", pill ? "ui_pill_ring" : "ui_ring");
            b.Button = b.Fill.gameObject.AddComponent<Button>();
            b.Button.transition = Selectable.Transition.None;
            var press = b.Fill.gameObject.AddComponent<PressSink>();
            press.target = b.Fill.transform.parent as RectTransform;
            if (onClick != null) b.Button.onClick.AddListener(() => onClick());
            float labelH = string.IsNullOrEmpty(sub) ? h : h * 0.6f;
            b.Label = Txt(b.Fill.transform.parent, label, size, Ink, string.IsNullOrEmpty(sub) ? TextAnchor.MiddleCenter : TextAnchor.LowerCenter, 6, 0, w - 12, labelH);
            if (!string.IsNullOrEmpty(sub) || sub == "")
                b.Sub = Txt(b.Fill.transform.parent, sub ?? "", Mathf.RoundToInt(size * 0.55f), InkSoft, TextAnchor.UpperCenter, 6, h * 0.6f, w - 12, h * 0.4f, false, FontStyle.Normal);
            b.SetColor(fill);
            return b;
        }
    }

    /// <summary>Pushes a button card down while it is held, like the HTML :active style.</summary>
    public class PressSink : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public RectTransform target;
        Vector2 basePos; bool down;
        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData e)
        {
            var b = GetComponent<Button>(); if (b != null && !b.interactable) return;
            if (!down) { basePos = target.anchoredPosition; target.anchoredPosition = basePos + new Vector2(0, -4); down = true; }
        }
        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData e) => Release();
        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData e) => Release();
        void Release() { if (down) { target.anchoredPosition = basePos; down = false; } }
    }
}
