using System;
using System.IO;
using SsmsSharedQueries.UI;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    /// <summary>Exercises the .ssq read-modify-write helpers against a real temp directory.</summary>
    public class FolderMetaTests : IDisposable
    {
        private readonly string _dir;

        public FolderMetaTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ssq_fm_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, recursive: true); } catch { /* best effort */ }
        }

        private string Sub(string name)
        {
            var p = Path.Combine(_dir, name);
            Directory.CreateDirectory(p);
            return p;
        }

        [Fact]
        public void Color_round_trips()
        {
            Assert.Null(FolderMeta.ReadColor(_dir));
            FolderMeta.WriteColor(_dir, "#FF8800");
            Assert.Equal("#FF8800", FolderMeta.ReadColor(_dir));
        }

        [Fact]
        public void WriteColor_null_removes_the_color()
        {
            FolderMeta.WriteColor(_dir, "#123456");
            FolderMeta.WriteColor(_dir, null);
            Assert.Null(FolderMeta.ReadColor(_dir));
        }

        [Fact]
        public void EnsureFile_seeds_inherited_color_only_when_present()
        {
            var withColor = Sub("a");
            FolderMeta.EnsureFile(withColor, "#0A0B0C");
            Assert.Equal("#0A0B0C", FolderMeta.ReadColor(withColor));

            var noColor = Sub("b");
            FolderMeta.EnsureFile(noColor, null);
            Assert.Null(FolderMeta.ReadColor(noColor));
        }

        [Fact]
        public void Lock_round_trips_and_replaces_without_duplicates()
        {
            Assert.Null(FolderMeta.GetLock(_dir, "q.sql"));

            FolderMeta.SetLock(_dir, "q.sql", "alice");
            Assert.Equal("alice", FolderMeta.GetLock(_dir, "q.sql"));

            // re-locking the same file replaces, does not duplicate
            FolderMeta.SetLock(_dir, "q.sql", "bob");
            Assert.Equal("bob", FolderMeta.GetLock(_dir, "q.sql"));
            Assert.Single(FolderMeta.GetLocks(_dir));

            FolderMeta.RemoveLock(_dir, "q.sql");
            Assert.Null(FolderMeta.GetLock(_dir, "q.sql"));
        }

        [Fact]
        public void Multiple_locks_coexist_in_one_ssq()
        {
            FolderMeta.SetLock(_dir, "a.sql", "alice");
            FolderMeta.SetLock(_dir, "b.sql", "bob");
            var locks = FolderMeta.GetLocks(_dir);
            Assert.Equal(2, locks.Count);
            Assert.Equal("alice", locks["a.sql"]);
            Assert.Equal("bob", locks["b.sql"]);
        }

        [Fact]
        public void Deprecation_round_trips()
        {
            Assert.Null(FolderMeta.GetDeprecation(_dir, "q.sql"));
            FolderMeta.SetDeprecation(_dir, "q.sql", "alice");
            Assert.Equal("alice", FolderMeta.GetDeprecation(_dir, "q.sql"));
            FolderMeta.RemoveDeprecation(_dir, "q.sql");
            Assert.Null(FolderMeta.GetDeprecation(_dir, "q.sql"));
        }

        [Fact]
        public void Color_lock_and_deprecation_are_independent_in_one_file()
        {
            FolderMeta.WriteColor(_dir, "#abcdef");
            FolderMeta.SetLock(_dir, "q.sql", "alice");
            FolderMeta.SetDeprecation(_dir, "q.sql", "bob");

            Assert.Equal("#abcdef", FolderMeta.ReadColor(_dir));
            Assert.Equal("alice", FolderMeta.GetLock(_dir, "q.sql"));
            Assert.Equal("bob", FolderMeta.GetDeprecation(_dir, "q.sql"));

            // removing one leaves the others intact
            FolderMeta.RemoveLock(_dir, "q.sql");
            Assert.Equal("#abcdef", FolderMeta.ReadColor(_dir));
            Assert.Equal("bob", FolderMeta.GetDeprecation(_dir, "q.sql"));
        }

        [Fact]
        public void MoveFileMeta_carries_lock_and_deprecation_to_dest()
        {
            var src = Sub("src");
            var dst = Sub("dst");
            FolderMeta.SetLock(src, "q.sql", "alice");
            FolderMeta.SetDeprecation(src, "q.sql", "bob");

            FolderMeta.MoveFileMeta(src, dst, "q.sql");

            Assert.Null(FolderMeta.GetLock(src, "q.sql"));
            Assert.Null(FolderMeta.GetDeprecation(src, "q.sql"));
            Assert.Equal("alice", FolderMeta.GetLock(dst, "q.sql"));
            Assert.Equal("bob", FolderMeta.GetDeprecation(dst, "q.sql"));
        }

        [Fact]
        public void MoveFileMeta_same_folder_is_noop()
        {
            FolderMeta.SetDeprecation(_dir, "q.sql", "alice");
            FolderMeta.MoveFileMeta(_dir, _dir, "q.sql");
            Assert.Equal("alice", FolderMeta.GetDeprecation(_dir, "q.sql"));
        }

        [Fact]
        public void RenameFileMeta_carries_metadata_to_new_name_in_same_folder()
        {
            FolderMeta.SetLock(_dir, "old.sql", "alice");
            FolderMeta.SetDeprecation(_dir, "old.sql", "bob");

            FolderMeta.RenameFileMeta(_dir, "old.sql", "new.sql");

            Assert.Null(FolderMeta.GetLock(_dir, "old.sql"));
            Assert.Null(FolderMeta.GetDeprecation(_dir, "old.sql"));
            Assert.Equal("alice", FolderMeta.GetLock(_dir, "new.sql"));
            Assert.Equal("bob", FolderMeta.GetDeprecation(_dir, "new.sql"));
        }

        [Fact]
        public void RemoveFileMeta_clears_lock_and_deprecation_so_a_future_same_name_is_clean()
        {
            FolderMeta.SetLock(_dir, "q.sql", "alice");
            FolderMeta.SetDeprecation(_dir, "q.sql", "bob");

            FolderMeta.RemoveFileMeta(_dir, "q.sql");

            Assert.Null(FolderMeta.GetLock(_dir, "q.sql"));
            Assert.Null(FolderMeta.GetDeprecation(_dir, "q.sql"));
        }
    }
}
