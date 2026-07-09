using System;

namespace SsmsSharedQueries.UI
{
    internal enum RowKind { New, Modified, Deleted, Renamed }

    /// <summary>A readable form of one git porcelain status line, for the Submit dialog.</summary>
    internal sealed class RowStatus
    {
        public string Label { get; set; }
        public RowKind Kind { get; set; }
        public string Path { get; set; }
    }

    /// <summary>
    /// Maps a git porcelain status line ("XY path") to a human label + kind + display path,
    /// turning the hidden ".ssq" folder marker into the folder itself. Pure, so the mapping
    /// is unit-tested independently of the WPF row rendering in CommitDialog.
    ///
    /// A ".ssq" no longer mirrors a folder's lifecycle: folders are not seeded with one and an
    /// empty ".ssq" is deleted, so an added/modified/cleared marker is just a folder-settings
    /// change. A deleted marker means the whole folder went away only when the folder is actually
    /// gone on disk - hence the optional <c>folderExists</c> probe, so clearing a folder's last
    /// setting reads as "folder settings", not "deleted folder".
    /// </summary>
    internal static class RowStatusMapper
    {
        private const string SsqMarker = "/.ssq";

        public static RowStatus Map(string porcelain, Func<string, bool> folderExists = null)
        {
            porcelain = porcelain ?? string.Empty;
            string code = porcelain.Length >= 2 ? porcelain.Substring(0, 2) : porcelain;
            string path = (porcelain.Length >= 4 ? porcelain.Substring(3) : porcelain).Trim().Trim('"');

            // renames come as "old -> new": show the new path.
            int arrow = path.IndexOf(" -> ", StringComparison.Ordinal);
            string shown = arrow >= 0 ? path.Substring(arrow + 4) : path;

            var c = code.Replace(" ", string.Empty);
            bool isNew = code == "??" || c.IndexOf('A') >= 0;
            bool isDel = !isNew && c.IndexOf('D') >= 0;
            bool isRen = !isNew && !isDel && c.IndexOf('R') >= 0;

            string norm = shown.Replace('\\', '/');
            bool isFolder = norm.EndsWith(SsqMarker, StringComparison.Ordinal) || norm == ".ssq";
            if (isFolder)
            {
                bool isRootSsq = norm.Length <= SsqMarker.Length; // the base folder's own ".ssq"
                shown = isRootSsq ? "(root)" : norm.Substring(0, norm.Length - SsqMarker.Length);
                // The marker is a folder deletion only when the folder is really gone; otherwise its
                // last setting was just cleared and the folder (and its .sql files) survive. The base
                // folder itself never "goes away", so its marker is always a settings change.
                bool folderGone = isDel && !isRootSsq && folderExists != null && !folderExists(shown);
                return new RowStatus
                {
                    Label = folderGone ? "deleted folder" : "folder settings",
                    Kind = folderGone ? RowKind.Deleted : RowKind.Modified,
                    Path = shown,
                };
            }
            // The auto-managed AI guide files get a friendly name instead of a bare path.
            var fileName = norm.Substring(norm.LastIndexOf('/') + 1);
            if (AiInstructions.IsGuideFile(fileName)) shown = "AI rules (" + fileName + ")";

            if (isNew) return new RowStatus { Label = "new", Kind = RowKind.New, Path = shown };
            if (isDel) return new RowStatus { Label = "deleted", Kind = RowKind.Deleted, Path = shown };
            if (isRen) return new RowStatus { Label = "renamed", Kind = RowKind.Renamed, Path = shown };
            return new RowStatus { Label = "modified", Kind = RowKind.Modified, Path = shown };
        }
    }
}
