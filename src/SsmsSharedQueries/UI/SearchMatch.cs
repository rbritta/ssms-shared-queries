using System;
using System.Text;

namespace SsmsSharedQueries.UI
{
    /// <summary>
    /// Pure search-matching rules for the query tree, split out so they can be unit-tested without
    /// WPF or the file system. All matching is case-insensitive.
    /// <para>
    /// File NAMES are matched separator-insensitively (see <see cref="LooseMatchSpan"/>) so that,
    /// e.g., "allt" finds "all-tables". File BODIES are matched literally (<see cref="Contains"/>),
    /// since stripping separators from code would over-match across whitespace and punctuation.
    /// </para>
    /// </summary>
    internal static class SearchMatch
    {
        /// <summary>Literal, case-insensitive substring test. A null/empty term or null text never matches.</summary>
        public static bool Contains(string text, string term)
            => !string.IsNullOrEmpty(term)
               && text != null
               && text.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0;

        // Characters treated as "noise" between words in a name; ignored on both sides of a match.
        private static bool IsSeparator(char c) => c == '-' || c == '_' || c == '.' || char.IsWhiteSpace(c);

        /// <summary>Case- and separator-insensitive containment: "allt" matches "all-tables".</summary>
        public static bool LooseContains(string text, string term)
            => LooseMatchSpan(text, term, out _, out _);

        /// <summary>
        /// Find the first case/separator-insensitive occurrence of <paramref name="term"/> in
        /// <paramref name="text"/> (separators - _ . and whitespace are skipped in both). On success
        /// returns true with [<paramref name="start"/>, start+<paramref name="length"/>) covering the
        /// matched region in the ORIGINAL text, so a caller can highlight exactly those characters.
        /// </summary>
        public static bool LooseMatchSpan(string text, string term, out int start, out int length)
        {
            start = -1; length = 0;
            if (string.IsNullOrEmpty(term) || text == null) return false;
            var t = Strip(term);
            if (t.Length == 0) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (IsSeparator(text[i])) continue; // a match starts on a real character
                int ti = 0, j = i, last = i;
                while (j < text.Length && ti < t.Length)
                {
                    var c = text[j];
                    if (IsSeparator(c)) { j++; continue; } // skip separators inside the text
                    if (char.ToUpperInvariant(c) == char.ToUpperInvariant(t[ti])) { last = j; ti++; j++; }
                    else break;
                }
                if (ti == t.Length) { start = i; length = last - i + 1; return true; }
            }
            return false;
        }

        private static string Strip(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var c in s) if (!IsSeparator(c)) sb.Append(c);
            return sb.ToString();
        }
    }
}
