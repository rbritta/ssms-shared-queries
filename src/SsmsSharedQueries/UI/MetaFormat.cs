namespace SsmsSharedQueries.UI
{
    /// <summary>
    /// Pure formatting of the per-file line-count label shown in the tree, split out so the
    /// pluralization and the committed-to-current delta can be unit-tested. Uses an arrow (not a
    /// dash) for the delta.
    /// </summary>
    internal static class MetaFormat
    {
        /// <summary>"1 line" (singular) or "N lines".</summary>
        public static string Lines(int current) => $"{current} {(current == 1 ? "line" : "lines")}";

        /// <summary>For an edited-but-unsubmitted file whose committed count differs, "15 → 13 lines"
        /// (singular unit when the current count is 1); otherwise just the current count.</summary>
        public static string Lines(int committed, int current, bool modified)
            => modified && committed != current
                ? $"{committed} → {current} {(current == 1 ? "line" : "lines")}"
                : Lines(current);
    }
}
