# TODO

The roadmap, in the order it would be picked up. Nothing here is promised; an issue moves an item up.

- Split the WPF panel (`QueryPanelControl`) into smaller collaborators: icons, tree builder, drag controller, search,
  git state (the parsing / path / row logic is already extracted and tested, so the refactor has a test net; see
  [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)).
- Per-host identity bootstrap: set `user.name` / `user.email` on the first Sync when git has none.
- An optional protected-branch / Pull Request mode for governed environments, instead of pushing straight to the
  shared branch.
