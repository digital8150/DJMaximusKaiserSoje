using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// Turns the bundled Pretendard weights into TextMeshPro font assets. The atlases are dynamic,
    /// so Korean glyphs are rasterized when a screen first asks for them instead of baking a
    /// pre-generated sheet of thousands of syllables into the build.
    /// </summary>
    internal static class FontAssetBuilder
    {
        public const string FontFolder = "Assets/Game/UI/Fonts";
        public const string OutputFolder = "Assets/Game/UI/Fonts/TMP";

        private static readonly string[] Weights = { "Regular", "Medium", "Bold", "ExtraBold", "Black" };

        [MenuItem("Tools/DJ Maximus/Build Font Assets")]
        public static void Build()
        {
            if (!TmpEssentials.IsImported)
            {
                Debug.LogError("Import the TMP essential resources before building font assets.");
                return;
            }

            Directory.CreateDirectory(OutputFolder);

            foreach (var weight in Weights)
            {
                string sourcePath = $"{FontFolder}/Pretendard-{weight}.ttf";
                var font = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
                if (font == null)
                {
                    Debug.LogWarning("Missing font file: " + sourcePath);
                    continue;
                }

                string assetPath = $"{OutputFolder}/Pretendard-{weight} SDF.asset";
                if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(assetPath) != null) continue;

                var fontAsset = TMP_FontAsset.CreateFontAsset(
                    font,
                    samplingPointSize: 90,
                    atlasPadding: 9,
                    renderMode: GlyphRenderMode.SDFAA,
                    atlasWidth: 1024,
                    atlasHeight: 1024,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                fontAsset.name = $"Pretendard-{weight} SDF";
                AssetDatabase.CreateAsset(fontAsset, assetPath);

                // The atlas texture and material belong to the font asset, not to the project root.
                if (fontAsset.atlasTextures != null)
                {
                    foreach (var atlas in fontAsset.atlasTextures)
                    {
                        if (atlas == null) continue;
                        atlas.name = fontAsset.name + " Atlas";
                        AssetDatabase.AddObjectToAsset(atlas, fontAsset);
                    }
                }

                if (fontAsset.material != null)
                {
                    fontAsset.material.name = fontAsset.name + " Material";
                    AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
                }

                EditorUtility.SetDirty(fontAsset);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Pretendard font assets are ready in " + OutputFolder);
        }

        public static TMP_FontAsset Load(string weight) =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>($"{OutputFolder}/Pretendard-{weight} SDF.asset");
    }
}
