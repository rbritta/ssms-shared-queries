using System;
using System.Collections.Generic;

namespace SsmsSharedQueries.Git
{
    /// <summary>
    /// Pure parsing of git plumbing output (porcelain status, log, show). Split out of
    /// <see cref="GitService"/> so the fiddly, edge-case-heavy parsing can be unit-tested
    /// without a real repository or spawning a process.
    /// </summary>
    internal static class GitStatusParser
    {
        /// <summary>
        /// Repo-relative paths of changed/untracked files from `status --porcelain` lines
        /// (each line is "XY &lt;path&gt;", or "XY &lt;old&gt; -&gt; &lt;new&gt;" for a staged rename).
        /// </summary>
        public static HashSet<string> ParseChangedRelPaths(IEnumerable<string> statusLines)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (statusLines == null) return set;
            foreach (var line in statusLines)
            {
                if (line == null || line.Length < 4) continue;
                set.Add(ExtractPath(line));
            }
            return set;
        }

        /// <summary>Repo-relative paths of tracked files deleted in the working tree
        /// (a 'D' in either the staged or unstaged status column).</summary>
        public static List<string> ParseDeletedRelPaths(IEnumerable<string> statusLines)
        {
            var list = new List<string>();
            if (statusLines == null) return list;
            foreach (var line in statusLines)
            {
                if (line == null || line.Length < 4) continue;
                if (line[0] != 'D' && line[1] != 'D') continue;
                list.Add(ExtractPath(line));
            }
            return list;
        }

        // "XY <path>"  ->  <path>  (and "<old> -> <new>" -> <new>), unquoted.
        private static string ExtractPath(string line)
        {
            var p = line.Substring(3).Trim();
            var arrow = p.IndexOf(" -> ", StringComparison.Ordinal);
            if (arrow >= 0) p = p.Substring(arrow + 4);
            return p.Trim().Trim('"');
        }

        /// <summary>
        /// Count lines the way <c>File.ReadAllLines</c> does (a trailing newline adds no extra
        /// line). The captured git stdout terminates every output line with a newline, so the
        /// number of '\n' equals the File.ReadAllLines length used for the working-tree count.
        /// </summary>
        public static int CountNewlines(string text)
        {
            int n = 0;
            if (text != null) foreach (var ch in text) if (ch == '\n') n++;
            return n;
        }

        /// <summary>
        /// Parse the output of
        /// <c>log --no-renames --date=short --pretty=format:%x01%an%x02%ad --name-only</c>
        /// into a per-path <see cref="FileMeta"/>: Creator/CreateDate from the oldest commit
        /// touching the path, LastAuthor/LastDate from the newest.
        /// </summary>
        public static Dictionary<string, FileMeta> ParseHistoryMap(string logOutput)
        {
            var map = new Dictionary<string, FileMeta>(StringComparer.OrdinalIgnoreCase);
            string author = null, date = null;
            foreach (var raw in (logOutput ?? string.Empty).Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line[0] == '\x01')
                {
                    var rest = line.Substring(1);
                    var sep = rest.IndexOf('\x02');
                    if (sep >= 0) { author = rest.Substring(0, sep); date = rest.Substring(sep + 1); }
                }
                else
                {
                    var path = line.Trim();
                    if (path.Length == 0) continue;
                    if (!map.TryGetValue(path, out var m)) { m = new FileMeta(); map[path] = m; }
                    if (m.LastAuthor == null) { m.LastAuthor = author; m.LastDate = date; } // newest occurrence
                    m.Creator = author; m.CreateDate = date;                                 // ends at oldest
                }
            }
            return map;
        }
    }
}
