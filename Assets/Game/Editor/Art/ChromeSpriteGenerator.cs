using System.IO;
using UnityEditor;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Draws the UI chrome — plates, bars, notes, beams — instead of generating it as pictures.
    /// These shapes have to line up with 9-slice borders and lane widths to the pixel, which is a
    /// job for a deterministic script rather than an image model.
    /// </summary>
    internal static class ChromeSpriteGenerator
    {
        public const string OutputFolder = "Assets/Game/UI/Art/Chrome";

        [MenuItem("Tools/DJ Maximus/Generate UI Chrome")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);

            Write("Solid", Solid(), Vector4.zero);
            Write("Panel", Panel(), new Vector4(20f, 20f, 20f, 20f));
            Write("PanelOutline", PanelOutline(), new Vector4(20f, 20f, 20f, 20f));
            Write("PanelCut", PanelCut(), new Vector4(26f, 20f, 26f, 20f));
            Write("Bar", Bar(), new Vector4(19f, 19f, 19f, 19f));
            Write("BarCut", BarCut(), new Vector4(26f, 10f, 26f, 10f));
            Write("Underline", Underline(), new Vector4(6f, 0f, 6f, 0f));
            Write("Glow", Glow(), Vector4.zero);
            Write("Ring", Ring(), Vector4.zero);
            Write("JacketFrame", JacketFrame(), new Vector4(40f, 40f, 40f, 40f));
            Write("LaneGuide", LaneGuide(), Vector4.zero);
            Write("RailEdge", RailEdge(), Vector4.zero);
            Write("KeyCap", KeyCap(), new Vector4(16f, 16f, 16f, 16f));
            Write("NoteNormal", Note(64, false), new Vector4(0f, 15f, 0f, 15f));
            Write("NoteFx", Note(128, true), new Vector4(0f, 15f, 0f, 15f));
            Write("KeyBeam", KeyBeam(), Vector4.zero);
            Write("KeyBurst", KeyBurst(), Vector4.zero);
            Write("Chevron", Chevron(), Vector4.zero);
            Write("Hatch", Hatch(), Vector4.zero, repeat: true);
            Write("Vignette", Vignette(), Vector4.zero);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("UI chrome sprites written to " + OutputFolder);
        }

        // --- Shapes ---------------------------------------------------------------------------

        /// <summary>
        /// A plain rectangle. Bars that grow and shrink need one: an Image with no sprite draws a
        /// full quad and ignores its fill amount, so a gauge built without this never moves.
        /// </summary>
        private static Texture2D Solid()
        {
            var canvas = new ShapeCanvas(8, 8);
            canvas.Fill(canvas.RoundedRect(new Rect(0f, 0f, 8f, 8f), 0f), Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D Panel()
        {
            var canvas = new ShapeCanvas(64, 64);
            var body = canvas.RoundedRect(new Rect(1f, 1f, 62f, 62f), 14f);
            canvas.Fill(body, Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D PanelOutline()
        {
            var canvas = new ShapeCanvas(64, 64);
            var body = canvas.RoundedRect(new Rect(1.5f, 1.5f, 61f, 61f), 14f);
            canvas.Stroke(body, Color.white, 2f);
            return canvas.ToTexture();
        }

        private static Texture2D PanelCut()
        {
            var canvas = new ShapeCanvas(96, 64);
            var body = canvas.CutCornerRect(new Rect(1f, 1f, 94f, 62f), 18f);
            canvas.Fill(body, Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D Bar()
        {
            var canvas = new ShapeCanvas(40, 40);
            canvas.Fill(canvas.RoundedRect(new Rect(1f, 1f, 38f, 38f), 19f), Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D BarCut()
        {
            var canvas = new ShapeCanvas(96, 40);
            canvas.Fill(canvas.CutCornerRect(new Rect(1f, 1f, 94f, 38f), 14f), Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D Underline()
        {
            var canvas = new ShapeCanvas(64, 8);
            var body = canvas.RoundedRect(new Rect(0f, 2f, 64f, 4f), 2f);
            canvas.FillShaded(body, point =>
            {
                // Brightest in the middle so a tab underline reads as lit rather than painted.
                float t = Mathf.Abs(point.x / 64f - 0.5f) * 2f;
                return Color.white.WithAlphaValue(Mathf.Lerp(1f, 0.15f, t * t));
            });
            return canvas.ToTexture();
        }

        private static Texture2D Glow()
        {
            var canvas = new ShapeCanvas(256, 256);
            var centre = canvas.Centre;
            canvas.FillShaded(canvas.Circle(centre, 127f), point =>
            {
                float t = Mathf.Clamp01((point - centre).magnitude / 127f);
                float falloff = Mathf.Pow(1f - t, 2.4f);
                return Color.white.WithAlphaValue(falloff);
            });
            return canvas.ToTexture();
        }

        private static Texture2D Ring()
        {
            var canvas = new ShapeCanvas(512, 512);
            canvas.Stroke(canvas.Circle(canvas.Centre, 236f), Color.white, 8f);
            canvas.Stroke(canvas.Circle(canvas.Centre, 252f), Color.white.WithAlphaValue(0.35f), 3f);
            return canvas.ToTexture();
        }

        private static Texture2D JacketFrame()
        {
            var canvas = new ShapeCanvas(128, 128);
            var outer = canvas.CutCornerRect(new Rect(2f, 2f, 124f, 124f), 22f);
            canvas.Stroke(outer, Color.white, 3f);
            var inner = canvas.CutCornerRect(new Rect(8f, 8f, 112f, 112f), 18f);
            canvas.Stroke(inner, Color.white.WithAlphaValue(0.28f), 1.5f);
            return canvas.ToTexture();
        }

        /// <summary>A lane separator that fades out with distance, so the lanes read as receding.</summary>
        private static Texture2D LaneGuide()
        {
            var canvas = new ShapeCanvas(16, 256);
            canvas.FillShaded(canvas.RoundedRect(new Rect(7f, 0f, 2f, 256f), 0f), point =>
                Color.white.WithAlphaValue(Mathf.Pow(1f - point.y / 256f, 1.6f)));
            return canvas.ToTexture();
        }

        /// <summary>The gear's side rail: dark up top, lit where the lanes meet the judgement line.</summary>
        private static Texture2D RailEdge()
        {
            var canvas = new ShapeCanvas(48, 256);
            canvas.FillShaded(canvas.RoundedRect(new Rect(0f, 0f, 48f, 256f), 0f), point =>
            {
                float lift = Mathf.Pow(1f - point.y / 256f, 2f);
                float edge = point.x >= 42f ? 1f : 0f;
                return Color.white.WithAlphaValue(Mathf.Clamp01(0.16f + lift * 0.5f + edge * lift * 0.85f));
            });
            return canvas.ToTexture();
        }

        /// <summary>The plate under each lane that carries its key letter.</summary>
        private static Texture2D KeyCap()
        {
            var canvas = new ShapeCanvas(64, 64);
            canvas.Fill(canvas.RoundedRect(new Rect(1f, 1f, 62f, 62f), 10f), Color.white.WithAlphaValue(0.9f));
            // A brighter lip along the top edge, where the key meets the judgement line.
            canvas.Fill(canvas.RoundedRect(new Rect(6f, 52f, 52f, 6f), 3f), Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D Note(int width, bool isFx)
        {
            var canvas = new ShapeCanvas(width, 48);
            var body = canvas.RoundedRect(new Rect(1f, 1f, width - 2f, 46f), 7f);

            canvas.FillShaded(body, point =>
            {
                // Caps are brighter than the middle so a stretched hold still reads as a note.
                float t = Mathf.Abs(point.y / 48f - 0.5f) * 2f;
                float brightness = Mathf.Lerp(isFx ? 0.72f : 0.82f, 1f, t * t);
                return new Color(brightness, brightness, brightness, 1f);
            });

            canvas.Stroke(canvas.RoundedRect(new Rect(1.5f, 1.5f, width - 3f, 45f), 7f),
                Color.white, 2f);

            if (isFx)
            {
                // A centre notch keeps the wide FX note from reading as an empty slab.
                var notch = canvas.RoundedRect(new Rect(width * 0.5f - 18f, 18f, 36f, 12f), 6f);
                canvas.Fill(notch, new Color(0.35f, 0.35f, 0.35f, 1f));
            }

            return canvas.ToTexture();
        }

        /// <summary>
        /// A square that fades out with height. It is stretched across a whole lane, so the fade runs
        /// top to bottom only: any narrowing across the square would pull the beam off the lane edges.
        /// </summary>
        private static Texture2D KeyBeam()
        {
            var canvas = new ShapeCanvas(256, 256);
            canvas.FillShaded(canvas.RoundedRect(new Rect(0f, 0f, 256f, 256f), 0f), point =>
                Color.white.WithAlphaValue(Mathf.Clamp01(Mathf.Pow(1f - point.y / 256f, 2.2f))));
            return canvas.ToTexture();
        }

        private static Texture2D KeyBurst()
        {
            var canvas = new ShapeCanvas(256, 256);
            var centre = canvas.Centre;
            canvas.FillShaded(canvas.Circle(centre, 127f), point =>
            {
                float t = Mathf.Clamp01((point - centre).magnitude / 127f);
                // A bright core with a ring around it, so the burst has an edge to expand.
                float core = Mathf.Pow(1f - t, 5f);
                float ring = Mathf.Exp(-Mathf.Pow((t - 0.72f) / 0.13f, 2f)) * 0.8f;
                return Color.white.WithAlphaValue(Mathf.Clamp01(core + ring));
            });
            return canvas.ToTexture();
        }

        private static Texture2D Chevron()
        {
            var canvas = new ShapeCanvas(64, 64);
            canvas.Fill(canvas.Triangle(new Vector2(14f, 6f), new Vector2(50f, 32f), new Vector2(14f, 58f)), Color.white);
            return canvas.ToTexture();
        }

        private static Texture2D Hatch()
        {
            var canvas = new ShapeCanvas(64, 64);
            canvas.FillShaded(canvas.RoundedRect(new Rect(0f, 0f, 64f, 64f), 0f), point =>
            {
                float stripe = Mathf.Repeat(point.x + point.y, 16f);
                return Color.white.WithAlphaValue(stripe < 7f ? 1f : 0f);
            });
            return canvas.ToTexture();
        }

        private static Texture2D Vignette()
        {
            var canvas = new ShapeCanvas(256, 256);
            var centre = canvas.Centre;
            canvas.FillShaded(canvas.RoundedRect(new Rect(0f, 0f, 256f, 256f), 0f), point =>
            {
                float t = Mathf.Clamp01((point - centre).magnitude / 150f);
                return Color.white.WithAlphaValue(Mathf.Pow(t, 2.6f));
            });
            return canvas.ToTexture();
        }

        // --- Output ---------------------------------------------------------------------------

        private static void Write(string name, Texture2D texture, Vector4 border, bool repeat = false)
        {
            string path = OutputFolder + "/" + name + ".png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer)) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
    }

    internal static class ColorAlphaExtensions
    {
        public static Color WithAlphaValue(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
