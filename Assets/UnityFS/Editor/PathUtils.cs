using System.Collections.Generic;
using System.IO;

namespace UnityFS.Editor
{
    public static class PathUtils
    {
        private static HashSet<string> UnsupportedExts = new HashSet<string>(new string[]
        {
            ".xlsx", ".xlsm", ".xls", ".docx", ".doc"
        });

        public static bool UnrecognizedAsset(string file)
        {
            var fi = new FileInfo(file);
            return UnsupportedExts.Contains(fi.Extension.ToLower());
        }

        public static string ReplaceFileExt(string fileName, string oldSuffix, string newSuffix)
        {
            if (fileName.EndsWith(oldSuffix))
            {
                return fileName.Substring(0, fileName.Length - oldSuffix.Length) + newSuffix;
            }

            return fileName;
        }

        public static void CleanupDirectoryRecursively(string parent)
        {
            if (Directory.Exists(parent))
            {
                foreach (var child in Directory.GetDirectories(parent))
                {
                    CleanupDirectoryRecursively(child);
                }

                foreach (var file in Directory.GetFiles(parent))
                {
                    File.Delete(file);
                }

                Directory.Delete(parent);
            }
        }
    }
}