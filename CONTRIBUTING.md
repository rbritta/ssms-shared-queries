# Contributing

Thanks for your interest in improving SSMS Shared Queries.

## Build and test

```powershell
.\build.ps1                                   # builds the VSIX (Release)
dotnet test tests\SsmsSharedQueries.Tests     # runs the unit tests
```

- Building the VSIX needs **VS Build Tools 2022** with the *.NET desktop build
  tools* workload. The VSIX targets come from the `Microsoft.VSSDK.BuildTools`
  NuGet package, so the full VS extension workload is not required.
- The tests are a plain `net472` xUnit project that **links** the pure source
  files under test (no reference to the VS SDK), so `dotnet test` runs anywhere.
- To try a build in SSMS: close SSMS, run `install.ps1` (per-machine), reopen.

## Releasing / version bumps

The version lives in **three** places that must be kept in sync (this is a classic,
non-SDK VSPackage project, so there is no single `Version` property):

1. `src/SsmsSharedQueries/Properties/AssemblyInfo.cs` - `AssemblyVersion` /
   `AssemblyFileVersion`
2. `src/SsmsSharedQueries/source.extension.vsixmanifest` - `Identity Version`
3. `src/SsmsSharedQueries/SharedQueriesPackage.cs` - `InstalledProductRegistration`

Bump all three together, update `CHANGELOG.md`, then push a `vX.Y.Z` tag - the
release workflow builds the VSIX and publishes a GitHub Release.

## Style

- Follow the `.editorconfig` (4-space indent, CRLF, block-scoped namespaces, C#
  7.3). Match the surrounding code.
- Keep commit messages in the imperative mood with a short summary line.
- Do not use em-dashes or en-dashes in code, comments, or docs - plain hyphens
  only.

## Scope

Behavior-affecting changes should come with a unit test when the logic is pure
(parsing, path handling, `.ssq` metadata). UI-only changes are reviewed by hand.
