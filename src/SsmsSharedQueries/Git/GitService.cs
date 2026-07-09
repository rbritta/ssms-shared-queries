using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SsmsSharedQueries.Git
{
    /// <summary>
    /// Result of running a single git command.
    /// </summary>
    internal sealed class GitResult
    {
        public int ExitCode { get; set; }
        public string StdOut { get; set; } = string.Empty;
        public string StdErr { get; set; } = string.Empty;
        public bool Success => ExitCode == 0;
        public override string ToString() => $"exit={ExitCode}\n{StdOut}\n{StdErr}".Trim();
    }

    /// <summary>
    /// Makes git completely invisible to the end user. The plugin shells out to the
    /// installed git.exe (with Git Credential Manager handling Azure DevOps / GitHub
    /// login the first time and caching the credential in Windows Credential Manager).
    /// No git command, console window, or concept is ever shown to the user.
    ///
    /// Commits are attributed to the signed-in person via the resolved git identity
    /// (user.name / user.email), so the repository history is a real per-person audit
    /// trail (SOC 2 friendly).
    /// </summary>
    internal sealed class GitService
    {
        private readonly string _repoUrl;
        private readonly string _branch;
        private readonly string _localPath;

        public GitService(string repoUrl, string branch, string cacheFolder)
        {
            _repoUrl = (repoUrl ?? throw new ArgumentNullException(nameof(repoUrl))).Trim();
            _branch = string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim();
            // Each repository gets its OWN subfolder under the cache folder, so switching the
            // Repository URL just looks at a different clone and leaves the previous one in place.
            var baseDir = (cacheFolder ?? throw new ArgumentNullException(nameof(cacheFolder))).Trim();
            _localPath = Path.Combine(baseDir, GitUrl.RepoFolderName(_repoUrl));
        }

        public string LocalPath => _localPath;
        public string Branch => _branch;

        private bool IsCloned => Directory.Exists(Path.Combine(_localPath, ".git"));

        /// <summary>
        /// Ensure the local clone exists and is up to date on the configured branch.
        /// Clones on first use; otherwise fetches and fast-forwards/rebases.
        /// The first network operation may trigger a one-time browser login via GCM.
        /// </summary>
        public async Task EnsureRepositoryAsync(CancellationToken ct = default)
        {
            // If the cache folder already holds a DIFFERENT repository (the user changed the
            // Repository URL in Options), re-clone instead of silently fetching the old one.
            if (IsCloned)
            {
                var originUrl = (await RunAsync("config --get remote.origin.url", _localPath, ct).ConfigureAwait(false)).StdOut.Trim();
                if (!GitUrl.SameRepository(originUrl, _repoUrl))
                {
                    var dirty = await RunAsync("status --porcelain", _localPath, ct).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(dirty.StdOut))
                        throw new GitException(
                            $"The local cache folder holds a different repository ({originUrl}) with unsubmitted changes. " +
                            "Submit or discard them, or set a new 'Local cache folder' in Options.", dirty);
                    DeleteClone(); // working tree clean -> safe to replace with the configured repo
                }
            }

            if (!IsCloned)
            {
                var parent = Directory.GetParent(_localPath)?.FullName;
                if (!string.IsNullOrEmpty(parent))
                    Directory.CreateDirectory(parent);

                // Refuse to clone into a non-empty directory that is not our clone, so we never
                // clobber unexpected user data; the target must be empty (or not exist yet).
                if (Directory.Exists(_localPath) && !IsDirectoryEmpty(_localPath))
                {
                    throw new InvalidOperationException(
                        $"Local cache path '{_localPath}' exists and is not a git clone. Choose an empty folder.");
                }

                var clone = await RunAsync(
                    $"clone --branch {Quote(_branch)} --single-branch {Quote(_repoUrl)} {Quote(_localPath)}",
                    workingDir: parent ?? Environment.CurrentDirectory,
                    ct: ct).ConfigureAwait(false);

                if (!clone.Success)
                {
                    // Branch may not exist yet on a brand-new repo; fall back to a plain clone.
                    var plain = await RunAsync($"clone {Quote(_repoUrl)} {Quote(_localPath)}",
                        parent ?? Environment.CurrentDirectory, ct).ConfigureAwait(false);
                    if (!plain.Success)
                        throw new GitException("Failed to clone the shared-queries repository.", plain);
                }
                return;
            }

            // Existing clone: fetch, then rebase ONLY if the working tree is clean.
            // (Rebasing with local edits fails with "cannot rebase: unstaged changes";
            // those local changes are exactly what the user will Submit, so just fetch.)
            var fetch = await RunAsync($"fetch origin {Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
            if (!fetch.Success)
                throw new GitException("Failed to fetch from the shared-queries repository.", fetch);

            await RunAsync($"checkout {Quote(_branch)}", _localPath, ct).ConfigureAwait(false);

            var status = await RunAsync("status --porcelain", _localPath, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(status.StdOut))
                return; // local changes present: fetched only, skip rebase to avoid failing.

            var rebase = await RunAsync($"rebase origin/{_branch}", _localPath, ct).ConfigureAwait(false);
            if (!rebase.Success)
            {
                // Abort a failed rebase to leave the working tree clean.
                await RunAsync("rebase --abort", _localPath, ct).ConfigureAwait(false);
                throw new GitException("Failed to update the local copy (rebase).", rebase);
            }
        }

        /// <summary>Resolve the commit author identity that will be recorded (name &lt;email&gt;).</summary>
        public async Task<string> GetIdentityAsync(CancellationToken ct = default)
        {
            var name = (await RunAsync("config user.name", _localPath, ct).ConfigureAwait(false)).StdOut.Trim();
            var email = (await RunAsync("config user.email", _localPath, ct).ConfigureAwait(false)).StdOut.Trim();
            if (string.IsNullOrEmpty(name) && string.IsNullOrEmpty(email))
                return string.Empty;
            return $"{name} <{email}>";
        }

        /// <summary>Porcelain status lines (e.g. " M path", "?? path"); empty when clean.</summary>
        public async Task<IReadOnlyList<string>> GetStatusAsync(CancellationToken ct = default)
        {
            var list = new List<string>();
            if (!IsCloned) return list;
            // --untracked-files=all so files inside a brand-new folder are listed
            // individually (otherwise git collapses them to "folder/").
            // core.quotepath=false keeps non-ASCII paths (e.g. "Relatorios") as literal UTF-8 instead
            // of octal-escaped, quoted strings, so callers see the real path (the Submit dialog's
            // folder-existence probe needs it, and stdout is already decoded as UTF-8).
            var r = await RunAsync("-c core.quotepath=false status --porcelain --untracked-files=all", _localPath, ct).ConfigureAwait(false);
            foreach (var raw in (r.StdOut ?? string.Empty).Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Trim().Length > 0) list.Add(line);
            }
            return list;
        }

        /// <summary>
        /// Stage everything, commit with <paramref name="message"/>, integrate the remote,
        /// and push. Returns the FAILING <see cref="GitResult"/> (commit, rebase, or push) so
        /// the caller can show the exact git error (e.g. a protected-branch rejection); returns
        /// a success result when the push succeeds (or there was nothing to commit).
        /// </summary>
        public async Task<GitResult> CommitAndPushAsync(string message, CancellationToken ct = default)
        {
            if (!IsCloned)
                throw new InvalidOperationException("Repository is not cloned yet. Click Sync first.");

            await RunAsync("add -A", _localPath, ct).ConfigureAwait(false);

            var status = await RunAsync("status --porcelain", _localPath, ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(status.StdOut))
                return new GitResult { ExitCode = 0, StdOut = "Nothing to commit." };

            var commit = await RunAsync($"commit -m {Quote(message)}", _localPath, ct).ConfigureAwait(false);
            if (!commit.Success) return commit;

            // Integrate the latest remote first to avoid a non-fast-forward rejection.
            var rebase = await RunAsync($"pull --rebase origin {Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
            if (!rebase.Success)
            {
                await RunAsync("rebase --abort", _localPath, ct).ConfigureAwait(false);
                return rebase;
            }

            return await RunAsync($"push origin HEAD:{Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
        }

        /// <summary>Number of local commits not yet on the remote branch (0 if none/unknown).</summary>
        public async Task<int> GetAheadCountAsync(CancellationToken ct = default)
        {
            if (!IsCloned) return 0;
            var r = await RunAsync("rev-list --count @{u}..HEAD", _localPath, ct).ConfigureAwait(false);
            return int.TryParse((r.StdOut ?? string.Empty).Trim(), out var n) ? n : 0;
        }

        /// <summary>Number of remote commits not yet local (as of the last fetch/sync).</summary>
        public async Task<int> GetBehindCountAsync(CancellationToken ct = default)
        {
            if (!IsCloned) return 0;
            var r = await RunAsync("rev-list --count HEAD..@{u}", _localPath, ct).ConfigureAwait(false);
            return int.TryParse((r.StdOut ?? string.Empty).Trim(), out var n) ? n : 0;
        }

        /// <summary>
        /// Re-integrate and push after a conflict, auto-resolving in favor of one side:
        /// <paramref name="preferMine"/> true keeps the local change, false takes the
        /// server's. Assumes the local commit already exists (a prior commit step).
        /// </summary>
        public async Task<GitResult> ResolvePushAsync(bool preferMine, CancellationToken ct = default)
        {
            if (!IsCloned) throw new InvalidOperationException("Repository is not cloned yet. Click Sync first.");
            // In a rebase, the replayed (local) commit is "theirs"; the upstream base is "ours".
            var strategy = preferMine ? "theirs" : "ours";
            var rebase = await RunAsync($"pull --rebase -X {strategy} origin {Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
            if (!rebase.Success)
            {
                await RunAsync("rebase --abort", _localPath, ct).ConfigureAwait(false);
                return rebase;
            }
            return await RunAsync($"push origin HEAD:{Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
        }

        /// <summary>Integrate the remote then push pending commits (no commit). Returns the
        /// failing result (rebase or push) so the caller can show the exact git error.</summary>
        public async Task<GitResult> PushAsync(CancellationToken ct = default)
        {
            if (!IsCloned)
                throw new InvalidOperationException("Repository is not cloned yet. Click Sync first.");
            var rebase = await RunAsync($"pull --rebase origin {Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
            if (!rebase.Success)
            {
                await RunAsync("rebase --abort", _localPath, ct).ConfigureAwait(false);
                return rebase;
            }
            return await RunAsync($"push origin HEAD:{Quote(_branch)}", _localPath, ct).ConfigureAwait(false);
        }

        /// <summary>Repo-relative paths of the changed/untracked files (from porcelain status).</summary>
        public async Task<HashSet<string>> GetChangedRelPathsAsync(CancellationToken ct = default)
            => GitStatusParser.ParseChangedRelPaths(await GetStatusAsync(ct).ConfigureAwait(false));

        /// <summary>Repo-relative paths of tracked files currently deleted in the working tree.</summary>
        public async Task<IReadOnlyList<string>> GetDeletedRelPathsAsync(CancellationToken ct = default)
            => GitStatusParser.ParseDeletedRelPaths(await GetStatusAsync(ct).ConfigureAwait(false));

        /// <summary>
        /// One pass over the full history mapping each repo-relative path to its creator
        /// (oldest commit) and last author (newest), with short dates.
        /// </summary>
        public async Task<Dictionary<string, FileMeta>> GetHistoryMapAsync(CancellationToken ct = default)
        {
            if (!IsCloned) return new Dictionary<string, FileMeta>(StringComparer.OrdinalIgnoreCase);
            var r = await RunAsync("log --no-renames --date=short --pretty=format:%x01%an%x02%ad --name-only", _localPath, ct).ConfigureAwait(false);
            return GitStatusParser.ParseHistoryMap(r.StdOut);
        }

        /// <summary>
        /// Discard local changes under a path (file or folder): restore tracked files to
        /// their last-committed state, INCLUDING ones deleted in the working tree (they come
        /// back) and ones whose change was already staged. Scoped to the path, so it never
        /// touches anything else, and it NEVER removes untracked (new) files.
        /// </summary>
        public async Task<GitResult> RevertPathAsync(string relPath, CancellationToken ct = default)
        {
            if (!IsCloned) throw new InvalidOperationException("Repository is not cloned yet. Click Sync first.");
            // "checkout HEAD -- <path>" resets both the index and the working tree for the
            // path back to HEAD, so a deleted (staged or unstaged) tracked file is restored.
            // It never deletes untracked files, so Discard can never destroy a submitted file.
            return await RunAsync($"checkout HEAD -- {Quote(relPath)}", _localPath, ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete untracked plugin-metadata files (named <paramref name="metaFileName"/>, the
        /// hidden ".ssq") under a path. Folder-discard calls this after <see cref="RevertPathAsync"/>
        /// so a color or lock just set on a folder that had none - which lives in a brand-new,
        /// untracked ".ssq" that "checkout HEAD --" leaves behind - is reverted too. Untracked
        /// queries (".sql") are never touched, so newly-created queries are still kept.
        /// </summary>
        public async Task RemoveUntrackedMetaAsync(string relPath, string metaFileName, CancellationToken ct = default)
        {
            if (!IsCloned) return;
            var r = await RunAsync($"ls-files --others --exclude-standard -- {Quote(relPath)}", _localPath, ct).ConfigureAwait(false);
            if (!r.Success) return;
            foreach (var rel in GitStatusParser.FilterUntrackedMeta(r.StdOut, metaFileName))
            {
                try
                {
                    var full = Path.Combine(_localPath, rel.Replace('/', Path.DirectorySeparatorChar));
                    if (File.Exists(full)) File.Delete(full);
                }
                catch (Exception ex) { Diagnostics.Log.Write("RemoveUntrackedMetaAsync: could not delete " + rel, ex); }
            }
        }

        /// <summary>Repo-relative paths of untracked (new, non-ignored) files in the working tree.</summary>
        public async Task<IReadOnlyList<string>> GetUntrackedRelPathsAsync(CancellationToken ct = default)
        {
            var list = new List<string>();
            if (!IsCloned) return list;
            var r = await RunAsync("ls-files --others --exclude-standard", _localPath, ct).ConfigureAwait(false);
            foreach (var line in r.StdOut.Split('\n'))
            {
                var p = line.Trim().Trim('"');
                if (p.Length > 0) list.Add(p);
            }
            return list;
        }

        /// <summary>True if the path is tracked by git (i.e. it was submitted at least once).</summary>
        public async Task<bool> IsTrackedAsync(string relPath, CancellationToken ct = default)
        {
            if (!IsCloned) return false;
            var r = await RunAsync($"ls-files --error-unmatch -- {Quote(relPath)}", _localPath, ct).ConfigureAwait(false);
            return r.Success;
        }

        /// <summary>
        /// Line count of the file as committed at HEAD, or -1 if it is not tracked yet.
        /// Counts the same way File.ReadAllLines does (a trailing newline adds no extra line):
        /// the captured stdout terminates every output line with a newline, so counting '\n'
        /// in it equals the File.ReadAllLines length used for the working-tree count.
        /// </summary>
        public async Task<int> GetHeadLineCountAsync(string relPath, CancellationToken ct = default)
        {
            if (!IsCloned) return -1;
            var r = await RunAsync($"show HEAD:{Quote(relPath)}", _localPath, ct).ConfigureAwait(false);
            return r.Success ? GitStatusParser.CountNewlines(r.StdOut) : -1;
        }

        /// <summary>Recent commit lines ("date  author  subject") touching one file.</summary>
        public async Task<IReadOnlyList<string>> GetFileLogAsync(string relPath, int max, CancellationToken ct = default)
        {
            var list = new List<string>();
            if (!IsCloned) return list;
            // The format string contains spaces, so it MUST be quoted as a single argument;
            // unquoted, "%an" and "%s" become separate args and git fails (empty log).
            var r = await RunAsync($"log -n {max} --date=short {Quote("--pretty=format:%ad  %an  %s")} -- {Quote(relPath)}", _localPath, ct).ConfigureAwait(false);
            foreach (var raw in (r.StdOut ?? string.Empty).Split('\n'))
            {
                var l = raw.TrimEnd('\r');
                if (l.Trim().Length > 0) list.Add(l);
            }
            return list;
        }

        private static bool IsDirectoryEmpty(string path)
        {
            try { return !Directory.EnumerateFileSystemEntries(path).GetEnumerator().MoveNext(); }
            catch { return false; }
        }

        /// <summary>Remove the local clone, first clearing the read-only attribute git puts on pack
        /// files so the recursive delete does not fail on Windows.</summary>
        private void DeleteClone()
        {
            if (!Directory.Exists(_localPath)) return;
            foreach (var file in Directory.EnumerateFiles(_localPath, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); } catch { }
            }
            Directory.Delete(_localPath, recursive: true);
        }

        private static string Quote(string s) => "\"" + (s ?? string.Empty).Replace("\"", "\\\"") + "\"";

        /// <summary>
        /// Run a git command with no visible console window, capturing stdout/stderr.
        /// </summary>
        private static Task<GitResult> RunAsync(string arguments, string workingDir, CancellationToken ct)
        {
            Diagnostics.Log.Write($"git {arguments}  (cwd={workingDir})");
            return Task.Run(() =>
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "git.exe",
                    Arguments = arguments,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                };
                // Disable git's own console prompt; Git Credential Manager still shows its
                // GUI login when a credential is actually needed.
                psi.EnvironmentVariables["GIT_TERMINAL_PROMPT"] = "0";

                using (var p = new Process { StartInfo = psi })
                {
                    var so = new StringBuilder();
                    var se = new StringBuilder();
                    p.OutputDataReceived += (s, e) => { if (e.Data != null) so.AppendLine(e.Data); };
                    p.ErrorDataReceived += (s, e) => { if (e.Data != null) se.AppendLine(e.Data); };
                    p.Start();
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();

                    while (!p.WaitForExit(200))
                    {
                        if (ct.IsCancellationRequested)
                        {
                            try { p.Kill(); } catch { /* best effort */ }
                            ct.ThrowIfCancellationRequested();
                        }
                    }
                    p.WaitForExit(); // flush async buffers

                    var result = new GitResult
                    {
                        ExitCode = p.ExitCode,
                        StdOut = so.ToString(),
                        StdErr = se.ToString(),
                    };
                    Diagnostics.Log.Write($"git exit={result.ExitCode} :: {result.StdErr.Trim()}");
                    return result;
                }
            }, ct);
        }
    }

    internal sealed class GitException : Exception
    {
        public GitResult Result { get; }
        public GitException(string message, GitResult result) : base(message + " " + (result?.StdErr ?? string.Empty))
        {
            Result = result;
        }
    }
}
