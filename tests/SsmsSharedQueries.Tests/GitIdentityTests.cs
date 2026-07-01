using SsmsSharedQueries.Git;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class GitIdentityTests
    {
        [Theory]
        [InlineData("Ana Paula <ana@x.com>", "Ana Paula")]
        [InlineData("Dev User <dev@example.com>", "Dev User")]
        [InlineData("nomail", "nomail")]                 // no email -> whole string, trimmed
        [InlineData("  Spaced  <s@x>", "Spaced")]
        public void DisplayName_takes_the_name_before_the_email(string identity, string expected)
        {
            Assert.Equal(expected, GitIdentity.DisplayName(identity));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(" <only@email>")]                    // blank name part -> null
        public void DisplayName_returns_null_when_there_is_no_name(string identity)
        {
            Assert.Null(GitIdentity.DisplayName(identity));
        }
    }
}
