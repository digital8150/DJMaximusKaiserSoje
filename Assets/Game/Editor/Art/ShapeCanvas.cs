using System;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// A small anti-aliased drawing surface for UI chrome. Shapes are described as signed distance
    /// fields so a 64-pixel plate has the same clean edge as a 512-pixel ring, and everything is
    /// drawn white with alpha so the game can tint one sprite into many.
    /// </summary>
    internal sealed class ShapeCanvas
    {
        private readonly Color[] pixels;

        public ShapeCanvas(int width, int height)
        {
            Width = width;
            Height = height;
            pixels = new Color[width * height];
        }

        public int Width { get; }
        public int Height { get; }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(Width, Height, TextureFormat.RGBA32, false, true);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>Blends a colour over a pixel, weighted by how much of it the shape covers.</summary>
        private void Blend(int x, int y, Color color, float coverage)
        {
            if (coverage <= 0f || x < 0 || y < 0 || x >= Width || y >= Height) return;

            float alpha = color.a * Mathf.Clamp01(coverage);
            if (alpha <= 0f) return;

            var existing = pixels[y * Width + x];
            float outAlpha = alpha + existing.a * (1f - alpha);
            if (outAlpha <= 0f)
            {
                pixels[y * Width + x] = default;
                return;
            }

            var rgb = (new Vector3(color.r, color.g, color.b) * alpha +
                       new Vector3(existing.r, existing.g, existing.b) * existing.a * (1f - alpha)) / outAlpha;
            pixels[y * Width + x] = new Color(rgb.x, rgb.y, rgb.z, outAlpha);
        }

        /// <summary>Fills wherever <paramref name="distance"/> is negative, with a one-pixel soft edge.</summary>
        public void Fill(Func<Vector2, float> distance, Color color)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    Blend(x, y, color, Mathf.Clamp01(0.5f - distance(point)));
                }
            }
        }

        /// <summary>Draws the outline of a shape, centred on its edge.</summary>
        public void Stroke(Func<Vector2, float> distance, Color color, float thickness)
        {
            Fill(point => Mathf.Abs(distance(point)) - thickness * 0.5f, color);
        }

        /// <summary>Fills a shape with a per-pixel colour, for gradients and glows.</summary>
        public void FillShaded(Func<Vector2, float> distance, Func<Vector2, Color> shade)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    var point = new Vector2(x + 0.5f, y + 0.5f);
                    float coverage = Mathf.Clamp01(0.5f - distance(point));
                    if (coverage <= 0f) continue;
                    Blend(x, y, shade(point), coverage);
                }
            }
        }

        public Vector2 Centre => new Vector2(Width * 0.5f, Height * 0.5f);

        // --- Signed distance fields ---------------------------------------------------------

        public Func<Vector2, float> RoundedRect(Rect rect, float radius)
        {
            var centre = rect.center;
            var half = rect.size * 0.5f;
            float clamped = Mathf.Min(radius, Mathf.Min(half.x, half.y));

            return point =>
            {
                var delta = new Vector2(Mathf.Abs(point.x - centre.x), Mathf.Abs(point.y - centre.y));
                var q = delta - half + Vector2.one * clamped;
                return Mathf.Min(Mathf.Max(q.x, q.y), 0f) + new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude - clamped;
            };
        }

        /// <summary>A rectangle with its corners sliced off — the angular plate the HUD is built from.</summary>
        public Func<Vector2, float> CutCornerRect(Rect rect, float cut)
        {
            var box = RoundedRect(rect, 0f);
            var centre = rect.center;
            var half = rect.size * 0.5f;

            return point =>
            {
                float dx = Mathf.Abs(point.x - centre.x);
                float dy = Mathf.Abs(point.y - centre.y);
                float diagonal = dx + dy - (half.x + half.y - cut);
                return Mathf.Max(box(point), diagonal * 0.70710678f);
            };
        }

        public Func<Vector2, float> Circle(Vector2 centre, float radius) =>
            point => (point - centre).magnitude - radius;

        /// <summary>
        /// A filled triangle. Winding does not matter: the sign is picked so the centroid is inside.
        /// </summary>
        public Func<Vector2, float> Triangle(Vector2 a, Vector2 b, Vector2 c)
        {
            float Raw(Vector2 point) => Mathf.Max(
                Mathf.Max(EdgeDistance(point, a, b), EdgeDistance(point, b, c)),
                EdgeDistance(point, c, a));

            float sign = Raw((a + b + c) / 3f) > 0f ? -1f : 1f;
            return point => Raw(point) * sign;
        }

        private static float EdgeDistance(Vector2 point, Vector2 from, Vector2 to)
        {
            var edge = to - from;
            var normal = new Vector2(edge.y, -edge.x).normalized;
            return Vector2.Dot(point - from, normal);
        }
    }
}
