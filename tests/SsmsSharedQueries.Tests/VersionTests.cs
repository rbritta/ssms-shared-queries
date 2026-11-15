using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace SsmsSharedQueries.Tests
{
    /// <summary>
    /// The version lives in several places of a classic VSPackage project (CONTRIBUTING.md): they must agree, and the
    /// CHANGELOG must have its section right under Unreleased.
    /// </summary>
    public class VersionTests
    {
        private static string ManifestVersion()
        {
            var doc = XDocument.Parse(RepoFiles.Read("src", "SsmsSharedQueries", "source.extension.vsixmanifest"));
            XNamespace ns = "http://schemas.microsoft.com/developer/vsx-schema/2011";
            return (string)doc.Root.Element(ns + "Metadata").Element(ns + "Identity").Attribute("Version");
        }

        [Fact]
        public void The_manifest_has_a_three_part_version()
            => Assert.Matches(@"^\d+\.\d+\.\d+$", ManifestVersion());

        [Fact]
        public void The_assembly_versions_follow_the_manifest()
        {
            var info = RepoFiles.Read("src", "SsmsSharedQueries", "Properties", "AssemblyInfo.cs");
            var expected = ManifestVersion() + ".0";
            Assert.Equal(expected, Regex.Match(info, @"AssemblyVersion\(""([^""]+)""\)").Groups[1].Value);
            Assert.Equal(expected, Regex.Match(info, @"AssemblyFileVersion\(""([^""]+)""\)").Groups[1].Value);
        }

        [Fact]
        public void The_package_registration_follows_the_manifest()
        {
            var package = RepoFiles.Read("src", "SsmsSharedQueries", "SharedQueriesPackage.cs");
            var m = Regex.Match(package, @"\[InstalledProductRegistration\(""[^""]*"",\s*""[^""]*"",\s*""([^""]+)""\)\]");
            Assert.True(m.Success, "InstalledProductRegistration not found");
            Assert.Equal(ManifestVersion(), m.Groups[1].Value);
        }

        [Fact]
        public void The_changelog_has_Unreleased_on_top_then_a_dated_section_for_this_version()
        {
            var changelog = RepoFiles.Read("CHANGELOG.md");
            var heads = Regex.Matches(changelog, @"^## \[([^\]]+)\]", RegexOptions.Multiline)
                .Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            Assert.Equal("Unreleased", heads[0]);
            Assert.Equal(ManifestVersion(), heads[1]);
            Assert.Matches(@"(?m)^## \[" + Regex.Escape(ManifestVersion()) + @"\] - \d{4}-\d\d-\d\d\r?$", changelog);
        }
    }
}
