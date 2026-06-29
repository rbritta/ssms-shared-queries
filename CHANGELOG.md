# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_No unreleased changes yet._

## [1.0.0] - 2026-06-28

First public release.

### Added
- Object-Explorer-style docked panel: a folder tree of `.sql` queries with a repo
  node (name, branch, ahead/behind), favorites, and per-folder colors.
- **Sync** (pull) and **Submit** (commit + push) with a readable change list, a
  commit-message prompt, and a keep-mine / take-theirs conflict resolver.
- **Open** a query in the SSMS editor (double-click, or right-click > Open);
  **Insert** a query at the editor cursor (right-click > Insert).
- **Search** box that filters the tree by file name and highlights matches in blue.
- **Favorites** (per-user), **folder colors** (inherited, shared), advisory
  **locks**, and a **deprecated** mark (strikethrough), both shared via the repo.
- **Drag-and-drop** moves, **inline rename**, **new file / new folder**, and a
  **Discard changes** that only reverts the uncommitted part of a file (never
  deletes a committed file; restores deleted files via folder discard).
- Live metadata: line count with a `committed -> current` indicator on edit,
  creator / last author from git, and an **Info** dialog with recent commits.
- Local operation **history**.
- A **per-repository local cache**: each Repository URL is cloned into its own
  subfolder, so switching repositories keeps each clone in place.
- Unit-tested pure core: the git porcelain/log parsing, path helpers, the
  Submit-row mapping, and the `.ssq` metadata are dependency-free and covered by
  an xUnit suite (70 tests) that runs with a plain `dotnet test` (no VS SDK).

### Notes
- Microsoft does not officially support third-party SSMS 21/22 extensions; install
  the prebuilt `.vsix` per-machine (see the README).

[Unreleased]: https://github.com/rbritta/ssms-shared-queries/compare/v1.0.0...HEAD
[1.0.0]: https://github.com/rbritta/ssms-shared-queries/releases/tag/v1.0.0
