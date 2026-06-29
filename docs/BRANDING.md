# Branding

The logo and icons are generated from code with [Pillow](https://python-pillow.org/),
so they are reproducible and need no external design tool.

## The mark

A database cylinder - three stacked discs in an "ocean" blue gradient (only the top
disc shows its lid; the bottom is rounded) - with the git **branch** glyph in white
sitting on the body. Blue is the database (SQL); the branch is git version control.
Together, the whole product in one mark: SQL, in git.

## Palette

| Part | Hex |
|---|---|
| Top disc | `#2E6FB0` |
| Middle disc | `#4F8AC0` (the panel's database-icon blue) |
| Bottom disc | `#86B6E0` |
| Outline | `#21425F` |
| Git glyph | white, with a subtle dark halo for contrast |

## Regenerate

```bash
pip install pillow
python tools/make_logo.py
```

This overwrites, in place:

- `docs/logo.png` - 480px, the README hero
- `src/SsmsSharedQueries/Resources/Icon.png` - 16px, the Tools-menu command icon
- `src/SsmsSharedQueries/Resources/repo.png` - 32px, the repository node in the panel tree
  (embedded in the assembly and loaded by `QueryPanelControl.RepoIcon`)

Edit the palette or geometry in [`tools/make_logo.py`](../tools/make_logo.py) and
re-run. The geometry is authored in a 256-unit grid and supersampled down, so the one
mark stays crisp from 480px to 16px.
