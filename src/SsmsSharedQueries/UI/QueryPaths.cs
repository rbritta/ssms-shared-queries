using System;
using System.IO;

namespace SsmsSharedQueries.UI
{
    /// <summary>
    /// Pure path helpers used by the panel. Split out of QueryPanelControl so the boundary
    /// cases (path under/at/outside a root, separator handling, name sanitizing, unique-name
    /// generation) can be unit-tested.
    /// </summary>
    internal static class QueryPaths
    {
        /// <summary>
        /// <paramref name="fullPath"/> expressed relative to <paramref name="root"/> with
        /// forward slashes; falls back to just the file name when it is not under the root.
        /// </summary>
        public static string MakeRelative(string root, string fullPath)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            var target = Path.GetFullPath(fullPath);
            if (target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                return target.Substring(rootFull.Length).Replace('\\', '/');
            return Path.GetFileName(fullPath);
        }

        /// <summary>Strip characters that are illegal in a file name and trim whitespace.</summary>
        public static string SanitizeName(string name)
        {
            name = (name ?? string.Empty).Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c.ToString(), string.Empty);
            return name;
        }

        /// <summary>A path in <paramref name="parent"/> for <paramref name="baseName"/>+<paramref name="ext"/>
        /// that does not yet exist, appending " 2", " 3", ... when needed.</summary>
        public static string UniquePath(string parent, string baseName, string ext)
        {
            var p = Path.Combine(parent, baseName + ext);
            int i = 2;
            while (File.Exists(p) || Directory.Exists(p)) { p = Path.Combine(parent, $"{baseName} {i}{ext}"); i++; }
            return p;
        }

        /// <summary>True when <paramref name="target"/> is <paramref name="folder"/> itself or a
        /// descendant of it (used to block dropping a folder into its own subtree).</summary>
        public static bool IsSameOrDescendant(string target, string folder)
        {
            var f = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar);
            var t = Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
            return t.Equals(f, StringComparison.OrdinalIgnoreCase)
                || t.StartsWith(f + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
    }
}
