using System;

namespace SsmsSharedQueries.Git
{
    /// <summary>
    /// Pure helpers for comparing git remote URLs. Split out of <see cref="GitService"/> so the
    /// normalization rules (trim, an optional trailing "/" and ".git" suffix, case) can be
    /// unit-tested without a repository. Used to decide whether the local cache already holds
    /// the configured repository or a different one that must be re-cloned.
    /// </summary>
    internal static class GitUrl
    {
        /// <summary>
        /// Normalize a remote URL for comparison: trim surrounding whitespace, drop a trailing
        /// "/" and a ".git" suffix, and lowercase the result.
        /// </summary>
        public static string Normalize(string url)
        {
            url = (url ?? string.Empty).Trim().TrimEnd('/');
            if (url.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) url = url.Substring(0, url.Length - 4);
            return url.ToLowerInvariant();
        }

        /// <summary>True when two remote URLs refer to the same repository.</summary>
        public static bool SameRepository(string a, string b) => Normalize(a) == Normalize(b);

        /// <summary>
        /// A stable, filesystem-safe folder name for the local clone of a repository, derived
        /// from its URL: the repository's own name plus a short hash of the full normalized URL.
        /// Different repositories never collide (the hash disambiguates same-named repos on
        /// different owners/hosts) and the same repository always maps to the same folder, so the
        /// plugin keeps one clone per repo under the cache folder; switching the Repository URL
        /// just looks at a different folder and leaves the previous clone in place.
        /// </summary>
        public static string RepoFolderName(string url)
        {
            var norm = Normalize(url);
            var slash = norm.LastIndexOf('/');
            var name = Sanitize(slash >= 0 ? norm.Substring(slash + 1) : norm);
            if (name.Length == 0) name = "repo";
            if (name.Length > 40) name = name.Substring(0, 40).Trim('-');
            return name + "-" + Hash8(norm);
        }

        private static string Sanitize(string s)
        {
            var chars = s.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                var ch = chars[i];
                var ok = (ch >= 'a' && ch <= 'z') || (ch >= '0' && ch <= '9') || ch == '-' || ch == '_';
                if (!ok) chars[i] = '-';
            }
            var collapsed = new string(chars);
            while (collapsed.Contains("--")) collapsed = collapsed.Replace("--", "-");
            return collapsed.Trim('-');
        }

        /// <summary>FNV-1a 32-bit hash as 8 hex chars. Deterministic across processes (unlike
        /// string.GetHashCode), with no cryptographic dependency - it is only a folder discriminator.</summary>
        private static string Hash8(string s)
        {
            uint h = 2166136261u;
            foreach (var ch in s) { h ^= ch; h *= 16777619u; }
            return h.ToString("x8");
        }
    }
}
