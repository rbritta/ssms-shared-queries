using SsmsSharedQueries.UI;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class SearchMatchTests
    {
        // ---- Contains: literal substring, used for file BODIES ----------------------------

        [Theory]
        [InlineData("sessions", "active_sessions.sql", true)]
        [InlineData("SESSIONS", "active_sessions.sql", true)] // case-insensitive
        [InlineData("orders", "active_sessions.sql", false)]
        [InlineData("", "anything.sql", false)]               // empty term never "contains"
        public void Contains_is_case_insensitive_substring(string term, string text, bool expected)
        {
            Assert.Equal(expected, SearchMatch.Contains(text, term));
        }

        [Fact]
        public void Contains_null_text_is_false()
        {
            Assert.False(SearchMatch.Contains(null, "x"));
        }

        [Fact]
        public void Contains_is_literal_so_a_hyphen_breaks_the_match()
        {
            // Body matching stays literal: "allt" does NOT match the text "all-tables".
            Assert.False(SearchMatch.Contains("select * from all-tables", "allt"));
            Assert.True(SearchMatch.Contains("select * from all-tables", "all-tables"));
        }

        // ---- LooseContains / LooseMatchSpan: separator-insensitive, used for file NAMES ----

        [Theory]
        [InlineData("allt", "all-tables.sql", true)]  // the reported case: hyphen ignored
        [InlineData("allt", "all_tables.sql", true)]  // underscore ignored
        [InlineData("allt", "all tables.sql", true)]  // space ignored
        [InlineData("allt", "alltables.sql", true)]   // no separator at all
        [InlineData("ALLT", "all-tables.sql", true)]  // case-insensitive
        [InlineData("alltables", "all-tables.sql", true)]
        [InlineData("tables", "all-tables.sql", true)] // matches the second word
        [InlineData("all-t", "alltables.sql", true)]   // separators in the TERM are ignored too
        [InlineData("xyz", "all-tables.sql", false)]
        [InlineData("allt", "report.sql", false)]
        [InlineData("allz", "all-tables.sql", false)]  // must be contiguous ignoring separators, not a subsequence
        public void LooseContains_ignores_separators(string term, string name, bool expected)
        {
            Assert.Equal(expected, SearchMatch.LooseContains(name, term));
        }

        [Fact]
        public void LooseContains_empty_or_null_is_false()
        {
            Assert.False(SearchMatch.LooseContains("all-tables.sql", ""));
            Assert.False(SearchMatch.LooseContains("all-tables.sql", null));
            Assert.False(SearchMatch.LooseContains(null, "allt"));
            Assert.False(SearchMatch.LooseContains("all-tables.sql", "---")); // term is only separators
        }

        [Fact]
        public void LooseMatchSpan_covers_the_original_characters_including_inner_separators()
        {
            // "allt" in "all-tables" spans "all-t" (indices 0..4) so the caller highlights exactly that.
            Assert.True(SearchMatch.LooseMatchSpan("all-tables", "allt", out int start, out int length));
            Assert.Equal(0, start);
            Assert.Equal(5, length);
            Assert.Equal("all-t", "all-tables".Substring(start, length));
        }

        [Fact]
        public void LooseMatchSpan_finds_a_match_that_does_not_start_at_index_zero()
        {
            Assert.True(SearchMatch.LooseMatchSpan("xy-all-tables", "allt", out int start, out int length));
            Assert.Equal("all-t", "xy-all-tables".Substring(start, length));
        }

        [Fact]
        public void LooseMatchSpan_no_match_reports_negative_start()
        {
            Assert.False(SearchMatch.LooseMatchSpan("report.sql", "allt", out int start, out int length));
            Assert.Equal(-1, start);
            Assert.Equal(0, length);
        }
    }
}
