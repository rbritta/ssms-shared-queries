using SsmsSharedQueries.UI;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class AiInstructionsTests
    {
        // ---- guide text ------------------------------------------------------------------

        [Fact]
        public void Guide_states_the_core_rules_and_carries_the_version_marker()
        {
            var g = AiInstructions.BuildGuide("SqlScripts");

            Assert.Contains("SqlScripts", g);                 // names the repo
            Assert.Contains("never touch git", g);                // the golden rule
            Assert.Contains("Submit", g);                         // human submits via the plugin
            Assert.Contains("`.ssq`", g);                         // do not edit metadata
            Assert.Contains("lock=<file name>|<user>", g);        // respect locks
            Assert.Contains("AI assistant", g);                   // addressed to any AI
            Assert.Contains("ssq-ai-guide v" + AiInstructions.GuideVersion, g); // version marker
            Assert.Contains("Delete this line", g);               // the opt-out note (delete the first line)
        }

        [Fact]
        public void Guide_marker_is_the_very_first_line()
        {
            var g = AiInstructions.BuildGuide("Repo");
            var firstLine = g.Split('\n')[0];
            Assert.StartsWith("<!-- ssq-ai-guide v", firstLine);
        }

        [Fact]
        public void Guide_falls_back_when_repo_name_is_missing()
        {
            Assert.Contains("this repository", AiInstructions.BuildGuide(null));
        }

        [Fact]
        public void Guide_never_uses_em_or_en_dashes()
        {
            var g = AiInstructions.BuildGuide("Repo");
            Assert.DoesNotContain("—", g); // em dash
            Assert.DoesNotContain("–", g); // en dash
        }

        // ---- file identification ---------------------------------------------------------

        [Theory]
        [InlineData("CLAUDE.md", true)]
        [InlineData("agents.md", true)] // case-insensitive
        [InlineData("readme.md", false)]
        [InlineData("query.sql", false)]
        public void IsGuideFile_recognizes_the_guide_files(string name, bool expected)
        {
            Assert.Equal(expected, AiInstructions.IsGuideFile(name));
        }

        // ---- version comparison ----------------------------------------------------------

        [Theory]
        [InlineData("1.0.0", "1.1.0", -1)]
        [InlineData("1.1.0", "1.1.0", 0)]
        [InlineData("2.0.0", "1.9.9", 1)]
        [InlineData("1.1", "1.1.0", 0)]     // missing segments are zero
        [InlineData("1.10.0", "1.9.0", 1)]  // numeric, not lexical
        [InlineData("1.x.3", "1.0.3", 0)]   // non-numeric segment -> 0 (ExtractVersion can emit this)
        [InlineData("", "0", 0)]            // empty -> {0}
        [InlineData("1.1.", "1.1.0", 0)]    // trailing dot is a zero segment
        public void CompareVersions_orders_dotted_numbers(string a, string b, int expected)
        {
            Assert.Equal(expected, AiInstructions.CompareVersions(a, b));
        }

        // ---- orphan-guide classification -------------------------------------------------

        [Theory]
        [InlineData("Queries/CLAUDE.md", "Queries", false)]        // the canonical guide -> keep
        [InlineData("Queries/AGENTS.md", "Queries", false)]        // mirror in base -> keep
        [InlineData("CLAUDE.md", "Queries", true)]                 // at the git root, base is a subfolder -> orphan
        [InlineData("Queries/Create/CLAUDE.md", "Queries", true)]  // in a subfolder of base -> orphan
        [InlineData("QUERIES/CLAUDE.md", "queries", false)]        // dir compare is case-insensitive
        [InlineData("Queries/active_sessions.sql", "Queries", false)] // not a guide -> keep
        [InlineData("CLAUDE.md", "", false)]                       // root base: root guide is canonical
        [InlineData("sub/CLAUDE.md", "", true)]                    // root base: a subfolder guide is an orphan
        public void IsOrphanGuide_flags_guides_outside_the_base_folder(string rel, string baseRel, bool expected)
        {
            Assert.Equal(expected, AiInstructions.IsOrphanGuide(rel, baseRel));
        }

        [Fact]
        public void IsOrphanGuide_empty_path_is_not_an_orphan()
        {
            Assert.False(AiInstructions.IsOrphanGuide(null, "Queries"));
            Assert.False(AiInstructions.IsOrphanGuide("", "Queries"));
        }

        // ---- auto-write decision ---------------------------------------------------------

        [Fact]
        public void ShouldWrite_creates_when_missing()
        {
            Assert.True(AiInstructions.ShouldWrite(null, "1.1.0"));
        }

        [Fact]
        public void ShouldWrite_updates_an_older_managed_guide_only()
        {
            var older = "<!-- ssq-ai-guide v1.0.0 -->\n\n# guide";
            var same = "<!-- ssq-ai-guide v1.1.0 -->\n\n# guide";
            var newer = "<!-- ssq-ai-guide v2.0.0 -->\n\n# guide";

            Assert.True(AiInstructions.ShouldWrite(older, "1.1.0"));   // older -> replace
            Assert.False(AiInstructions.ShouldWrite(same, "1.1.0"));   // same  -> leave
            Assert.False(AiInstructions.ShouldWrite(newer, "1.1.0"));  // newer -> leave (do not downgrade)
        }

        [Fact]
        public void ShouldWrite_never_overwrites_a_file_whose_marker_line_was_removed()
        {
            // Opting out = deleting the first (version marker) line. Then there is no marker, so the
            // file is treated as hand-authored and left untouched even by a much newer version.
            var optedOut = "# my tweaked guide\n\nno marker here anymore";
            var handAuthored = "# My own notes\n\nnothing managed here";

            Assert.False(AiInstructions.ShouldWrite(optedOut, "9.9.9"));
            Assert.False(AiInstructions.ShouldWrite(handAuthored, "9.9.9"));
        }

        [Fact]
        public void ShouldWrite_round_trips_with_BuildGuide()
        {
            var current = AiInstructions.BuildGuide("Repo", "1.1.0");
            Assert.False(AiInstructions.ShouldWrite(current, "1.1.0")); // freshly written -> no rewrite
            Assert.True(AiInstructions.ShouldWrite(current, "1.2.0"));  // a newer version would replace it

            var old = AiInstructions.BuildGuide("Repo", "1.0.0");
            Assert.True(AiInstructions.ShouldWrite(old, "1.1.0"));
        }
    }
}
