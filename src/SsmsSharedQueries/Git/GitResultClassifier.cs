namespace SsmsSharedQueries.Git
{
    /// <summary>
    /// Pure classification of git command output, split out so the fragile substring matching
    /// against git's (localized, version-dependent) messages can be unit-tested. It gates whether
    /// Submit offers conflict resolution instead of a generic error, so it is worth pinning.
    /// </summary>
    internal static class GitResultClassifier
    {
        /// <summary>True when the combined stderr/stdout indicate a merge/rebase conflict.</summary>
        public static bool IsConflict(string stdErr, string stdOut)
        {
            var t = ((stdErr ?? string.Empty) + (stdOut ?? string.Empty)).ToLowerInvariant();
            return t.Contains("conflict") || t.Contains("could not apply") || t.Contains("resolve all conflicts");
        }
    }
}
