using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DJMaximusKaiserSoje.Presentation;
using UnityEditor;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor.Content
{
    public static class BattleCrewCatalogGenerator
    {
        public const string CatalogAssetPath = "Assets/Game/Content/BattleCrewCatalog.asset";

        [MenuItem("Rhythm/Generate Battle Crew Catalog")]
        public static void GenerateMenu()
        {
            OptimizeTextures(8192);
            string result = Generate();
            Debug.Log("[BattleCrewCatalogGenerator] " + result);
        }

        [MenuItem("Rhythm/Optimize Crew Textures (8192)")]
        public static void OptimizeTexturesMenu()
        {
            OptimizeTextures(8192);
            AssetDatabase.SaveAssets();
            Debug.Log("[BattleCrewCatalogGenerator] All character textures optimized to 8192 CompressedHQ (no mipmaps).");
        }

        public static void OptimizeTextures(int maxAnimationSize = 8192)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/UI/Art/Characters" });
            int count = 0;
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool isSource = path.Contains("Source");
                int targetSize = isSource ? 2048 : maxAnimationSize;

                bool changed = false;
                if (importer.maxTextureSize != targetSize)
                {
                    importer.maxTextureSize = targetSize;
                    changed = true;
                }
                if (importer.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    changed = true;
                }
                if (importer.crunchedCompression)
                {
                    importer.crunchedCompression = false;
                    changed = true;
                }
                if (importer.mipmapEnabled)
                {
                    importer.mipmapEnabled = false;
                    changed = true;
                }
                if (importer.filterMode != FilterMode.Bilinear)
                {
                    importer.filterMode = FilterMode.Bilinear;
                    changed = true;
                }

                var defaultPlatform = importer.GetDefaultPlatformTextureSettings();
                if (defaultPlatform.maxTextureSize != targetSize ||
                    defaultPlatform.textureCompression != TextureImporterCompression.CompressedHQ ||
                    defaultPlatform.crunchedCompression)
                {
                    defaultPlatform.maxTextureSize = targetSize;
                    defaultPlatform.textureCompression = TextureImporterCompression.CompressedHQ;
                    defaultPlatform.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(defaultPlatform);
                    changed = true;
                }

                var standalonePlatform = importer.GetPlatformTextureSettings("Standalone");
                if (!standalonePlatform.overridden ||
                    standalonePlatform.maxTextureSize != targetSize ||
                    standalonePlatform.textureCompression != TextureImporterCompression.CompressedHQ ||
                    standalonePlatform.crunchedCompression)
                {
                    standalonePlatform.overridden = true;
                    standalonePlatform.maxTextureSize = targetSize;
                    standalonePlatform.textureCompression = TextureImporterCompression.CompressedHQ;
                    standalonePlatform.crunchedCompression = false;
                    importer.SetPlatformTextureSettings(standalonePlatform);
                    changed = true;
                }

                if (changed)
                {
                    EditorUtility.SetDirty(importer);
                    importer.SaveAndReimport();
                    count++;
                }
            }
            Debug.Log($"[BattleCrewCatalogGenerator] Optimized {count} character textures.");
        }

        public static string Generate()
        {
            string dir = "Assets/Game/Content";
            if (!AssetDatabase.IsValidFolder(dir))
            {
                AssetDatabase.CreateFolder("Assets/Game", "Content");
            }

            var catalog = AssetDatabase.LoadAssetAtPath<BattleCrewCatalog>(CatalogAssetPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<BattleCrewCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
            }

            var airaIdle = LoadFrames("Assets/Game/UI/Art/Characters/Aira/Aira Idle.png");
            var airaExcited = LoadFrames("Assets/Game/UI/Art/Characters/Aira/Aira Excited.png");
            var airaBad = LoadFrames("Assets/Game/UI/Art/Characters/Aira/Aira Bad.png");
            var airaSource = LoadSourceSprite("Assets/Game/UI/Art/Characters/Aira/Aira Source.png") ?? airaIdle.FirstOrDefault();

            var kaiIdle = LoadFrames("Assets/Game/UI/Art/Characters/Kai/Kai idle_transparent.png");
            var kaiExcited = LoadFrames("Assets/Game/UI/Art/Characters/Kai/Kai Excited_transparent.png");
            var kaiBad = LoadFrames("Assets/Game/UI/Art/Characters/Kai/Kai Bad_transparent.png");
            var kaiSource = LoadSourceSprite("Assets/Game/UI/Art/Characters/Kai/Kai Source.png") ?? kaiIdle.FirstOrDefault();

            var lenaIdle = LoadFrames("Assets/Game/UI/Art/Characters/Lena/Lena Idle_transparent.png");
            var lenaExcited = LoadFrames("Assets/Game/UI/Art/Characters/Lena/Lena Excited_transparent.png");
            var lenaBad = LoadFrames("Assets/Game/UI/Art/Characters/Lena/Lena Bad_transparent.png");
            var lenaSource = LoadSourceSprite("Assets/Game/UI/Art/Characters/Lena/Lena Source.png") ?? lenaIdle.FirstOrDefault();

            var renIdle = LoadFrames("Assets/Game/UI/Art/Characters/Ren/Ren Idle_transparent.png");
            var renExcited = LoadFrames("Assets/Game/UI/Art/Characters/Ren/Ren Excited_transparent.png");
            var renBad = LoadFrames("Assets/Game/UI/Art/Characters/Ren/Ren Bad_transparent.png");
            var renSource = LoadSourceSprite("Assets/Game/UI/Art/Characters/Ren/Ren Source.png") ?? renIdle.FirstOrDefault();

            var aira = new BattleCrewData("aira", "아이라", "신디사이저 비트마스터", airaSource, airaIdle, airaExcited, airaBad, 24f);
            var kai = new BattleCrewData("kai", "카이", "그루브 베이스스트라이커", kaiSource, kaiIdle, kaiExcited, kaiBad, 24f);
            var lena = new BattleCrewData("lena", "레나", "일렉트로 바이올리니스트", lenaSource, lenaIdle, lenaExcited, lenaBad, 24f);
            var ren = new BattleCrewData("ren", "렌", "하이브리드 드러머", renSource, renIdle, renExcited, renBad, 24f);

            catalog.SetCrews(new[] { aira, kai, lena, ren });
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            return $"Generated catalog with {catalog.Crews.Count} crews: Aira({airaIdle.Length}/{airaExcited.Length}/{airaBad.Length}), Kai({kaiIdle.Length}/{kaiExcited.Length}/{kaiBad.Length}), Lena({lenaIdle.Length}/{lenaExcited.Length}/{lenaBad.Length}), Ren({renIdle.Length}/{renExcited.Length}/{renBad.Length})";
        }

        private static Sprite[] LoadFrames(string path)
        {
            var all = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            return all.OrderBy(s =>
            {
                var match = Regex.Match(s.name, @"_(\d+)$");
                return match.Success ? int.Parse(match.Groups[1].Value) : 0;
            }).ToArray();
        }

        private static Sprite LoadSourceSprite(string path)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            if (sprites.Length == 0) return null;
            return sprites.OrderByDescending(s => s.rect.width * s.rect.height).FirstOrDefault();
        }
    }
}
