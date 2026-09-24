using System;
using System.Collections.Generic;
using UnityEngine;

namespace GK2.MapMarkers.Map
{
    /// <summary>
    /// Builds pixel-art marker sprites at runtime: a semi-transparent parchment tag with an ink border,
    /// a drop shadow and a pointer at the bottom, holding either a bust cropped from the NPC portrait
    /// or a colored glyph. Everything is procedural, so the mod ships no image assets.
    /// Sprites are point-filtered with the pivot on the pointer tip.
    /// </summary>
    internal static class MarkerArt
    {
        private static readonly Color32 Ink = new Color32(74, 50, 32, 235);
        private static readonly Color32 PortraitInk = new Color32(74, 50, 32, 255);
        private static readonly Color32 Shadow = new Color32(30, 20, 10, 90);
        private static readonly Color32 Paper = new Color32(236, 214, 168, 255);

        private const int Padding = 3;       // border + inner margin around the content
        private const int PointerHeight = 4;
        private const int GlyphSize = 9;

        private static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>Parchment alpha 0..1; changing it invalidates the cache.</summary>
        public static float PaperOpacity { get; private set; } = 0.8f;

        public static void SetPaperOpacity(float value)
        {
            value = Mathf.Clamp01(value);
            if (!Mathf.Approximately(value, PaperOpacity))
            {
                PaperOpacity = value;
                Clear();
            }
        }

        public static Sprite ForPortrait(Sprite portrait, bool cropHead)
        {
            string key = "p:" + portrait.GetInstanceID() + (cropHead ? ":h" : ":f") + ":" + PaperOpacity;
            if (cache.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }

            PixelImage content = PixelImage.FromSprite(portrait);
            if (content != null)
            {
                content.ReplaceBlueKey(PortraitInk);
                if (cropHead)
                {
                    content = content.Crop(PortraitCropper.FindBust(content));
                }
            }
            sprite = content != null ? BuildTag(content, key.GetHashCode()) : null;
            cache[key] = sprite;
            return sprite;
        }

        public static Sprite ForGlyph(Color color)
        {
            string key = "g:" + ColorUtility.ToHtmlStringRGBA(color) + ":" + PaperOpacity;
            if (cache.TryGetValue(key, out Sprite sprite) && sprite != null)
            {
                return sprite;
            }
            sprite = BuildTag(Glyph(color), key.GetHashCode());
            cache[key] = sprite;
            return sprite;
        }

