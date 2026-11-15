# Contributing

Thanks for your interest in improving SSMS Shared Queries.

## Set up, build and test

Windows with [VS Build Tools 2022](https://visualstudio.microsoft.com/downloads/) (the *.NET desktop build tools*
workload), the .NET 8 SDK and Git for Windows.

```powershell
git clone https://github.com/rbritta/ssms-shared-queries
cd ssms-shared-queries
.\build.ps1                                   # builds the VSIX (Release)
dotnet test tests\SsmsSharedQueries.Tests     # runs the unit tests
```

- Building the VSIX needs **VS Build Tools 2022** with the *.NET desktop build tools* workload. The VSIX targets come
  from the `Microsoft.VSSDK.BuildTools` NuGet package, so the full VS extension workload is not required.
- The tests are a plain `net472` xUnit project that **links** the pure source files under test (no reference to the
  VS SDK), so `dotnet test` runs anywhere .NET Framework does. They also check that the version agrees in every place
  it lives and run `tools/release-notes.ps1`.
- Coverage, as CI prints it:

  ```powershell
  dotnet test tests\SsmsSharedQueries.Tests --collect "Code Coverage;Format=cobertura" --results-directory TestResults
  .\tools\coverage-summary.ps1 -Path TestResults
  ```

- To try a build in SSMS: close SSMS, run `install.ps1` (per-machine), reopen. The panel (WPF), the editor and git
  integration are checked by hand there; there is no live demo, since everything runs inside SSMS. The screenshots in
  `docs/` show the real panel.

CI builds the VSIX and runs the tests on Windows for every push and pull request.

## Pull requests

- Behavior-affecting changes come with a unit test when the logic is pure (parsing, path handling, `.ssq` metadata);
  a bug fix with a test that fails without it. UI-only changes are reviewed by hand.
- Add a line under `Unreleased` in [CHANGELOG.md](CHANGELOG.md).
- Made-up data only (repository URLs, user names, queries) in tests, docs and screenshots.

## Releasing / version bumps

The version lives in **three** files that must be kept in sync (this is a classic, non-SDK VSPackage project, so there
is no single `Version` property); `VersionTests` fails when they disagree:

1. `src/SsmsSharedQueries/Properties/AssemblyInfo.cs` - `AssemblyVersion` / `AssemblyFileVersion` (`X.Y.Z.0`)
2. `src/SsmsSharedQueries/source.extension.vsixmanifest` - `Identity Version`
3. `src/SsmsSharedQueries/SharedQueriesPackage.cs` - `InstalledProductRegistration`

After a bump, build from clean (delete `src\SsmsSharedQueries\obj`, or `msbuild /t:Rebuild`): an incremental build
can keep the previous version in `obj\Release\extension.vsixmanifest`. CI always builds from a clean checkout.

Bump all three together, move the `Unreleased` notes in `CHANGELOG.md` under `## [X.Y.Z] - YYYY-MM-DD`, push, wait
for CI, then push a `vX.Y.Z` tag. The release workflow builds and tests the VSIX and publishes a GitHub Release with
it, the CHANGELOG section of that version as its notes (`tools/release-notes.ps1` refuses a tag that does not name the
manifest version).

## Style

- Follow the `.editorconfig` (4-space indent for C# and PowerShell, 2 elsewhere, LF; PowerShell scripts and the
  solution file CRLF; block-scoped namespaces, C# 7.3). Match the surrounding code.
- Keep commit messages in the imperative mood with a short summary line.
- Do not use em-dashes or en-dashes in code, comments, or docs - plain hyphens only.
