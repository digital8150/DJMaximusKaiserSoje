using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DJMaximusKaiserSoje.Editor
{
    /// <summary>
    /// TextMeshPro ships its settings, shaders, and default font in a package that has to be
    /// unpacked into the project once. <c>AssetDatabase.ImportPackage</c> is asynchronous and never
    /// finishes inside a batch-mode run that quits, so the archive is unpacked directly instead —
    /// a .unitypackage is a gzipped tar of <c>guid/{pathname,asset,asset.meta}</c> entries, and
    /// copying the meta files along with the assets keeps the original GUIDs.
    /// </summary>
    internal static class TmpEssentials
    {
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string PackageName = "TMP Essential Resources.unitypackage";

        public static bool IsImported => File.Exists(SettingsPath);

        [MenuItem("Tools/DJ Maximus/Import TMP Essentials")]
        public static void Import()
        {
            if (IsImported)
            {
                Debug.Log("TMP essential resources are already in the project.");
                return;
            }

            string package = FindEssentialsPackage();
            if (package == null)
            {
                Debug.LogError("TMP essential resources package was not found in the UGUI package.");
                return;
            }

            int written = Unpack(package);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            if (IsImported) Debug.Log($"TMP essential resources imported ({written} files).");
            else Debug.LogError($"Unpacked {written} files from {package} but {SettingsPath} is still missing.");
        }

        private static string FindEssentialsPackage()
        {
            foreach (var root in new[] { "Library/PackageCache", "Packages" })
            {
                if (!Directory.Exists(root)) continue;
                var match = Directory.GetFiles(root, PackageName, SearchOption.AllDirectories).FirstOrDefault();
                if (match != null) return match;
            }

            return null;
        }

        private static int Unpack(string packagePath)
        {
            var entries = ReadTarGz(packagePath);
            int written = 0;

            foreach (var pair in entries.Where(entry => entry.Key.EndsWith("/pathname", StringComparison.Ordinal)))
            {
                string folder = pair.Key.Substring(0, pair.Key.Length - "/pathname".Length);
                string target = Encoding.UTF8.GetString(pair.Value).Split('\n')[0].Trim();
                if (string.IsNullOrEmpty(target) || !target.StartsWith("Assets/", StringComparison.Ordinal)) continue;

                if (entries.TryGetValue(folder + "/asset", out var asset))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target) ?? "Assets");
                    File.WriteAllBytes(target, asset);
                    written++;
                }
                else
                {
                    Directory.CreateDirectory(target);
                }

                if (!entries.TryGetValue(folder + "/asset.meta", out var meta)) continue;
                File.WriteAllBytes(target + ".meta", meta);
            }

            return written;
        }

        private static Dictionary<string, byte[]> ReadTarGz(string path)
        {
            var entries = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            using (var file = File.OpenRead(path))
            using (var gzip = new GZipStream(file, CompressionMode.Decompress))
            {
                var header = new byte[512];
                while (ReadExactly(gzip, header, header.Length))
                {
                    string name = Encoding.UTF8.GetString(header, 0, 100).TrimEnd('\0', ' ');
                    if (name.Length == 0) break;

                    long size = ParseOctal(header, 124, 12);
                    char type = (char)header[156];

                    var data = new byte[size];
                    if (size > 0 && !ReadExactly(gzip, data, data.Length)) break;

                    long padding = (512 - size % 512) % 512;
                    if (padding > 0) ReadExactly(gzip, new byte[padding], (int)padding);

                    // '0' and '\0' are regular files; directories and the rest carry no payload.
                    if (type == '0' || type == '\0') entries[name.TrimStart('.', '/')] = data;
                }
            }

            return entries;
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int count)
        {
            int read = 0;
            while (read < count)
            {
                int chunk = stream.Read(buffer, read, count - read);
                if (chunk <= 0) return false;
                read += chunk;
            }

            return true;
        }

        private static long ParseOctal(byte[] header, int offset, int length)
        {
            long value = 0;
            for (int index = offset; index < offset + length; index++)
            {
                byte digit = header[index];
                if (digit < (byte)'0' || digit > (byte)'7') continue;
                value = value * 8 + (digit - '0');
            }

            return value;
        }
    }
}
