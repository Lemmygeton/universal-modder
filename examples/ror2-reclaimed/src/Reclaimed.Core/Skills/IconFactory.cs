using System.Collections.Generic;
using UnityEngine;

namespace Reclaimed.Core.Skills
{
    /// <summary>
    /// Procedural 128x128 placeholder icons (gradient plate + glyph + border) so no SkillDef, buff or
    /// unlockable ships with a null sprite while real art is pending. Deterministic per (key, color, glyph).
    /// </summary>
    public static class IconFactory
    {
        public enum Glyph { Auto, Circle, Ring, Diamond, Triangle, Cross, Bars, Chevron, Star }

        public const int Size = 128;
        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Create(string key, Color color, Glyph glyph = Glyph.Auto)
        {
            if (glyph == Glyph.Auto) glyph = (Glyph)(1 + (Hash(key) & 0x7fffffff) % 8);
            string cacheKey = key + "|" + ColorUtility.ToHtmlStringRGBA(color) + "|" + glyph;
            Sprite sprite;
            if (cache.TryGetValue(cacheKey, out sprite) && sprite != null) return sprite;

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.name = "texReclaimedIcon_" + key;
            tex.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color32[Size * Size];
            Color dark = Color.Lerp(color, Color.black, 0.8f);
            Color light = Color.Lerp(color, Color.white, 0.35f);
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float u = (x + 0.5f) / Size * 2f - 1f;
                    float v = (y + 0.5f) / Size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    Color c = Color.Lerp(color * 0.75f, dark, Mathf.Clamp01(r * 0.9f));
                    c.a = 1f;
                    float g = GlyphCoverage(glyph, u, v);
                    if (g > 0f) c = Color.Lerp(c, light, g);
                    float border = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
                    if (border > 0.92f) c = Color.Lerp(c, light, 0.8f);
                    pixels[y * Size + x] = c;
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            sprite = Sprite.Create(tex, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = "spr" + tex.name;
            cache[cacheKey] = sprite;
            return sprite;
        }

        /// <summary>Anti-aliased coverage (0..1) of a glyph at normalized coordinates (-1..1).</summary>
        private static float GlyphCoverage(Glyph glyph, float u, float v)
        {
            const float aa = 0.03f;
            float d;
            switch (glyph)
            {
                case Glyph.Circle: d = Mathf.Sqrt(u * u + v * v) - 0.45f; break;
                case Glyph.Ring: d = Mathf.Abs(Mathf.Sqrt(u * u + v * v) - 0.45f) - 0.1f; break;
                case Glyph.Diamond: d = (Mathf.Abs(u) + Mathf.Abs(v)) - 0.6f; break;
                case Glyph.Triangle: d = Mathf.Max(-v - 0.4f, Mathf.Abs(u) * 1.15f + v * 0.6f - 0.35f); break;
                case Glyph.Cross: d = Mathf.Min(Mathf.Max(Mathf.Abs(u) - 0.12f, Mathf.Abs(v) - 0.55f), Mathf.Max(Mathf.Abs(u) - 0.55f, Mathf.Abs(v) - 0.12f)); break;
                case Glyph.Bars:
                {
                    float bx = Mathf.Abs(Mathf.Repeat(u + 0.6f, 0.4f) - 0.2f) - 0.08f;
                    d = Mathf.Max(bx, Mathf.Max(Mathf.Abs(u) - 0.55f, Mathf.Abs(v) - 0.5f));
                    break;
                }
                case Glyph.Chevron:
                {
                    float c = Mathf.Abs(v - (0.45f - Mathf.Abs(u))) - 0.13f;
                    d = Mathf.Max(c, Mathf.Abs(u) - 0.55f);
                    break;
                }
                default:
                {
                    // Star: radius modulated by angle.
                    float a = Mathf.Atan2(v, u);
                    float star = 0.32f + 0.18f * Mathf.Cos(a * 5f);
                    d = Mathf.Sqrt(u * u + v * v) - star;
                    break;
                }
            }
            return Mathf.Clamp01(0.5f - d / aa);
        }

        private static int Hash(string s)
        {
            unchecked
            {
                int h = 23;
                if (s != null) foreach (char ch in s) h = h * 31 + ch;
                return h;
            }
        }
    }
}
