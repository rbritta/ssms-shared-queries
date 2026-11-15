# AGENTS.md

For AI coding agents working on this repository.

## What this is

SSMS Shared Queries is an extension for SQL Server Management Studio 22: a panel docked next to Object Explorer that
shows a team's library of `.sql` files kept in an ordinary git repository. The panel clones, pulls, commits and pushes
through `git.exe` (each change in its author's name); folder colors, advisory locks and deprecation marks live in a
committed `.ssq` file per folder. A classic VSPackage (`net472`, amd64, C# 7.3), Windows only.

## Layout

| Path | What |
|---|---|
| `src/SsmsSharedQueries/SharedQueriesPackage.cs` | the `AsyncPackage`: the command, the tool window, the options page |
| `src/SsmsSharedQueries/UI/QueryPanelControl.cs` | the code-built WPF panel (large on purpose; see docs/ARCHITECTURE.md) |
| `src/SsmsSharedQueries/UI/` | dialogs and the pure helpers: `QueryPaths`, `RowStatus`, `SearchMatch`, `FolderMeta`, `MetaFormat`, `AiInstructions` |
| `src/SsmsSharedQueries/Git/` | `GitService` (git.exe), and the pure `GitStatusParser`, `GitUrl`, `GitIdentity`, `GitResultClassifier`, `FileMeta` |
| `src/SsmsSharedQueries/Editor/` | reading and inserting into the active SQL editor |
| `tests/SsmsSharedQueries.Tests/` | xUnit on `net472`; it **links** the pure source files, with no reference to the VS SDK |
| `tools/` | `release-notes.ps1` (a version's CHANGELOG section), `coverage-summary.ps1` (CI), `make_logo.py` |
| `build.ps1`, `install.ps1` | build the VSIX with the desktop MSBuild; install it per-machine into SSMS |
| `docs/` | architecture, AI collaboration, the private gallery guide, branding, and the screenshots |

## Commands

```powershell
.\build.ps1                                                  # src\SsmsSharedQueries\bin\Release\SsmsSharedQueries.vsix
dotnet test tests\SsmsSharedQueries.Tests                    # the unit tests
.\tools\release-notes.ps1 -Tag v1.2.2                        # the notes of a release
```

## Invariants

- Only pure, dependency-free logic goes in the files the test project links; anything that touches the VS SDK, WPF or
  git.exe stays out of them. A new pure helper is added to the `<Compile Include>` list of the test project.
- Git runs as `git.exe` with quoted, escaped arguments and `GIT_TERMINAL_PROMPT=0`; never a console window, never a
  stored credential (Git Credential Manager owns those).
- `.ssq` and `.sql` files are shared with the whole team: metadata follows a file on move and rename and leaves on
  delete (`FolderMeta`), and a `.ssq` exists only while a folder has metadata.
- Discard never deletes a submitted query; conflicts are resolved by the user's choice, never auto-merged.
- No telemetry: the only network traffic is git talking to the user's repository.
- The version lives in `AssemblyInfo.cs`, `source.extension.vsixmanifest` and `SharedQueriesPackage.cs`, and
  `CHANGELOG.md` has its section under `Unreleased`; `VersionTests` checks they agree.
- English only, made-up data only, no em-dashes or en-dashes. `.editorconfig`: 4 spaces for C# and PowerShell, 2
  elsewhere, LF (PowerShell and the .sln CRLF).
