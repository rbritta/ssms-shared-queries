namespace SsmsSharedQueries.Git
{
    /// <summary>Creator / last-author summary for a file, derived from git history.</summary>
    internal sealed class FileMeta
    {
        public string Creator { get; set; }
        public string CreateDate { get; set; }
        public string LastAuthor { get; set; }
        public string LastDate { get; set; }
    }
}
