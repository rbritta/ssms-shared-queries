using System.IO;
using SsmsSharedQueries.UI;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class QueryPathsTests
    {
        [Fact]
        public void MakeRelative_under_root_uses_forward_slashes()
        {
            var root = Path.Combine(Path.GetTempPath(), "repo");
            var full = Path.Combine(root, "sub", "a.sql");
            Assert.Equal("sub/a.sql", QueryPaths.MakeRelative(root, full));
        }

        [Fact]
        public void MakeRelative_is_case_insensitive_on_root()
        {
            var root = Path.Combine(Path.GetTempPath(), "Repo");
            var full = Path.Combine(Path.GetTempPath(), "repo", "a.sql");
            Assert.Equal("a.sql", QueryPaths.MakeRelative(root, full));
        }

        [Fact]
        public void MakeRelative_outside_root_falls_back_to_file_name()
        {
            var root = Path.Combine(Path.GetTempPath(), "repo");
            var full = Path.Combine(Path.GetTempPath(), "elsewhere", "b.sql");
            Assert.Equal("b.sql", QueryPaths.MakeRelative(root, full));
        }

        [Theory]
        [InlineData("a:b*c?.sql", "abc.sql")]
        [InlineData("  spaced  ", "spaced")]
        [InlineData("", "")]
        [InlineData(null, "")]
        public void SanitizeName_strips_invalid_and_trims(string input, string expected)
        {
            Assert.Equal(expected, QueryPaths.SanitizeName(input));
        }

        [Fact]
        public void UniquePath_increments_when_target_exists()
        {
            var dir = Path.Combine(Path.GetTempPath(), "ssq_unique_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var first = QueryPaths.UniquePath(dir, "Query", ".sql");
                Assert.Equal(Path.Combine(dir, "Query.sql"), first);

                File.WriteAllText(first, "");
                var second = QueryPaths.UniquePath(dir, "Query", ".sql");
                Assert.Equal(Path.Combine(dir, "Query 2.sql"), second);
            }
            finally { Directory.Delete(dir, recursive: true); }
        }

        [Fact]
        public void IsSameOrDescendant_true_for_self_and_child()
        {
            var root = Path.Combine(Path.GetTempPath(), "p");
            Assert.True(QueryPaths.IsSameOrDescendant(root, root));
            Assert.True(QueryPaths.IsSameOrDescendant(Path.Combine(root, "child"), root));
            Assert.True(QueryPaths.IsSameOrDescendant(Path.Combine(root, "a", "b"), root));
        }

        [Fact]
        public void IsSameOrDescendant_false_for_parent_and_sibling()
        {
            var root = Path.Combine(Path.GetTempPath(), "p");
            Assert.False(QueryPaths.IsSameOrDescendant(Path.GetTempPath(), root));
            Assert.False(QueryPaths.IsSameOrDescendant(Path.Combine(Path.GetTempPath(), "q"), root));
        }

        [Fact]
        public void IsSameOrDescendant_not_fooled_by_name_prefix()
        {
            // "p2" must not count as inside "p".
            var root = Path.Combine(Path.GetTempPath(), "p");
            var sibling = Path.Combine(Path.GetTempPath(), "p2", "x");
            Assert.False(QueryPaths.IsSameOrDescendant(sibling, root));
        }

        [Theory]
        [InlineData(".git", true)]
        [InlineData(".github", true)]
        [InlineData(".vs", true)]
        [InlineData(".", true)]
        [InlineData("Queries", false)]
        [InlineData("Create", false)]
        [InlineData("v1.0", false)] // dot in the middle is fine
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsHiddenName_only_dot_prefixed(string name, bool expected)
        {
            Assert.Equal(expected, QueryPaths.IsHiddenName(name));
        }

        [Theory]
        [InlineData(".git/config", true)]
        [InlineData(".git\\hooks\\pre-commit", true)]
        [InlineData("Queries/.git/x.sql", true)] // hidden segment anywhere in the path
        [InlineData(".git", true)]                // bare hidden segment, no separator
        [InlineData(".gitignore", true)]          // dotfile directly in the base
        [InlineData(".hidden.sql", true)]         // dot-prefixed .sql at the base root
        [InlineData("Queries/Create/a.sql", false)]
        [InlineData("a.sql", false)]
        [InlineData("", false)]
        [InlineData(null, false)]
        public void HasHiddenSegment_detects_dot_folder_anywhere(string rel, bool expected)
        {
            Assert.Equal(expected, QueryPaths.HasHiddenSegment(rel));
        }
    }
}
