# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this repository is

**PerfectGit** is a fork of [SourceGit](https://github.com/sourcegit-scm/sourcegit) (MIT), a cross-platform Git GUI written in C# / .NET 10 / Avalonia 11. The fork keeps SourceGit's Git logic and re-skins the main window after the design handoff in `docs/design/` (`README.md` = tokens, sizes, copy and interactions; `PG Screen.dc.html` = the mock; `logo/` = logo SVGs). When changing UI, treat `docs/design/README.md` as the source of truth.

Naming rule: the **user-visible** name is `PerfectGit` (App.axaml `Name`, locale strings, About, csproj Product/Title, `App.plist` bundle name, Linux `.desktop` Name). **Internal** names stay `SourceGit`: assembly/executable name (all `avares://SourceGit/...` URIs depend on it), namespaces, data folder (`~/Library/Application Support/SourceGit`, `%APPDATA%\SourceGit`, `$XDG_CONFIG_HOME/SourceGit`), bundle identifier, IPC pipe. Keep the SourceGit MIT copyright notice (LICENSE, About dialog).

## Commands

```sh
git submodule update --init          # required: depends/AvaloniaEdit is a custom fork referenced by the csproj
dotnet build src/SourceGit.csproj    # Debug build
dotnet run --project src/SourceGit.csproj [-- <repo dir>]
dotnet format --verify-no-changes src/SourceGit.csproj   # CI format check (format-check.yml) — must pass
build/scripts/run.osx-dev.sh [<repo dir>]   # macOS: Debug build wrapped in build/dev/PerfectGit.app (real name + icon in Dock/menu bar)
python translate_helper.py fr_FR [--check]  # list/fill keys missing from a locale vs en_US
```

- On this machine the .NET SDK may live in `~/.dotnet` (not on PATH): `export DOTNET_ROOT=$HOME/.dotnet PATH=$HOME/.dotnet:$PATH`.
- `dotnet run` on macOS always shows the executable name "SourceGit" in the Dock/menu bar and can't be changed at runtime; use `run.osx-dev.sh` to see PerfectGit branding.
- Release publishing (`.github/workflows/build.yml`): `dotnet publish src/SourceGit.csproj -c Release -r <rid>`; Release is **NativeAOT + trimmed** (`PublishAot`, `TrimMode=link`). Packaging: `build/scripts/package.osx-app.sh`, `package.linux.sh`, `package.win.ps1`.
- CLI modes of the app: `<exe> <DIR>` (open repo, forwarded to the running instance via IPC), `<exe> --history <FILE_OR_DIR>`, `<exe> --blame <FILE>`.
- There is no test project. Verify by building, running `dotnet format`, and exercising the UI. The UI can be rendered offscreen with `Avalonia.Headless` + `UseSkia()` and `Window.CaptureRenderedFrame()` (set up like `App.Main`: `Native.OS.SetupBasicDirectories()`, `Native.OS.SetupApp(builder)`, `Models.AvatarManager.Instance.Start()`).

## Architecture

Single project `src/SourceGit.csproj` (namespace `SourceGit`), MVVM with CommunityToolkit.Mvvm and **compiled bindings by default** (`AvaloniaUseCompiledBindingsByDefault`).

- `Commands/` — one class per git invocation, deriving from `Command` (set `WorkingDirectory`, `Context`, `Args`; run with `ExecAsync()` / `ReadToEndAsync()` or a custom parser; `RaiseError = false` for probes). All Git work goes through these; never shell out elsewhere.
- `Models/` — plain data (Commit, Branch, Change, Decorator…) plus UI-agnostic logic: `CommitGraph` (lane layout: `LANE_WIDTH`, `FIRST_LANE_X`, `CONTENT_MIN_X`, lane colors follow `BranchColor`), `BranchColor` (stable per-branch color: hash of the name, main/master/develop → lane 0), `RelativeTime` (normal + compact “12 min ago”), `PullRequestProvider` (GitHub PRs via `gh api`, fallback to the public REST API, parsed with `JsonDocument`), `FuzzyMatch`, `AvatarManager`.
- `ViewModels/` — `Launcher` (window, tabs = `LauncherPage`, `CommandPalette`) → `Repository` (the hub for one repo: branches/remotes/tags/stashes refresh, popups, toolbar state and tooltip texts) → `Histories` (commit list, selection → `DetailContext`) → `CommitDetail` (inspector, file rows, numstat, `CenterDiff`). Dialogs are `Popup` subclasses shown with `repo.ShowPopup(...)` (check `CanCreatePopup()` first; `ShowAndStartPopupAsync` runs popups that can start directly); their `Sure()` does the work. Palettes implement `ICommandPalette` and are opened with `.Open()`; `QuickPalette` is the ⌘P palette (actions/branches/commits).
- `Views/` — `.axaml` + code-behind. A view is resolved from its VM by naming convention (`ControlExtensions.CreateFromViewModels`: `ViewModels.X` → `Views.X`), so new popups/palettes need a matching view class name. Performance-critical pieces are custom-drawn controls: `CommitGraph` (graph overlay drawn over the `HistoriesCommitList` DataGrid, synced in `Histories.OnCommitListLayoutUpdated`), `CommitRefsPresenter` (ref pills, also drag source/target), `CommitSubjectPresenter` (subject with conventional-commit type colors), `ChangeStatusIcon`.
- `Converters/` — static `FuncValueConverter` instances used from XAML via `{x:Static c:...}`.
- `Native/` — `OS` facade with `MacOS` / `Windows` / `Linux` backends (data dirs, terminals, file manager, macOS Objective-C interop: traffic lights, Dock icon).
- `Resources/` — `Themes.axaml` (Light/Dark theme dictionaries; `Color.*` → `Brush.*`), `Styles.axaml` (global and class styles such as `tb_action`, `group_expander`, `inspector_action`), `Icons.axaml` (`StreamGeometry` icons, `Icons.*`), `Locales/*.axaml`.
- `App.axaml(.cs)` — startup, theme/locale/font switching (`SetTheme`, `SetLocale`), `App.Text(key, args)` for localized strings; `App.JsonCodeGen.cs` holds the System.Text.Json source-generation context.

### PerfectGit UI pieces (added on top of SourceGit)

- Theme tokens: dark dictionary of `Themes.axaml` maps the design tokens (`--bar`/`--bg`/`--side`/`--panel`/`--raise`/`--line`/`--fg*`) onto SourceGit's `Color.*` keys, plus new `FG3`, `Hover`, `Selection`, `AccentFG` (text on the lavender accent), `Added`, `Deleted`, `Warning`. The accent comes from `FluentTheme.Palettes` in `App.axaml`; use `Brush.AccentFG` (not white) on accent backgrounds and `Brush.Selection` for selected rows.
- Main window: title bar tabs (`LauncherTabBar`), toolbar (`RepositoryToolbar`, labelled actions + state-aware tooltips from `Repository.*Tip`), sidebar sections incl. PULL REQUESTS and STASHES (`Repository.axaml`; group heights are computed in `Repository.axaml.cs` `UpdateLeftSidebarLayout`), 38px graph rows with avatar nodes, pinned “uncommitted changes” row, status bar hint (`Histories.StatusHint`), right-hand inspector (`CommitBaseInfo` with `UseInspectorLayout`, CHANGES tab hidden in history), file diffs opened in the center (`CommitDetail.CenterDiff`, Esc/“← History” closes).
- Drag a branch onto another (`Views/BranchDragDrop.cs`, `ViewModels/BranchDrop.cs`, `BranchDropPopover`): merge/rebase/interactive rebase/PR, conflict prediction via `git merge-tree` (`Commands/MergeTreeConflicts`), switching branch first when needed (`Repository.SwitchToBranchIfNeededAsync`).

## Conventions and gotchas

- **Localization**: every user-facing string is a `Text.*` key in `src/Resources/Locales/en_US.axaml` (keys kept in alphabetical order); other locales merge `en_US` as fallback. PerfectGit strings are also translated in `fr_FR.axaml` — add new keys to both. CI (`localization-check.yml`) re-sorts locale files and updates `TRANSLATION.md`.
- **`Run.Text` binds TwoWay by default** in Avalonia: always write `<Run Text="{Binding …, Mode=OneWay}"/>` for model data (a TwoWay Run once wrote `''` into `Commit.SHA`).
- Release is NativeAOT/trimmed: avoid reflection-based serialization (use `App.JsonCodeGen` or `JsonDocument`). The `SourceGit` assembly is a trimmer root, so its own types survive.
- Only the Dark theme is designed; keep Light-theme values sensible when adding theme keys (every key must exist in both dictionaries).
- `build/resources/` is in `.gitignore` but its files are tracked: stage changes with `git add -u build/resources` (or `-f`).
- Commit messages follow upstream prefixes: `feature:`, `fix:`, `ux:`, `refactor:`, `enhance:`, `doc:`, `localization:`. Upstream PRs target the `develop` branch (CI workflows run on `develop`).
