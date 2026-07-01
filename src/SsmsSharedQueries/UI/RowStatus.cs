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
    /// </summary>
    internal static class RowStatusMapper
    {
        private const string SsqMarker = "/.ssq";

        public static RowStatus Map(string porcelain)
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
                shown = norm.Length > SsqMarker.Length ? norm.Substring(0, norm.Length - SsqMarker.Length) : "(root)";
                return new RowStatus
                {
                    Label = isNew ? "new folder" : isDel ? "deleted folder" : "folder settings",
                    Kind = isNew ? RowKind.New : isDel ? RowKind.Deleted : RowKind.Modified,
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
