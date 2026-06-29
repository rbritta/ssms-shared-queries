using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SsmsSharedQueries.UI
{
    /// <summary>
    /// Per-folder metadata kept in a hidden ".ssq" file inside each folder (committed to
    /// the repo, so colors/preferences are shared with the team). Format is simple
    /// key=value lines, e.g. "color=#FF8800" or "lock=report.sql|alice", so it stays easy
    /// to extend. Creating the file also gives an otherwise-empty folder something for git
    /// to track.
    /// </summary>
    internal static class FolderMeta
    {
        public const string FileName = ".ssq";

        private const string ColorKey = "color=";
        private const string LockKey = "lock=";
        private const string DeprecatedKey = "deprecated=";

        /// <summary>
        /// Write the .ssq atomically (temp file + replace) so a concurrent reader never sees a
        /// half-written file and two near-simultaneous edits cannot truncate each other.
        /// </summary>
        private static void WriteAllLinesAtomic(string path, IEnumerable<string> lines)
        {
            var tmp = path + ".tmp";
            File.WriteAllLines(tmp, lines);
            if (File.Exists(path)) File.Replace(tmp, path, null);
            else File.Move(tmp, path);
        }

        // ---- color: a single "color=<hex>" line ----------------------------

        public static string ReadColor(string folderPath)
        {
            try
            {
                var f = Path.Combine(folderPath, FileName);
                if (!File.Exists(f)) return null;
                foreach (var line in File.ReadAllLines(f))
                {
                    var t = line.Trim();
                    if (t.StartsWith(ColorKey, StringComparison.OrdinalIgnoreCase))
                    {
                        var v = t.Substring(ColorKey.Length).Trim();
                        return string.IsNullOrWhiteSpace(v) ? null : v;
                    }
                }
            }
            catch { }
            return null;
        }

        public static void WriteColor(string folderPath, string hex)
        {
            var f = Path.Combine(folderPath, FileName);
            var lines = new List<string>();
            if (File.Exists(f))
                lines = File.ReadAllLines(f)
                    .Where(l => !l.TrimStart().StartsWith(ColorKey, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            if (!string.IsNullOrWhiteSpace(hex)) lines.Add(ColorKey + hex);
            WriteAllLinesAtomic(f, lines);
        }

        /// <summary>Create the .ssq file if missing, seeding it with an inherited color.</summary>
        public static void EnsureFile(string folderPath, string inheritColor)
        {
            var f = Path.Combine(folderPath, FileName);
            if (File.Exists(f)) return;
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(inheritColor)) lines.Add(ColorKey + inheritColor);
            WriteAllLinesAtomic(f, lines);
        }

        // ---- locks: "lock=<fileName>|<user>" lines -------------------------

        /// <summary>fileName -> locker (for every locked file in this folder).</summary>
        public static Dictionary<string, string> GetLocks(string folderPath) => ReadEntries(folderPath, LockKey);

        public static string GetLock(string folderPath, string fileName)
            => GetLocks(folderPath).TryGetValue(fileName, out var u) ? u : null;

        public static void SetLock(string folderPath, string fileName, string user)
            => SetEntry(folderPath, LockKey, fileName, user);

        public static void RemoveLock(string folderPath, string fileName)
            => RemoveEntry(folderPath, LockKey, fileName);

        // ---- deprecations: "deprecated=<fileName>|<user>" lines ------------

        /// <summary>fileName -> who deprecated it (for every deprecated file in this folder).</summary>
        public static Dictionary<string, string> GetDeprecations(string folderPath) => ReadEntries(folderPath, DeprecatedKey);

        public static string GetDeprecation(string folderPath, string fileName)
            => GetDeprecations(folderPath).TryGetValue(fileName, out var u) ? u : null;

        public static void SetDeprecation(string folderPath, string fileName, string user)
            => SetEntry(folderPath, DeprecatedKey, fileName, user);

        public static void RemoveDeprecation(string folderPath, string fileName)
            => RemoveEntry(folderPath, DeprecatedKey, fileName);

        // ---- per-file metadata bookkeeping on move / rename / delete -------

        /// <summary>
        /// Move a file's per-file metadata (its lock and deprecated mark) from one folder's
        /// .ssq to another's, so it travels with the file on move and back on a move-discard.
        /// No-op when the file has no per-file metadata (the common case).
        /// </summary>
        public static void MoveFileMeta(string srcFolder, string destFolder, string fileName)
        {
            if (string.Equals(srcFolder, destFolder, StringComparison.OrdinalIgnoreCase)) return;
            var locker = GetLock(srcFolder, fileName);
            if (locker != null) { RemoveLock(srcFolder, fileName); SetLock(destFolder, fileName, locker); }
            var dep = GetDeprecation(srcFolder, fileName);
            if (dep != null) { RemoveDeprecation(srcFolder, fileName); SetDeprecation(destFolder, fileName, dep); }
        }

        /// <summary>
        /// Carry a file's per-file metadata (lock + deprecated mark) to a new name within the
        /// SAME folder, on rename. Mirrors <see cref="MoveFileMeta"/> (which is for cross-folder
        /// moves and early-returns when src == dest folder).
        /// </summary>
        public static void RenameFileMeta(string folder, string oldName, string newName)
        {
            if (string.Equals(oldName, newName, StringComparison.OrdinalIgnoreCase)) return;
            var locker = GetLock(folder, oldName);
            if (locker != null) { RemoveLock(folder, oldName); SetLock(folder, newName, locker); }
            var dep = GetDeprecation(folder, oldName);
            if (dep != null) { RemoveDeprecation(folder, oldName); SetDeprecation(folder, newName, dep); }
        }

        /// <summary>Remove a file's per-file metadata (lock + deprecated mark) on delete, so no orphan
        /// line survives in the folder's .ssq to mis-mark a future same-named file.</summary>
        public static void RemoveFileMeta(string folder, string fileName)
        {
            RemoveLock(folder, fileName);
            RemoveDeprecation(folder, fileName);
        }

        // ---- shared "<key><fileName>|<value>" entry helpers ----------------

        /// <summary>fileName -> value, for every "<paramref name="key"/><fileName>|<value>" line.</summary>
        private static Dictionary<string, string> ReadEntries(string folderPath, string key)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                var f = Path.Combine(folderPath, FileName);
                if (!File.Exists(f)) return d;
                foreach (var line in File.ReadAllLines(f))
                {
                    var t = line.Trim();
                    if (!t.StartsWith(key, StringComparison.OrdinalIgnoreCase)) continue;
                    var v = t.Substring(key.Length);
                    var bar = v.IndexOf('|');
                    if (bar > 0) d[v.Substring(0, bar)] = v.Substring(bar + 1);
                    else if (v.Length > 0) d[v] = string.Empty;
                }
            }
            catch { }
            return d;
        }

        /// <summary>Set (replacing any existing) the entry for one file under <paramref name="key"/>.</summary>
        private static void SetEntry(string folderPath, string key, string fileName, string value)
        {
            var f = Path.Combine(folderPath, FileName);
            var lines = File.Exists(f) ? File.ReadAllLines(f).ToList() : new List<string>();
            lines = lines.Where(l => !IsEntryLineFor(l, key, fileName)).ToList();
            lines.Add($"{key}{fileName}|{value}");
            WriteAllLinesAtomic(f, lines);
        }

        /// <summary>Remove the entry (if any) for one file under <paramref name="key"/>.</summary>
        private static void RemoveEntry(string folderPath, string key, string fileName)
        {
            var f = Path.Combine(folderPath, FileName);
            if (!File.Exists(f)) return;
            var lines = File.ReadAllLines(f).Where(l => !IsEntryLineFor(l, key, fileName)).ToList();
            WriteAllLinesAtomic(f, lines);
        }

        private static bool IsEntryLineFor(string line, string key, string fileName)
        {
            var t = line.Trim();
            if (!t.StartsWith(key, StringComparison.OrdinalIgnoreCase)) return false;
            var v = t.Substring(key.Length);
            var bar = v.IndexOf('|');
            var fn = bar > 0 ? v.Substring(0, bar) : v;
            return string.Equals(fn, fileName, StringComparison.OrdinalIgnoreCase);
        }
    }
}
