Copy this folder into the root of your Perfect Git repo (e.g. `docs/design/`), then paste this to Claude Code:

---

This repo is Perfect Git, a fork of SourceGit (C# / Avalonia). Read `docs/design/README.md` and open `docs/design/PG Screen.dc.html` for exact values.

Implement the redesigned main window in Avalonia, following the "Suggested implementation order" in the README. Start with step 1 (theme tokens) only: find the dark theme resources, map the tokens, build, run `dotnet run --project src/SourceGit.csproj`, and show me what changed. Then stop and wait for my OK before moving to the next step.

Rules:
- Reuse existing ViewModels and commands; don't rewrite Git logic.
- English UI by default (strings go through the existing localization files).
- Keep the SourceGit MIT copyright notice.
- One commit per step, on branch `design/perfect-git`.
