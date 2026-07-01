# Changelog

All notable changes to this project are documented here. The format follows
[Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and the project aims to
follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_No unreleased changes yet._

## [1.1.1] - 2026-07-01

### Fixed
- A one-line query now reads "1 line" (singular) instead of "1 lines".

### Docs
- Refreshed the panel screenshot.

## [1.1.0] - 2026-07-01

### Added
- **Search by name and contents.** A single search matches both file names and file bodies in
  one pass (no mode to toggle) and runs automatically ~700 ms after you stop typing (Enter still
  searches immediately). Name matching ignores separators, so `allt` finds `all-tables`, and the
  matched span is highlighted in blue. Queries matched by their body get a blue file icon and a
  "Matches file contents" tooltip. Each file is read at most once per search (cached per term).
  The search box has an inline clear (X) and is disabled until a sync has loaded queries.
- **Auto-sync, spinning-gear status, and setup prompt.** The panel syncs automatically when it
  opens and reloads when you apply the repository settings. A slowly spinning gear with a
  "Syncing..." caption shows while a sync runs. Until the repository is configured - or if a
  sync fails - a friendly setup overlay (a link to the options) is shown instead of an error
  dialog, with a message that matches the cause. **Sync**, **Submit** and search stay disabled
  until the repository is configured and loaded.
- **AI collaboration.** An auto-managed `CLAUDE.md` (plus an `AGENTS.md` mirror) is kept in the
  queries folder, telling any AI assistant how to help with the shared queries: it may read,
  comment, and improve `.sql` files locally, but it must never run git - the human reviews and
  **Submits** through the plugin, so the commit history stays a per-person audit trail. The
  guide is version-stamped and refreshed automatically when a newer plugin ships (the first
  teammate to Submit shares the update); delete its first line to keep your own edits. Right-
  click the repo node > **Edit AI rules** to open it. The markdown guides are hidden from the
  `.sql`-only tree and appear as "AI rules (...)" in Submit. See `docs/AI-COLLABORATION.md`.
- **Open in File Explorer** on any folder's right-click menu.

### Fixed
- **Discard folder changes** now also reverts a just-set folder **color** (or lock), which lives
  in a brand-new, untracked `.ssq` file that `git checkout HEAD --` would leave behind. New
  `.sql` queries are still kept.
- **Clicking a highlighted search match no longer crashes SSMS.** Highlighted letters are text
  `Run`s (not visuals); walking up from one to its tree row now handles that instead of throwing.
- **A bug in the plugin can no longer take down the host.** Plugin exceptions in UI handlers are
  contained and logged (the SSMS window stays up) instead of propagating.
- **Search stays responsive and correct:** it no longer re-reads the whole library on every UI
  action while a search is active, refresh requests coalesce instead of being dropped (no stale
  Submit count / colors), and the blue content-match icon stays correct after you edit and save.
- **Only one sync touches the repository at a time,** so applying settings mid-sync can no longer
  race two git processes on the same working tree.
- The local **operation history** is capped (most recent 500) instead of growing without bound.

### Changed
- Right-click menus regrouped: the destructive **Discard** is isolated from the read actions, and
  **Edit AI rules** moved out of the "New..." cluster with verb-prefixed labels.
- More of the logic is extracted into pure, unit-tested helpers (search matching, version compare,
  repo-name / git-identity parsing, conflict and orphan-guide classification); the xUnit suite
  grew from 70 to 155 tests, still run by a plain `dotnet test`.

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

[Unreleased]: https://github.com/rbritta/ssms-shared-queries/compare/v1.1.1...HEAD
[1.1.1]: https://github.com/rbritta/ssms-shared-queries/compare/v1.1.0...v1.1.1
[1.1.0]: https://github.com/rbritta/ssms-shared-queries/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/rbritta/ssms-shared-queries/releases/tag/v1.0.0
