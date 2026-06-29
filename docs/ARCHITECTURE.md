# Architecture

A short tour of how the extension is put together and the two non-obvious design
decisions that surprise most readers.

## Layers

```
SharedQueriesPackage (AsyncPackage)
  -> SharedQueriesToolWindow            hosts the WPF panel
  -> SharedQueriesOptionsPage           repo URL / branch / queries folder

QueryPanelControl (WPF, code-built)     the panel: toolbar, tree, drag-drop,
                                        menus, inline rename, search, git-state
  -> GitService                         clone/pull/commit/push via git.exe
       -> GitStatusParser               pure parsing of porcelain/log/show
  -> FolderMeta                         the shared ".ssq" metadata per folder
  -> SqlEditorService                   read/insert into the active SQL editor
  -> QueryPaths / RowStatusMapper       pure helpers (path + Submit-row mapping)
```

The pure pieces (`GitStatusParser`, `QueryPaths`, `RowStatusMapper`, `FolderMeta`)
have no dependency on the VS SDK and are unit-tested by `tests/SsmsSharedQueries.Tests`,
which **links** those source files rather than referencing the VSPackage assembly,
so the tests run with a plain `dotnet test`.

`QueryPanelControl` is intentionally large (a code-built WPF panel). The roadmap
is to split it into collaborators (icons, tree builder, drag controller, search,
git-state) behind an options seam; the pure logic was extracted first so that
refactor has a test net.

## Two deliberate-but-surprising choices

### 1. Manual drag-and-drop (no OLE `DoDragDrop`)

Inside the SSMS host, the standard WPF/OLE `DoDragDrop` throws
`DV_E_FORMATETC` for a custom CLR data format. So the panel implements drag with
plain mouse capture + hit-testing and a ghost adorner, carrying the dragged item
in a field instead of an `IDataObject`. This is why the drag code looks lower
level than a typical WPF app.

### 2. `.ssq` is committed shared metadata

Folder color, advisory locks, and the deprecated mark are **team-shared**, so they
live in a hidden `.ssq` file inside each folder and are committed alongside the
queries (not in per-user local state). Per-file entries are keyed by file name:

```
color=#E8B74E
lock=report.sql|alice
deprecated=old_report.sql|bob
```

Because the entries are keyed by file name, the metadata must follow a file on
move/rename and be removed on delete, or a future same-named file would inherit a
stale lock/deprecation. That bookkeeping lives in `FolderMeta.MoveFileMeta` /
`RenameFileMeta` / `RemoveFileMeta`.

Per-user state (favorites, tree expand/collapse, operation history) is the
opposite: it stays local under `%LocalAppData%\SsmsSharedQueries` and is never
committed.

## Per-machine install

SSMS 22 only merges the `pkgdef` of extensions installed under the **product
folder** (`...\Common7\IDE\Extensions\`); a per-user install is catalogued but never
loads. `install.ps1` installs per-machine and runs `/updateconfiguration`. This
was learned the hard way and is the single most important deployment fact.
