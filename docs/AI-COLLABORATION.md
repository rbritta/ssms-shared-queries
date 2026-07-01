# AI collaboration

The shared query library is just a git repository full of `.sql` files, so an AI coding
assistant (Claude Code, or any other) can help you write and improve queries. The plugin
makes this safe by keeping one clear boundary:

> **The AI edits files locally. Only you Submit.**

That boundary is what protects the audit trail: every change reaches the team through the
plugin's **Submit** button, committed and pushed under *your* identity, never the AI's.

## How it works

1. **The rules are set up for you.** On every Sync the plugin makes sure a `CLAUDE.md` (and an
   `AGENTS.md` mirror) exists in the configured queries folder, telling the assistant how to
   behave: it may read, comment, format, and improve `.sql` files, but it must never run git,
   never edit the hidden `.ssq` metadata, and never touch a locked file. The file shows up as a
   pending change - **Submit** it so the whole team's assistants follow the same rules. It is
   version-stamped and refreshed automatically when a newer plugin ships (the first teammate to
   Submit shares the update). To keep your own edits and stop the auto-updates, just **delete the
   first line** (the version marker). Right-click the repo node > **Edit AI rules** to open it
   anytime.

2. **Point your assistant at the local repo.** The queries live in your *Local cache folder*
   (Tools > Options > SSMS Shared Queries), under a per-repo subfolder. Open that folder with
   your AI tool - for Claude Code, just run it from there and it picks up `CLAUDE.md`
   automatically.

3. **Let it work.** Ask the assistant to add a comment header, refactor a query, fix a bug, or
   draft a new `.sql`. It changes files in place and tells you what it did.

4. **Review and Submit.** Back in SSMS, click **Sync** if needed; changed files show in red
   with a live line-count delta. Read the diff, then **Submit**. The commit is yours.

## Why the AI never commits

Submission through the plugin is what makes the git history a trustworthy, per-person record
(the team relies on it for compliance). If an AI committed directly, attribution and review
would both be lost. So the assistant is told, explicitly and first, to **never run git** - no
commit, push, pull, branch, or stage. Its output is always just edited files for you to check.

## What stays hidden

The instruction files are markdown, and the panel only ever lists `.sql` queries, so
`CLAUDE.md` / `AGENTS.md` (and the `.ssq` metadata files) never clutter the tree. They are
still versioned and shared on Submit like everything else - they just do not show up as
queries.
