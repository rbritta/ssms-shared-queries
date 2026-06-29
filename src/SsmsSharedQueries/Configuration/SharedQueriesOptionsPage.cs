using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace SsmsSharedQueries.Configuration
{
    /// <summary>
    /// Settings under Tools &gt; Options &gt; SSMS Shared Queries. Persisted per user
    /// by the VS settings store. Repo URL works for Azure DevOps or GitHub; auth is
    /// handled transparently by Git Credential Manager on first push.
    /// </summary>
    [Guid("CA7201A6-BDEE-48D5-87A1-8EC7425FE18D")]
    public sealed class SharedQueriesOptionsPage : DialogPage
    {
        [Category("Repository")]
        [DisplayName("Repository URL")]
        [Description("HTTPS URL of the shared-queries git repository (Azure DevOps or GitHub).")]
        public string RepositoryUrl { get; set; } = string.Empty;

        [Category("Repository")]
        [DisplayName("Branch")]
        [Description("The single branch everyone reads from and pushes to.")]
        public string Branch { get; set; } = "main";

        [Category("Repository")]
        [DisplayName("Queries folder")]
        [Description("Folder inside the repository that holds the shared .sql files (leave empty for the repo root).")]
        public string BaseDirectory { get; set; } = "queries";

        [Category("Repository")]
        [DisplayName("Local cache folder")]
        [Description("Base folder for local clones; each repository is cloned into its own subfolder (per user). Switching the Repository URL keeps the previous clone in its folder.")]
        public string LocalCachePath { get; set; } = DefaultCachePath();

        private static string DefaultCachePath()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "SsmsSharedQueries", "repo");
        }
    }
}
