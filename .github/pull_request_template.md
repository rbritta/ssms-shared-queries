## What changed

<!-- One or two sentences. Link the issue it closes, if any. -->

## Why

<!-- The problem it solves, or what it makes possible. -->

## How it was tested

<!-- The unit tests added or changed, and what you checked by hand in SSMS (SSMS version, the steps). -->

## Checklist

- [ ] Builds (`build.ps1`) and the tests pass (`dotnet test tests\SsmsSharedQueries.Tests`)
- [ ] Unit tests cover any pure logic that changed (parsing, paths, `.ssq` metadata); a bug fix comes with a test that fails without it
- [ ] `CHANGELOG.md` has a line under `Unreleased`
- [ ] The version is bumped in all the places it lives, if releasing (see CONTRIBUTING; a test checks they agree)
- [ ] README or AGENTS.md describe the change, when a feature or a setting changed
- [ ] Made-up data only; no em-dashes or en-dashes; follows `.editorconfig`