        public static void Clear()
        {
            foreach (Sprite sprite in cache.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite.texture);
                    UnityEngine.Object.Destroy(sprite);
                }
            }
            cache.Clear();
        }

        /// <summary>Diamond gem in the category color with an ink outline.</summary>
        private static PixelImage Glyph(Color color)
        {
            var image = new PixelImage(GlyphSize, GlyphSize);
            Color32 fill = color;
            Color32 light = Color.Lerp(color, Color.white, 0.45f);
            int c = GlyphSize / 2;
            for (int y = 0; y < GlyphSize; y++)
            {
                for (int x = 0; x < GlyphSize; x++)
                {
                    int d = Mathf.Abs(x - c) + Mathf.Abs(y - c);
                    if (d == c)
                    {
                        image.Set(x, y, PortraitInk);
                    }
                    else if (d < c)
                    {
                        image.Set(x, y, x < c && y < c && d == c - 1 ? light : fill);
                    }
                }
            }
            return image;
        }

        private static Sprite BuildTag(PixelImage content, int seed)
        {
            int inner = Mathf.Max(content.Width, content.Height);
            int bodyWidth = inner + Padding * 2;
            int bodyHeight = bodyWidth;
            int width = bodyWidth + 1;                      // +1 for the drop shadow
            int height = bodyHeight + PointerHeight + 1;
            var canvas = new PixelImage(width, height);
            byte paperAlpha = (byte)Mathf.RoundToInt(PaperOpacity * 255f);

            bool Inside(int x, int y)
            {
                if (x < 0 || x >= bodyWidth || y < 0)
                {
                    return false;
                }
                if (y < bodyHeight)
                {
                    int cx = Math.Min(x, bodyWidth - 1 - x);
                    int cy = Math.Min(y, bodyHeight - 1 - y);
                    return cx + cy >= 2;                   // 2px chamfered corners
                }
                int half = PointerHeight - (y - bodyHeight);
                return half > 0 && Mathf.Abs(x - (bodyWidth - 1) * 0.5f) <= half - 0.5f + 0.5f;
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!Inside(x, y))
                    {
                        if (Inside(x - 1, y - 1))
                        {
                            canvas.Set(x, y, Shadow);
                        }
                        continue;
                    }
                    bool edge = !(Inside(x - 1, y) && Inside(x + 1, y) && Inside(x, y - 1) && Inside(x, y + 1));
                    if (edge)
                    {
                        canvas.Set(x, y, Ink);
                        continue;
                    }
                    // Mottled parchment: coarse + fine value noise plus sparse darker fibers.
                    float n = Hash(x / 2, y / 2, seed) * 0.6f + Hash(x, y, seed + 7) * 0.4f;
                    float k = 0.86f + 0.2f * n;
                    if (Hash(x, y, seed + 3) > 0.94f)
                    {
                        k -= 0.12f;
                    }
                    canvas.Set(x, y, new Color32(
                        (byte)Mathf.Clamp(Paper.r * k, 0f, 255f),
                        (byte)Mathf.Clamp(Paper.g * k, 0f, 255f),
                        (byte)Mathf.Clamp(Paper.b * k, 0f, 255f),
                        paperAlpha));
                }
            }

            canvas.Blit(content, Padding + (inner - content.Width) / 2, Padding + (inner - content.Height) / 2);

            Texture2D texture = canvas.ToTexture("GK2MapMarkerTag");
            // Pivot on the pointer tip so the tip marks the exact position.
            // Texture rows are bottom-up; the tip pixel is row 1 (row 0 is its shadow).
            var pivot = new Vector2(bodyWidth * 0.5f / width, 1f / height);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 100f, 0, SpriteMeshType.FullRect);
            sprite.name = "GK2MapMarkerTag";
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                n = (n ^ (n >> 13)) * 1274126177u;
                return ((n ^ (n >> 16)) & 0xffff) / 65535f;
            }
        }
    }

    /// <summary>Finds a head-and-shoulders square in a full-figure portrait.</summary>
    internal static class PortraitCropper
    {
        public static RectInt FindBust(PixelImage image)
        {
            int w = image.Width, h = image.Height;
            int top = -1, bottom = -1;
            var left = new int[h];
            var right = new int[h];
            for (int y = 0; y < h; y++)
            {
                left[y] = -1;
                for (int x = 0; x < w; x++)
                {
                    if (image.Get(x, y).a > 8)
                    {
                        if (left[y] < 0) left[y] = x;
                        right[y] = x;
                    }
                }
                if (left[y] >= 0)
                {
                    if (top < 0) top = y;
                    bottom = y;
                }
            }
            if (top < 0)
            {
                return new RectInt(0, 0, w, h);
            }

            int figureHeight = bottom - top + 1;
            // Wide sprites (animals, groups) are not figures: keep them whole.
            if (w > figureHeight * 0.9f)
            {
                return new RectInt(0, 0, w, h);
            }

            // The neck is the first row (below a minimum head height) where the silhouette widens into shoulders.
            int minHead = Math.Max(6, figureHeight / 6);
            int headMax = 0, neck = -1;
            for (int y = top; y <= bottom; y++)
            {
                if (left[y] < 0) continue;
                int width = right[y] - left[y] + 1;
                if (y - top >= minHead && width >= headMax + 3)
                {
                    neck = y;
                    break;
                }
                headMax = Math.Max(headMax, width);
            }
            if (neck < 0 || neck - top > figureHeight * 0.65f)
            {
                neck = top + (int)(figureHeight * 0.45f);
            }

            float centerSum = 0f;
            int rows = 0;
            for (int y = top; y < neck; y++)
            {
                if (left[y] < 0) continue;
                centerSum += (left[y] + right[y]) * 0.5f;
                rows++;
            }
            float centerX = rows > 0 ? centerSum / rows : w * 0.5f;

            int side = Math.Max(Math.Max(neck - top + 4, headMax + 2), Mathf.RoundToInt(figureHeight * 0.5f));
            side = Math.Min(side, Math.Min(w, h));
            int x0 = Mathf.Clamp(Mathf.RoundToInt(centerX - side * 0.5f), 0, w - side);
            int y0 = Mathf.Clamp(top - 1, 0, h - side);
            return new RectInt(x0, y0, side, side);
        }
    }

    /// <summary>Simple RGBA image with a top-left origin (y grows downwards).</summary>
    internal sealed class PixelImage
    {
        private readonly Color32[] pixels;

        public PixelImage(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color32[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public Color32 Get(int x, int y) => pixels[y * Width + x];

        public void Set(int x, int y, Color32 c) => pixels[y * Width + x] = c;

        /// <summary>Alpha-blends <paramref name="src"/> onto this image.</summary>
        public void Blit(PixelImage src, int ox, int oy)
        {
            for (int y = 0; y < src.Height; y++)
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int tx = ox + x, ty = oy + y;
                    if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) continue;
                    Color32 s = src.Get(x, y);
                    if (s.a == 0) continue;
                    Color32 d = Get(tx, ty);
                    float a = s.a / 255f;
                    float outA = a + d.a / 255f * (1f - a);
                    Set(tx, ty, outA <= 0f ? default : new Color32(
                        (byte)((s.r * a + d.r * (d.a / 255f) * (1f - a)) / outA),
                        (byte)((s.g * a + d.g * (d.a / 255f) * (1f - a)) / outA),
                        (byte)((s.b * a + d.b * (d.a / 255f) * (1f - a)) / outA),
                        (byte)(outA * 255f)));
                }
            }
        }

        public PixelImage Crop(RectInt r)
        {
            var result = new PixelImage(r.width, r.height);
            for (int y = 0; y < r.height; y++)
            {
                for (int x = 0; x < r.width; x++)
                {
                    result.Set(x, y, Get(r.x + x, r.y + y));
                }
            }
            return result;
        }

        /// <summary>Portraits use pure blue as an outline key that the game recolors in a shader.</summary>
        public void ReplaceBlueKey(Color32 replacement)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                Color32 p = pixels[i];
                if (p.a > 0 && p.b > 200 && p.r < 50 && p.g < 50)
                {
                    pixels[i] = new Color32(replacement.r, replacement.g, replacement.b, p.a);
                }
            }
        }

        public Texture2D ToTexture(string name)
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                name = name,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            var flipped = new Color32[pixels.Length];
            for (int y = 0; y < Height; y++)
            {
                Array.Copy(pixels, y * Width, flipped, (Height - 1 - y) * Width, Width);
            }
            texture.SetPixels32(flipped);
            texture.Apply(false, true);
            return texture;
        }

        /// <summary>Reads a (possibly atlased, non-readable) sprite through a render texture.</summary>
        public static PixelImage FromSprite(Sprite sprite)
        {
            Rect rect;
            try
            {
                if (sprite.packed && (sprite.packingMode == SpritePackingMode.Tight || sprite.packingRotation != SpritePackingRotation.None))
                {
                    return null;
                }
                rect = sprite.textureRect;
            }
            catch (Exception)
            {
                return null;
            }

            Texture2D source = sprite.texture;
            int w = Mathf.RoundToInt(rect.width), h = Mathf.RoundToInt(rect.height);
            if (source == null || w <= 0 || h <= 0)
            {
                return null;
            }

            RenderTexture previous = RenderTexture.active;
            RenderTexture rt = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(source, rt);
                RenderTexture.active = rt;
                readable.ReadPixels(new Rect(Mathf.Round(rect.x), Mathf.Round(rect.y), w, h), 0, 0, false);
                readable.Apply(false, false);
                Color32[] raw = readable.GetPixels32(); // bottom-up
                var image = new PixelImage(w, h);
                for (int y = 0; y < h; y++)
                {
                    Array.Copy(raw, (h - 1 - y) * w, image.pixels, y * w, w);
                }
                return image;
            }
            catch (Exception e)
            {
                Plugin.Log?.Warning($"Could not read sprite '{sprite.name}': {e.Message}");
                return null;
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(readable);
            }
        }
    }
}
