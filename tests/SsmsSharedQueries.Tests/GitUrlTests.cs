using SsmsSharedQueries.Git;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class GitUrlTests
    {
        [Theory]
        [InlineData("https://github.com/o/r", "https://github.com/o/r", true)]        // identical
        [InlineData("https://github.com/o/r", "https://github.com/o/r/", true)]       // trailing slash
        [InlineData("https://github.com/o/r", "https://github.com/o/r.git", true)]    // .git suffix
        [InlineData("https://github.com/o/r.git/", "https://github.com/o/r", true)]   // .git + slash
        [InlineData("https://github.com/o/r ", "https://github.com/o/r", true)]       // trailing space
        [InlineData("  https://github.com/o/r", "https://github.com/o/r", true)]      // leading space
        [InlineData("https://GitHub.com/O/R", "https://github.com/o/r", true)]        // case-insensitive
        [InlineData("https://github.com/o/r", "https://github.com/o/other", false)]   // different repo
        [InlineData("https://github.com/o/r", "https://gitlab.com/o/r", false)]       // different host
        public void SameRepository_treats_equivalent_urls_as_equal(string a, string b, bool expected)
            => Assert.Equal(expected, GitUrl.SameRepository(a, b));

        [Fact]
        public void Normalize_treats_null_as_empty()
            => Assert.Equal(string.Empty, GitUrl.Normalize(null));

        [Fact]
        public void Normalize_strips_trailing_slash_and_git_and_lowercases()
            => Assert.Equal("https://github.com/o/r", GitUrl.Normalize("  https://GitHub.com/o/R.git/  "));

        [Fact]
        public void RepoFolderName_is_stable_for_the_same_url()
            => Assert.Equal(GitUrl.RepoFolderName("https://github.com/o/r"),
                            GitUrl.RepoFolderName("https://github.com/o/r"));

        [Theory]
        [InlineData("https://github.com/o/r/")]      // trailing slash
        [InlineData("https://github.com/o/r.git")]   // .git suffix
        [InlineData("https://GitHub.com/O/R")]       // different case
        [InlineData("  https://github.com/o/r  ")]   // surrounding space
        public void RepoFolderName_is_identical_for_equivalent_urls(string url)
            => Assert.Equal(GitUrl.RepoFolderName("https://github.com/o/r"), GitUrl.RepoFolderName(url));

        [Fact]
        public void RepoFolderName_differs_for_different_repos()
            => Assert.NotEqual(GitUrl.RepoFolderName("https://github.com/o/r"),
                               GitUrl.RepoFolderName("https://github.com/o/other"));

        [Fact]
        public void RepoFolderName_differs_for_same_name_on_different_owners()
            => Assert.NotEqual(GitUrl.RepoFolderName("https://github.com/a/queries"),
                               GitUrl.RepoFolderName("https://github.com/b/queries"));

        [Fact]
        public void RepoFolderName_starts_with_the_repo_name_and_has_a_hash_suffix()
        {
            var f = GitUrl.RepoFolderName("https://github.com/rbritta/ssms-shared-queries-test");
            Assert.Matches("^ssms-shared-queries-test-[0-9a-f]{8}$", f);
        }

        [Fact]
        public void RepoFolderName_has_no_path_separators_or_invalid_chars()
        {
            var f = GitUrl.RepoFolderName("https://dev.azure.com/org/project/_git/My Repo");
            Assert.DoesNotContain('/', f);
            Assert.DoesNotContain('\\', f);
            Assert.DoesNotContain(' ', f);
            Assert.Equal(-1, f.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()));
        }

        [Fact]
        public void RepoFolderName_handles_empty_url()
            => Assert.Matches("^repo-[0-9a-f]{8}$", GitUrl.RepoFolderName(""));
    }
}
