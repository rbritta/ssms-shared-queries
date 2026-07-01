using SsmsSharedQueries.UI;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class MetaFormatTests
    {
        [Theory]
        [InlineData(1, "1 line")]     // singular
        [InlineData(0, "0 lines")]
        [InlineData(2, "2 lines")]
        [InlineData(205, "205 lines")]
        public void Lines_pluralizes_on_count(int current, string expected)
        {
            Assert.Equal(expected, MetaFormat.Lines(current));
        }

        [Theory]
        // modified with a changed count -> "committed -> current unit"
        [InlineData(15, 13, true, "15 → 13 lines")]
        [InlineData(3, 1, true, "3 → 1 line")]        // singular on the current side
        // not modified, or committed == current -> just the current count
        [InlineData(15, 13, false, "13 lines")]
        [InlineData(7, 7, true, "7 lines")]
        [InlineData(1, 1, true, "1 line")]
        public void Lines_shows_delta_only_when_modified_and_changed(int committed, int current, bool modified, string expected)
        {
            Assert.Equal(expected, MetaFormat.Lines(committed, current, modified));
        }
    }
}
