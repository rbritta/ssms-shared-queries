using System;
using System.Diagnostics;
using System.IO;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    /// <summary>tools/release-notes.ps1, run as release.yml runs it, against a temp CHANGELOG and the real one.</summary>
    public class ReleaseNotesTests : IDisposable
    {
        private const string Changelog =
            "# Changelog\n\n## [Unreleased]\n\n## [1.2.0] - 2026-01-02\n\n### Fixed\n- The thing.\n\n" +
            "## [1.1.0] - 2026-01-01\n\n### Added\n- The other thing.\n\n## [1.0.0] - 2025-12-31\n\n" +
            "[Unreleased]: https://example.com/compare/v1.2.0...HEAD\n[1.2.0]: https://example.com/compare/v1.1.0...v1.2.0\n";

        private readonly string _dir;

        public ReleaseNotesTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ssq-notes-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            File.WriteAllText(Path.Combine(_dir, "CHANGELOG.md"), Changelog);
        }

        public void Dispose()
        {
            try { Directory.Delete(_dir, true); } catch (IOException) { }
        }

        private static (int Code, string Out, string Err) Run(params string[] args)
        {
            var script = RepoFiles.Path("tools", "release-notes.ps1");
            var psi = new ProcessStartInfo("powershell.exe",
                "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + script + "\" " + string.Join(" ", args))
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (var p = Process.Start(psi))
            {
                var stdout = p.StandardOutput.ReadToEndAsync();
                var stderr = p.StandardError.ReadToEndAsync();
                Assert.True(p.WaitForExit(60000), "release-notes.ps1 did not finish");
                return (p.ExitCode, stdout.Result, stderr.Result);
            }
        }

        private string Notes(string tag, string version)
        {
            var outFile = Path.Combine(_dir, "notes.md");
            var r = Run("-Tag", tag, "-Version", version, "-Changelog", "\"" + Path.Combine(_dir, "CHANGELOG.md") + "\"",
                "-OutFile", "\"" + outFile + "\"");
            Assert.True(r.Code == 0, r.Err);
            return File.ReadAllText(outFile);
        }

        [Fact]
        public void A_section_ends_at_the_next_heading()
        {
            Assert.Equal("### Fixed\n- The thing.\n", Notes("v1.2.0", "1.2.0"));
            Assert.Equal("### Added\n- The other thing.\n", Notes("refs/tags/v1.1.0", "1.1.0"));
        }

        [Fact]
        public void A_tag_that_does_not_name_the_version_is_refused()
        {
            var r = Run("-Tag", "v1.1.0", "-Version", "1.2.0", "-Changelog", "\"" + Path.Combine(_dir, "CHANGELOG.md") + "\"");
            Assert.Equal(1, r.Code);
            Assert.Contains("does not name the version", r.Err);
        }

        [Theory]
        [InlineData("1.0.0")] // an empty section, the last one before the links
        [InlineData("9.9.9")] // no section at all
        public void A_version_without_notes_is_refused(string version)
        {
            var r = Run("-Tag", "v" + version, "-Version", version, "-Changelog", "\"" + Path.Combine(_dir, "CHANGELOG.md") + "\"");
            Assert.Equal(1, r.Code);
            Assert.Contains("no section, or an empty one", r.Err);
        }

        [Fact]
        public void The_real_changelog_has_notes_for_the_manifest_version()
        {
            var manifest = RepoFiles.Read("src", "SsmsSharedQueries", "source.extension.vsixmanifest");
            var version = System.Text.RegularExpressions.Regex.Match(manifest, @"<Identity [^>]*Version=""([^""]+)""").Groups[1].Value;
            var r = Run("-Tag", "v" + version);
            Assert.True(r.Code == 0, r.Err);
            Assert.False(string.IsNullOrWhiteSpace(r.Out));
        }
    }
}
