using System;
using System.Text;

namespace SsmsSharedQueries.UI
{
    /// <summary>
    /// The AI-agent guide (CLAUDE.md / AGENTS.md) placed at the repository root so any AI assistant
    /// can collaborate on the shared queries safely: it edits files locally, but the human reviews
    /// and Submits through the plugin, keeping git a per-person audit trail.
    /// <para>
    /// The file is auto-managed: it is created when missing and replaced when a newer
    /// <see cref="GuideVersion"/> ships (the first teammate to Submit shares the update). A user can
    /// opt out by adding "custom" to the version marker line or deleting it - then it is left alone.
    /// All of this is decided by the pure helpers here so it can be unit-tested.
    /// </para>
    /// </summary>
    internal static class AiInstructions
    {
        public const string ClaudeFile = "CLAUDE.md";
        public const string AgentsFile = "AGENTS.md";

        /// <summary>Bump this whenever the guide TEXT below changes meaningfully; a higher value
        /// causes older auto-managed copies to be replaced on the next Sync.</summary>
        public const string GuideVersion = "1.1.0";

        private const string Marker = "ssq-ai-guide"; // identifies an auto-managed guide file

        /// <summary>True if a file with the given name is one of the AI guide files.</summary>
        public static bool IsGuideFile(string fileName)
            => string.Equals(fileName, ClaudeFile, StringComparison.OrdinalIgnoreCase)
               || string.Equals(fileName, AgentsFile, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// True if <paramref name="relPath"/> (a git '/'-separated repo-relative path) is an AI guide
        /// file living OUTSIDE the canonical queries folder, so folder-sync should remove it and keep
        /// a single guide. <paramref name="baseRelDir"/> is the queries folder relative to the repo
        /// root ("" = repo root). Non-guide files and the guide inside the base folder return false.
        /// </summary>
        public static bool IsOrphanGuide(string relPath, string baseRelDir)
        {
            if (string.IsNullOrEmpty(relPath)) return false;
            int slash = relPath.LastIndexOf('/');
            var name = slash >= 0 ? relPath.Substring(slash + 1) : relPath;
            if (!IsGuideFile(name)) return false;
            var dir = slash >= 0 ? relPath.Substring(0, slash) : string.Empty;
            return !string.Equals(dir, baseRelDir ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether the guide should be (re)written, given the existing file content (null if the
        /// file does not exist yet). Writes when missing, or when the file is an auto-managed guide
        /// of an OLDER version that has not been marked "custom". Hand-authored or custom files, and
        /// same/newer versions, are left untouched.
        /// </summary>
        public static bool ShouldWrite(string existing) => ShouldWrite(existing, GuideVersion);

        public static bool ShouldWrite(string existing, string currentVersion)
        {
            if (existing == null) return true;                                  // create when missing
            if (!TryReadVersion(existing, out var version)) return false;        // marker line gone -> leave (opted out)
            return CompareVersions(version, currentVersion) < 0;               // update only if older
        }

        /// <summary>Read the version from the control (first non-empty) line. Returns false when that
        /// line is not our marker - i.e. the user deleted it to keep their own edits.</summary>
        private static bool TryReadVersion(string text, out string version)
        {
            version = "0";
            var line = FirstNonEmptyLine(text);
            if (line == null || line.IndexOf(Marker, StringComparison.OrdinalIgnoreCase) < 0) return false;
            version = ExtractVersion(line) ?? "0";
            return true;
        }

        /// <summary>Compare dotted numeric versions: -1 if a &lt; b, 0 if equal, 1 if a &gt; b.</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = Parts(a); var pb = Parts(b);
            int n = Math.Max(pa.Length, pb.Length);
            for (int i = 0; i < n; i++)
            {
                int x = i < pa.Length ? pa[i] : 0;
                int y = i < pb.Length ? pb[i] : 0;
                if (x != y) return x < y ? -1 : 1;
            }
            return 0;
        }

        private static int[] Parts(string v)
        {
            if (string.IsNullOrWhiteSpace(v)) return new[] { 0 };
            var segs = v.Split('.');
            var r = new int[segs.Length];
            for (int i = 0; i < segs.Length; i++) r[i] = int.TryParse(segs[i], out var num) ? num : 0;
            return r;
        }

        private static string FirstNonEmptyLine(string text)
        {
            foreach (var raw in (text ?? string.Empty).Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length > 0) return line;
            }
            return null;
        }

        // First "vN[.N...]" token in the line -> "N.N...".
        private static string ExtractVersion(string line)
        {
            for (int i = 0; i + 1 < line.Length; i++)
            {
                if ((line[i] == 'v' || line[i] == 'V') && char.IsDigit(line[i + 1]))
                {
                    int j = i + 1;
                    while (j < line.Length && (char.IsDigit(line[j]) || line[j] == '.')) j++;
                    return line.Substring(i + 1, j - (i + 1));
                }
            }
            return null;
        }

        // ---- the guide text -----------------------------------------------------------------

        public static string BuildGuide(string repoName) => BuildGuide(repoName, GuideVersion);

        public static string BuildGuide(string repoName, string version)
        {
            var name = string.IsNullOrWhiteSpace(repoName) ? "this repository" : repoName.Trim();
            var sb = new StringBuilder();

            sb.AppendLine($"<!-- {Marker} v{version} - auto-managed by the SSMS Shared Queries plugin. Delete this line to keep your own edits (it will no longer be overwritten). -->");
            sb.AppendLine();

            sb.AppendLine($"# AI agent instructions - {name}");
            sb.AppendLine();
            sb.AppendLine("This repository is a **shared library of SQL Server queries** for the team, versioned in git");
            sb.AppendLine("and used from SQL Server Management Studio (SSMS) through the \"SSMS Shared Queries\" plugin.");
            sb.AppendLine("Each `.sql` file is a query the whole team can run.");
            sb.AppendLine();
            sb.AppendLine("You (Claude Code, or any AI assistant) are welcome to help improve these queries. Read this");
            sb.AppendLine("file before you change anything here.");
            sb.AppendLine();

            sb.AppendLine("## The one rule that matters: never touch git");
            sb.AppendLine();
            sb.AppendLine("Do **not** run any git command in this repository - no `commit`, `push`, `pull`, `fetch`,");
            sb.AppendLine("`checkout`, `branch`, `merge`, `rebase`, or `stash`. Do not stage files or open pull requests.");
            sb.AppendLine();
            sb.AppendLine("A human reviews every change inside SSMS and submits it with the plugin's **Submit** button,");
            sb.AppendLine("which commits and pushes under their own identity. That is what keeps the git history a");
            sb.AppendLine("trustworthy, per-person audit trail (the team relies on it for compliance).");
            sb.AppendLine();
            sb.AppendLine("Your job is to **edit the files in place** in the working tree and then stop. The human takes");
            sb.AppendLine("it from there.");
            sb.AppendLine();

            sb.AppendLine("## What you may do");
            sb.AppendLine();
            sb.AppendLine("- Read and explain any `.sql` file.");
            sb.AppendLine("- Improve a query: clarify it, fix bugs, optimize it, and format it consistently - but keep");
            sb.AppendLine("  its original intent and result shape unless the user explicitly asks otherwise.");
            sb.AppendLine("- Add a short comment header to a query (what it does, expected parameters). Leave authorship");
            sb.AppendLine("  to git - do not add name or date lines.");
            sb.AppendLine("- Create a new `.sql` file in the most fitting folder, named after what it does.");
            sb.AppendLine("- Point out where a query belongs, or that two queries are duplicates.");
            sb.AppendLine();
            sb.AppendLine("Always describe what you changed so the user can review it before they Submit.");
            sb.AppendLine();

            sb.AppendLine("## What you must not do");
            sb.AppendLine();
            sb.AppendLine("- Do not run git (see above).");
            sb.AppendLine("- Do not edit, create, or delete the hidden **`.ssq`** files. Those are plugin metadata");
            sb.AppendLine("  (folder colors, advisory locks, deprecation marks); hand-editing them corrupts shared state.");
            sb.AppendLine("- Do not modify a **locked** query. A file is locked when its folder's `.ssq` has a line");
            sb.AppendLine("  `lock=<file name>|<user>`. Someone is working on it - leave it alone and tell the user.");
            sb.AppendLine("- Do not rename, move, or delete shared queries unless asked. Those are done from the plugin");
            sb.AppendLine("  so metadata travels with the file.");
            sb.AppendLine("- Never put secrets, connection strings, or credentials in a shared query, and avoid");
            sb.AppendLine("  environment-specific `USE` / `GO` batch separators. Parametrize instead.");
            sb.AppendLine();

            sb.AppendLine("## How the repository is organized");
            sb.AppendLine();
            sb.AppendLine("- Folders group queries by area or team.");
            sb.AppendLine("- `*.sql` - the shared queries (this is all the plugin shows).");
            sb.AppendLine("- `.ssq` - hidden plugin metadata. Do not edit.");
            sb.AppendLine("- `CLAUDE.md` / `AGENTS.md` - this guide (auto-managed). The plugin hides these too.");
            sb.AppendLine();

            sb.AppendLine("## SQL conventions");
            sb.AppendLine();
            sb.AppendLine("- Dialect is **T-SQL** (Microsoft SQL Server), run from SSMS.");
            sb.AppendLine("- Keep one coherent query, or a small related set, per file.");
            sb.AppendLine("- Prefer readable, consistently cased and indented SQL. Comment the non-obvious parts.");
            sb.AppendLine("- Avoid destructive statements in a shared \"library\" query unless that is explicitly the");
            sb.AppendLine("  file's purpose - and if so, say so loudly in a header comment.");
            sb.AppendLine();

            sb.AppendLine("## The human's workflow (for context)");
            sb.AppendLine();
            sb.AppendLine("1. You edit files here locally.");
            sb.AppendLine("2. In SSMS, the plugin shows the changed files in red.");
            sb.AppendLine("3. The human reviews them, then clicks **Submit** to commit and push under their name.");
            sb.AppendLine();
            sb.AppendLine("You never do step 3.");

            return sb.ToString();
        }
    }
}
