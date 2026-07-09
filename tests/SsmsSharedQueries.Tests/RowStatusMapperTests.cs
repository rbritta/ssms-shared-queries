using SsmsSharedQueries.UI;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    public class RowStatusMapperTests
    {
        [Theory]
        [InlineData("?? a.sql", "new", "New", "a.sql")]
        [InlineData(" M a.sql", "modified", "Modified", "a.sql")]
        [InlineData(" D a.sql", "deleted", "Deleted", "a.sql")]
        [InlineData("A  a.sql", "new", "New", "a.sql")]
        public void Map_basic_file_states(string line, string label, string kind, string path)
        {
            var row = RowStatusMapper.Map(line);
            Assert.Equal(label, row.Label);
            Assert.Equal(kind, row.Kind.ToString());
            Assert.Equal(path, row.Path);
        }

        [Fact]
        public void Map_rename_shows_new_side_and_renamed_label()
        {
            var row = RowStatusMapper.Map("R  old.sql -> new.sql");
            Assert.Equal("renamed", row.Label);
            Assert.Equal(RowKind.Renamed, row.Kind);
            Assert.Equal("new.sql", row.Path);
        }

        [Fact]
        public void Map_added_ssq_marker_is_folder_settings()
        {
            // A .ssq is no longer seeded on folder creation, so an added marker is a settings change,
            // not a "new folder" (a genuinely new folder shows up via its .sql files instead).
            var row = RowStatusMapper.Map("?? queries/reports/.ssq");
            Assert.Equal("folder settings", row.Label);
            Assert.Equal(RowKind.Modified, row.Kind);
            Assert.Equal("queries/reports", row.Path);
        }

        [Fact]
        public void Map_deleted_ssq_marker_is_folder_settings_when_the_folder_survives()
        {
            // Clearing a folder's last setting deletes the .ssq but the folder (and its .sql) remain.
            var row = RowStatusMapper.Map(" D queries/ops/.ssq", rel => true);
            Assert.Equal("folder settings", row.Label);
            Assert.Equal(RowKind.Modified, row.Kind);
            Assert.Equal("queries/ops", row.Path);
        }

        [Fact]
        public void Map_deleted_ssq_marker_is_deleted_folder_when_the_folder_is_gone()
        {
            var row = RowStatusMapper.Map(" D queries/ops/.ssq", rel => false);
            Assert.Equal("deleted folder", row.Label);
            Assert.Equal(RowKind.Deleted, row.Kind);
            Assert.Equal("queries/ops", row.Path);
        }

        [Fact]
        public void Map_deleted_ssq_marker_without_probe_defaults_to_folder_settings()
        {
            // With no folder-existence probe we cannot claim the folder is gone, so we must not
            // falsely report "deleted folder".
            var row = RowStatusMapper.Map(" D queries/ops/.ssq");
            Assert.Equal("folder settings", row.Label);
            Assert.Equal(RowKind.Modified, row.Kind);
        }

        [Fact]
        public void Map_modified_ssq_marker_becomes_folder_settings()
        {
            var row = RowStatusMapper.Map(" M queries/ops/.ssq");
            Assert.Equal("folder settings", row.Label);
            Assert.Equal(RowKind.Modified, row.Kind);
            Assert.Equal("queries/ops", row.Path);
        }

        [Fact]
        public void Map_root_ssq_marker_shows_root_placeholder()
        {
            var row = RowStatusMapper.Map("?? .ssq");
            Assert.Equal("folder settings", row.Label);
            Assert.Equal("(root)", row.Path);
        }

        [Fact]
        public void Map_deleted_root_ssq_stays_folder_settings_and_is_not_probed()
        {
            // The base folder never "goes away"; a deleted root .ssq is just a settings clear,
            // never "deleted folder" - and the "(root)" placeholder must not be probed as a path.
            bool probed = false;
            var row = RowStatusMapper.Map(" D .ssq", rel => { probed = true; return false; });
            Assert.False(probed);
            Assert.Equal("folder settings", row.Label);
            Assert.Equal(RowKind.Modified, row.Kind);
            Assert.Equal("(root)", row.Path);
        }

        [Fact]
        public void Map_deleted_ssq_probes_the_folder_path_not_the_marker()
        {
            string seen = null;
            var row = RowStatusMapper.Map(" D queries/ops/.ssq", rel => { seen = rel; return true; });
            Assert.Equal("queries/ops", seen);   // the folder path, forward-slashed, no "/.ssq" suffix
            Assert.Equal("folder settings", row.Label);
            Assert.Equal(RowKind.Modified, row.Kind);
        }

        [Fact]
        public void Map_deleted_backslash_ssq_probes_normalized_forward_slash_path()
        {
            string seen = null;
            var row = RowStatusMapper.Map(" D queries\\ops\\.ssq", rel => { seen = rel; return false; });
            Assert.Equal("queries/ops", seen);   // normalized to forward slashes before probing
            Assert.Equal("deleted folder", row.Label);
            Assert.Equal(RowKind.Deleted, row.Kind);
        }

        [Fact]
        public void Map_added_or_modified_ssq_never_probes()
        {
            // Only a deletion can mean the folder went away; adds/edits are always settings changes.
            bool probed = false;
            System.Func<string, bool> spy = rel => { probed = true; return false; };
            Assert.Equal("folder settings", RowStatusMapper.Map("?? queries/ops/.ssq", spy).Label);
            Assert.Equal("folder settings", RowStatusMapper.Map(" M queries/ops/.ssq", spy).Label);
            Assert.False(probed);
        }

        [Fact]
        public void Map_backslash_ssq_marker_is_recognized()
        {
            var row = RowStatusMapper.Map("?? queries\\ops\\.ssq");
            Assert.Equal("folder settings", row.Label);
            Assert.Equal("queries/ops", row.Path);
        }

        [Theory]
        [InlineData("?? CLAUDE.md", "new", "AI rules (CLAUDE.md)")]
        [InlineData(" M CLAUDE.md", "modified", "AI rules (CLAUDE.md)")]
        [InlineData("?? AGENTS.md", "new", "AI rules (AGENTS.md)")]
        public void Map_root_ai_guide_files_get_a_friendly_name(string line, string label, string path)
        {
            var row = RowStatusMapper.Map(line);
            Assert.Equal(label, row.Label);
            Assert.Equal(path, row.Path);
        }

        [Fact]
        public void Map_ai_guide_in_a_subfolder_also_gets_the_friendly_name()
        {
            var row = RowStatusMapper.Map("?? Queries/CLAUDE.md");
            Assert.Equal("new", row.Label);
            Assert.Equal("AI rules (CLAUDE.md)", row.Path);
        }

        [Fact]
        public void Map_deleted_ai_guide_gets_friendly_name()
        {
            // Orphan cleanup deletes stray guides, which then surface as deletions in Submit.
            var row = RowStatusMapper.Map(" D CLAUDE.md");
            Assert.Equal("deleted", row.Label);
            Assert.Equal(RowKind.Deleted, row.Kind);
            Assert.Equal("AI rules (CLAUDE.md)", row.Path);
        }

        [Fact]
        public void Map_renamed_into_a_guide_name_gets_friendly_name()
        {
            var row = RowStatusMapper.Map("R  notes.md -> AGENTS.md");
            Assert.Equal("renamed", row.Label);
            Assert.Equal("AI rules (AGENTS.md)", row.Path);
        }

        [Fact]
        public void Map_null_does_not_throw()
        {
            var row = RowStatusMapper.Map(null);
            Assert.NotNull(row);
        }
    }
}
