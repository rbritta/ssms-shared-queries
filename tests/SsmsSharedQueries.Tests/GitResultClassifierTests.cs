using SsmsSharedQueries.Git;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class GitResultClassifierTests
    {
        [Theory]
        [InlineData("CONFLICT (content): Merge conflict in a.sql", "")]      // stderr marker
        [InlineData("", "error: could not apply 1a2b3c... work")]           // stdout marker
        [InlineData("hint: Resolve all conflicts manually", "")]            // case-insensitive phrase
        [InlineData("conflict", "")]                                        // lowercase
        public void IsConflict_true_on_conflict_markers(string stdErr, string stdOut)
        {
            Assert.True(GitResultClassifier.IsConflict(stdErr, stdOut));
        }

        [Theory]
        [InlineData("Everything up-to-date", "")]
        [InlineData("", "1 file changed")]
        [InlineData("error: failed to push some refs (protected branch)", "")]
        public void IsConflict_false_on_non_conflict_output(string stdErr, string stdOut)
        {
            Assert.False(GitResultClassifier.IsConflict(stdErr, stdOut));
        }

        [Fact]
        public void IsConflict_null_inputs_are_false()
        {
            Assert.False(GitResultClassifier.IsConflict(null, null));
        }
    }
}
