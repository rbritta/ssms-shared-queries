using System;
using System.IO;

namespace SsmsSharedQueries.Tests
{
    /// <summary>The repository's own files, found from the test assembly's folder (the folder holding the .sln).</summary>
    internal static class RepoFiles
    {
        public static string Root
        {
            get
            {
                var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
                while (dir != null && !File.Exists(System.IO.Path.Combine(dir.FullName, "SsmsSharedQueries.sln")))
                {
                    dir = dir.Parent;
                }
                if (dir == null)
                {
                    throw new InvalidOperationException("SsmsSharedQueries.sln not found above " + AppDomain.CurrentDomain.BaseDirectory);
                }
                return dir.FullName;
            }
        }

        public static string Path(params string[] parts)
            => System.IO.Path.Combine(Root, System.IO.Path.Combine(parts));

        public static string Read(params string[] parts)
            => File.ReadAllText(Path(parts));
    }
}
