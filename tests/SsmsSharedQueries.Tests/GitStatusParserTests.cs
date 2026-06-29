using System.Linq;
using SsmsSharedQueries.Git;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class GitStatusParserTests
    {
        // The history pretty-format delimits author/date with the SOH/STX control chars
        // (git's %x01 / %x02). Build them by code point to avoid any escape ambiguity.
        private static readonly string Soh = ((char)1).ToString();
        private static readonly string Stx = ((char)2).ToString();

        [Fact]
        public void ParseChangedRelPaths_includes_modified_and_untracked()
        {
            var set = GitStatusParser.ParseChangedRelPaths(new[] { " M a.sql", "?? b.sql" });
            Assert.Equal(new[] { "a.sql", "b.sql" }, set.OrderBy(x => x));
        }

        [Fact]
        public void ParseChangedRelPaths_staged_rename_yields_new_path()
        {
            var set = GitStatusParser.ParseChangedRelPaths(new[] { "R  old.sql -> new.sql" });
            Assert.Equal(new[] { "new.sql" }, set);
        }

        [Fact]
        public void ParseChangedRelPaths_unquotes_paths_with_spaces()
        {
            var set = GitStatusParser.ParseChangedRelPaths(new[] { "?? \"name with space.sql\"" });
            Assert.Contains("name with space.sql", set);
        }

        [Fact]
        public void ParseChangedRelPaths_is_case_insensitive_and_dedups()
        {
            var set = GitStatusParser.ParseChangedRelPaths(new[] { " M A.sql", " M a.sql" });
            Assert.Single(set);
        }

        [Fact]
        public void ParseChangedRelPaths_skips_short_and_null_lines()
        {
            var set = GitStatusParser.ParseChangedRelPaths(new[] { null, "", "x", " M ok.sql" });
            Assert.Equal(new[] { "ok.sql" }, set);
        }

        [Fact]
        public void ParseChangedRelPaths_null_input_is_empty()
        {
            Assert.Empty(GitStatusParser.ParseChangedRelPaths(null));
        }

        [Fact]
        public void ParseDeletedRelPaths_detects_staged_and_unstaged_deletions()
        {
            var list = GitStatusParser.ParseDeletedRelPaths(new[] { " D unstaged.sql", "D  staged.sql" });
            Assert.Equal(new[] { "unstaged.sql", "staged.sql" }, list);
        }

        [Fact]
        public void ParseDeletedRelPaths_ignores_modified_and_added()
        {
            var list = GitStatusParser.ParseDeletedRelPaths(new[] { " M m.sql", "?? n.sql", "A  a.sql" });
            Assert.Empty(list);
        }

        [Theory]
        [InlineData("", 0)]
        [InlineData("a\n", 1)]
        [InlineData("a\nb\n", 2)]
        [InlineData("a\nb\n\n", 3)]
        public void CountNewlines_counts_newlines(string capturedStdout, int expected)
        {
            // GitService captures stdout with AppendLine, so a file of N lines arrives as N
            // newline-terminated lines; counting '\n' equals File.ReadAllLines(file).Length.
            Assert.Equal(expected, GitStatusParser.CountNewlines(capturedStdout));
        }

        [Fact]
        public void CountNewlines_null_is_zero()
        {
            Assert.Equal(0, GitStatusParser.CountNewlines(null));
        }

        [Fact]
        public void ParseHistoryMap_resolves_oldest_creator_and_newest_author()
        {
            // git log is newest-first. q.sql touched in 3 commits: newest by Carol, oldest by Alice.
            var log = string.Join("\n", new[]
            {
                Soh + "Carol" + Stx + "2026-03-01",
                "q.sql",
                Soh + "Bob" + Stx + "2026-02-01",
                "q.sql",
                "r.sql",
                Soh + "Alice" + Stx + "2026-01-01",
                "q.sql",
            });

            var map = GitStatusParser.ParseHistoryMap(log);

            Assert.Equal("Carol", map["q.sql"].LastAuthor);
            Assert.Equal("2026-03-01", map["q.sql"].LastDate);
            Assert.Equal("Alice", map["q.sql"].Creator);
            Assert.Equal("2026-01-01", map["q.sql"].CreateDate);
            // r.sql only appears in the Bob commit
            Assert.Equal("Bob", map["r.sql"].LastAuthor);
            Assert.Equal("Bob", map["r.sql"].Creator);
        }

        [Fact]
        public void ParseHistoryMap_tolerates_crlf()
        {
            var log = Soh + "Dan" + Stx + "2026-04-01\r\nx.sql\r\n";
            var map = GitStatusParser.ParseHistoryMap(log);
            Assert.Equal("Dan", map["x.sql"].LastAuthor);
        }

        [Fact]
        public void ParseHistoryMap_empty_is_empty()
        {
            Assert.Empty(GitStatusParser.ParseHistoryMap(""));
            Assert.Empty(GitStatusParser.ParseHistoryMap(null));
        }
    }
}
