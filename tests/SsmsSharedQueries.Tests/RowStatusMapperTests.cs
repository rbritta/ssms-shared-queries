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
        public void Map_new_ssq_marker_becomes_new_folder()
        {
            var row = RowStatusMapper.Map("?? queries/reports/.ssq");
            Assert.Equal("new folder", row.Label);
            Assert.Equal(RowKind.New, row.Kind);
            Assert.Equal("queries/reports", row.Path);
        }

        [Fact]
        public void Map_deleted_ssq_marker_becomes_deleted_folder()
        {
            var row = RowStatusMapper.Map(" D queries/ops/.ssq");
            Assert.Equal("deleted folder", row.Label);
            Assert.Equal(RowKind.Deleted, row.Kind);
            Assert.Equal("queries/ops", row.Path);
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
            Assert.Equal("new folder", row.Label);
            Assert.Equal("(root)", row.Path);
        }

        [Fact]
        public void Map_backslash_ssq_marker_is_recognized()
        {
            var row = RowStatusMapper.Map("?? queries\\ops\\.ssq");
            Assert.Equal("new folder", row.Label);
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
