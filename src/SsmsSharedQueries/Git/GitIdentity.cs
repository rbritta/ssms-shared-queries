using System;

namespace SsmsSharedQueries.Git
{
    /// <summary>Pure parsing of a git identity string ("Name &lt;email&gt;"), split out so the edge
    /// cases can be unit-tested without a repository.</summary>
    internal static class GitIdentity
    {
        /// <summary>
        /// The display name from a <c>Name &lt;email&gt;</c> identity: the part before " &lt;",
        /// trimmed. Returns null when the identity is empty or the name part is blank (e.g.
        /// <c>&lt;only@email&gt;</c>).
        /// </summary>
        public static string DisplayName(string identity)
        {
            if (string.IsNullOrWhiteSpace(identity)) return null;
            var lt = identity.IndexOf(" <", StringComparison.Ordinal);
            var name = (lt >= 0 ? identity.Substring(0, lt) : identity).Trim();
            return name.Length == 0 ? null : name;
        }
    }
}
