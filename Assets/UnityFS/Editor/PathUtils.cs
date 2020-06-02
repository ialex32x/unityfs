using System.Collections.Generic;
using System.IO;

namespace UnityFS.Editor
{
    public static class PathUtils
    {
        // private static HashSet<string> UnsupportedExts = new HashSet<string>(new string[]
        // {
        //     ".xlsx", ".xlsm", ".xls", ".docx", ".doc", ".cs"
        // });
        //
        // public static bool UnrecognizedAsset(string file)
        // {
        //     var fi = new FileInfo(file);
        //     return UnsupportedExts.Contains(fi.Extension.ToLower());
        // }

        public static string GetFileSizeString(long size)
        {
            if (size > 0)
            {
                if (size > 1024 * 1024)
                {
                    return string.Format("{0:.0} MB", size / (1024.0 * 1024.0));
                }

                if (size > 1024)
                {
                    return string.Format("{0:.0} KB", size / 1024.0);
                }

                return string.Format("{0} B", size);
            }

            return "N/A";
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