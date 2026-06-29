# Security

## Trust model

- The extension shells out to whatever **`git.exe`** is first on the `PATH`, and
  relies on **Git Credential Manager** for authentication. A hijacked `PATH`
  `git.exe` would therefore run with your privileges. Use a trusted Git for
  Windows install.
- Git arguments (repository URL, branch, paths, commit messages, file names) are
  passed as quoted, escaped arguments, and `GIT_TERMINAL_PROMPT=0` is set so git
  never blocks on a hidden console prompt.
- The local clone and per-user files live under `%LocalAppData%\SsmsSharedQueries`.
  Credentials are **not** stored by this extension; they are managed by Git
  Credential Manager in Windows Credential Manager.
- The hidden `.ssq` metadata files (folder color, locks, deprecated marks) are
  committed to the shared repository and are plain text - do not put secrets in
  them or in query files.

## Reporting a vulnerability

Please report suspected vulnerabilities privately via a
[GitHub security advisory](https://github.com/rbritta/ssms-shared-queries/security/advisories/new)
rather than a public issue. You can expect an initial response within a few days.
