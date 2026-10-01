# Handoff: Perfect Git — main window redesign

## Overview
Perfect Git is an open-source fork (MIT) of **SourceGit** (C# / .NET / Avalonia UI). This handoff describes the redesign of the **main repository window**: repo tabs, toolbar, left sidebar, commit graph + list, right-hand commit inspector, drag & drop merge/rebase, and a ⌘P command palette. macOS first, **dark theme by default**, **English UI by default**. Guidance for beginners is **subtle**: hover tooltips, one-line explanations in menus, a hint in the status bar.

Keep the original SourceGit copyright notice (MIT license) in LICENSE and About.

## About the Design Files
The files in this bundle are **design references created in HTML**, not production code. Recreate them **in the existing SourceGit codebase** using Avalonia (`.axaml` views, styles/resources, existing ViewModels). Do not embed HTML or a WebView.

- `Perfect Git.dc.html`: overview canvas showing the 3 states (open in a browser; needs `support.js` next to it).
- `PG Screen.dc.html`: the 1440×900 window itself. Prop `mode` = `default` | `drag` | `palette`. All values (colors, sizes, copy, sample data) are inline in this file.

## Fidelity
**High-fidelity.** Colors, type sizes, spacing and copy are final. Icons are placeholder Unicode glyphs: replace them with the icon set already used in SourceGit (`Icons.axaml` path geometries) or SF Symbols–like equivalents.

## Suggested implementation order (lowest risk first)
1. **Theme tokens**: replace the dark theme colors in SourceGit's theme resources (`src/Resources/Themes.axaml` or equivalent) with the tokens below.
2. **Title bar + repo tabs** (launcher / tab bar view).
3. **Toolbar** (repository toolbar view): regroup and relabel actions.
4. **Sidebar** (repository left panel): sections LOCAL / REMOTE / PULL REQUESTS / TAGS / STASHES / SUBMODULES.
5. **Commit list + graph** (`Histories` view + graph renderer): row height, avatar nodes, ref pills, conventional-commit type coloring.
6. **Right inspector**: move commit details from the bottom pane to a right column (keep bottom layout as an option in Preferences).
7. **Drag & drop** branch → branch popover.
8. **Command palette** (⌘P / ⇧⌘P).
9. Status bar hint + hover tooltips.

Explore the repo first to find the actual file names; the names above are indicative.

## Screen: Main window (1440×900 reference)
Vertical stack: Title bar 40 → Toolbar 50 → Body (flex) → no global footer (status bar lives inside the center column).

### Title bar — 40px, bg `--bar`
- Traffic lights at left (12px, gap 8, 16px left padding), then 16px gap.
- **Repo tabs**: height 32, aligned to bottom, radius 8 8 0 0, padding 0 14, min-width 140–200. Active tab: bg `--bg`, 2px top inset line in `--acc`, repo name 13/600 + current branch 11.5 `--fg3` + close ✕ `--fg3`. Inactive: text `--fg2`; a 6px `--warnfg` dot = uncommitted changes. "＋" opens a repo.
- Right: **search field** 300×26, radius 7, bg `--raise`, 1px `--line`, placeholder "Search or run a command…" 12px `--fg3`, keycap "⌘P" (11px, 1px border, radius 4). Clicking opens the palette.

### Toolbar — 50px, bottom border 1px `--line`, padding 0 14
- Left: **branch switcher** pill: 9px lane-color dot, branch name 13/600, ahead count "↑2" in mono 11 `--fg2`, "▾". bg `--raise`, 1px `--line`, radius 8, padding 6 10.
- Center group (buttons 7×11 padding, radius 7, gap 6 icon/label, text `--fg2`, hover bg `--hover`): **Fetch, Pull, Push** | **Branch, Stash, Pop** | **Terminal**. Separators: 1px `--line`, vertical margin 6. Push shows a badge (16px pill, bg `--acc`, text `--accfg`, 10/700) with the number of commits to push.
- Right: remote status "● origin · 2 min ago" 12px `--fg3`, dot `--addfg` (or `--delfg` on error).
- **Hover tooltip** (example on Push): 270px wide, bg `--raise`, 1px `#ffffff1f`, radius 9, shadow `0 12px 30px #0009`, padding 10 12. Title 12.5/600 "Push · send your commits"; body 12/1.45 `--fg2` "2 commits on **feature/graph-avatars** only exist on your Mac. Push copies them to `origin`."; shortcut 11 `--fg3`. Every toolbar action gets a tooltip of that form: name · plain-English verb, one sentence about what it will do *with the current state*, shortcut.

### Sidebar — 220px (resizable, min 160), bg `--side`, right border, padding 10 8, section gap 14, base 12.5px. A 1px `--line` rule separates consecutive sections (none above the first).
- Filter field: 28px, radius 7, bg `--raise`, "⌕ Filter".
- Section header: 11px/600, letter-spacing .03em, uppercase, `--fg3`, count after name (500), actions at right ("＋").
- **LOCAL**: rows 5×8 padding, radius 6. 9px dot in branch lane color, name (current branch 700 + bg `--sel`), ahead/behind at right in mono 11 `--fg2` ("↑2", "↓3").
- **REMOTE**: remote row ("▾ origin" + host "github" `--fg3`), children indented 26px with **hollow** 8px dot (1.5px border in lane color), text `--fg2`. Collapsed "▸ upstream  sourcegit-scm".
- **PULL REQUESTS**: two-line rows: "#14" mono 11 `--fg3` + title (ellipsis); second line 11px: status chip (radius 8, padding 0 6, 600; "in review" = `--warnfg` on 14% of it; "merged" = `--addfg` on 14%) + author · comment count. Clicking a PR selects it: the center (instead of the graph) shows the PR page — "#N" + status chip, title 22/700, author + opened/merged dates, "head → base" branch pill (mono), "N commits · N files +A −D", then the description rendered as GitHub markdown (headings, lists, task lists, code, quotes, tables, links). The right inspector (tab PULL REQUEST) repeats the header, has a "Description" row (selected while the page is shown; click to come back to it), then the FILES list like a commit (Tree/List, +/−; click → diff in the center, header "← History · #N · title"; Esc goes back to the PR page, "← History" to the graph), actions "Checkout" and "Open on GitHub". The files come from fetching `refs/pull/N/head` (FETCH_HEAD only) and diffing from the merge base, like GitHub's "Files changed".
- **TAGS / STASHES / SUBMODULES**: collapsed headers with counts.

### Center — commit graph & list, bg `--bg`
- Column header 30px: "GRAPH" (166px) · "MESSAGE" (flex) · "DATE" (64, right aligned) · "SHA" (62). Style = section header style.
- **Rows: 38px.** Content starts at x=194 (graph column).
- **Graph**: lane spacing 30px, first lane at x=24, stroke 2.5px round caps.
  - Lane colors cycle `--l0 … --l4`; a branch keeps its color everywhere (sidebar dot, ref pill, graph, inspector).
  - Same-lane edge = vertical line. Branch-off/merge edges = vertical then cubic curve over ~0.85 row height into the other lane.
  - **Commit node = author avatar**: circle r=12.5, fill = avatar (photo, or initials 11/700 white on a per-author color), 2.5px stroke in lane color.
  - **Merge commit**: small hollow dot r=5.5, fill `--bg`, 2.5px lane stroke; message text `--fg2`.
  - **HEAD**: extra ring r=17, 1.5px lane color, 45% opacity.
  - **Uncommitted changes (WIP)** row on top: dashed circle r=10 (3 3), dashed edge to HEAD, italic `--fg3` text "// 3 changed files, not committed yet", date "now". Clicking it selects it (selection bg) and the right inspector shows the local changes: UNSTAGED / STAGED file lists with "Stage all" / "Unstage all" (hovering a file or folder row shows a "Stage" / "Unstage" button on its right), commit message, Amend, "Commit & Push" and "Commit"; a Tree / List switch in its header (shared with the staging view). Clicking a file shows its diff in the center (same "← History · Esc" header as commit files). Double-clicking the row opens the full staging view.
- **Ref pills** before the message (gap 8): 11/600, padding 2 8, radius 5.
  - Local branch: bg = lane color at 22%, text `--fg`, current branch prefixed "✓ ".
  - Remote branch: transparent, 1px border lane color at 50%, text `--fg2`.
  - Tag: `--warnfg` text on 14% of it.
- **Conventional commit type** (`feat:`, `fix:`…): type word in 600 colored `oklch(0.76 0.12 H)` — feat 285, fix 25, refactor 200, style 350, enhance 150, chore/doc/ci = `--fg3`; rest of message `--fg`.
- Selected row: bg `--sel` + 3px left inset bar `--acc`.
- Date 12px `--fg3` ("12 min ago", "yesterday", "2 wk ago"); SHA mono 11 `--fg3`, 7 chars.
- **Status bar** 28px, bg `--side`, top border: left a "?" keycap + contextual hint ("Drag a branch onto another to merge or rebase it."), right "412 commits · 5 branches". 11.5px `--fg3`.

### Right inspector — 380px, bg `--panel`, left border
- Header block (padding 16 18, bottom border, gap 12):
  - Line 1: short SHA mono 11.5 `--fg2` + copy icon; right "✓ signed" chip (`--addfg` on 14%) if GPG/SSH-signed.
  - Subject 17/700, line-height 1.3, letter-spacing -0.01em. Body 12.5/1.5 `--fg2`.
  - Author: 32px avatar with 2px `--panel` gap + 2px lane-color ring; name 600; "Today, 2:02 PM · 12 min ago" 11.5 `--fg3`.
  - Grid 72px/1fr, 12px: Parent (mono, `--acc`, clickable → selects parent), Branches (pills).
- Files: header "4 FILES" + "+86" `--addfg` + "−21" `--delfg`, right segmented Tree/List. Rows 5×8, radius 6: status letter 10/700 (M `--warnfg`, A `--addfg`, D `--delfg`, R `--acc`), name, +/− counts 11px. Tree mode is a real nested tree: folder rows `--fg3` with a ▾/▸ chevron (click to collapse), folders before files, each level indented 14px; a folder holding a single sub-folder and no file is shown on one row ("app/code/Vendor", like VS Code). Click a file → diff view.
- Footer actions (padding 14 18, top border): buttons Checkout · Cherry-pick · Revert · "···" (12px, padding 6 11, radius 7, bg `--raise`, 1px `--line`).

## Interactions & Behavior

### Drag & drop branch onto branch (`mode="drag"`)
- Draggable: ref pills in the graph and branch rows in the sidebar. While dragging: source pill at 35% opacity; ghost = pill with lane-color border, bg lane 30% mixed with `--raise`, shadow `0 10px 24px #000a`, rotated −3°.
- Valid target (another branch pill/row): 1.5px dashed `--acc` border + 4px halo `--acc` at 18%; target row tinted `--acc` at 7%.
- On drop → popover 350px (bg `--raise`, radius 10, shadow `0 18px 44px #000b`), anchored under the target:
  - Header 12px `--fg2`: "Drop **feature/merge-dnd** onto **develop**" (names in their lane colors).
  - Options (padding 8 10, radius 7, first one highlighted `--sel`), each = title 600 + Git term right-aligned in mono 11 `--fg3` + one-line explanation 11.5 `--fg2`:
    - "Merge into develop" · `merge ↵` · "Creates a commit joining both branches. History is kept as is."
    - "Rebase onto develop" · `rebase` · "Replays your 2 commits on top of develop. Linear history."
    - "Interactive rebase…" · `rebase -i` · "Pick, reorder or squash commits first."
    - separator, "Create a pull request into develop"
  - Footer 11px: green dot "No conflicts expected" (run a dry-run merge / `git merge-tree` to compute) · "Esc to cancel". If conflicts are predicted: `--warnfg` dot + "N files may conflict".
- Enter = first option; arrows move; Esc cancels. Reuse SourceGit's existing Merge/Rebase popups/commands for execution.

### Command palette (`mode="palette"`)
- ⌘P (and click on the title-bar field). Overlay `#0a0b0dcc` over the whole window; panel 640px wide, top 110px, bg `--raise`, 1px `#ffffff24`, radius 12, shadow `0 30px 80px #000c`.
- Input row 15px, caret `--acc`, "Esc" keycap at right.
- Fuzzy results grouped **ACTIONS / BRANCHES / COMMITS** (group header = section header style). Action rows: 26px icon tile (bg `--hover`, radius 7), title 600, subtitle 11.5 `--fg2` with plain-English explanation + Git command in mono. Selected row bg `--sel`.
- Footer 11px `--fg3`: "↑↓ navigate · ↵ run · ⌘↵ show Git command · > commands only".
- Actions should map to SourceGit's existing commands (fetch, pull, push, branch, merge, rebase, stash, cherry-pick, tag, checkout…).

### Other
- Hover on any row: bg `--hover`. Transitions 120ms ease-out on bg/opacity.
- Double-click branch = checkout (confirm if dirty working copy).
- Right-click keeps SourceGit's context menus, restyled (bg `--raise`, radius 8, row radius 6).

## State
- `selectedCommit`, `hoveredAction` (tooltip), `dragSource`, `dropTarget`, `dropPopoverOpen`, `paletteOpen`, `paletteQuery`, `paletteIndex`.
- Per-branch color map (stable: hash of branch name → lane palette index, but current HEAD lane keeps its color).
- Avatar cache (Gravatar/GitHub avatar by email; fall back to initials on a color derived from email hash).
- PR list from the remote's API (GitHub first) — optional, hide section if not available.

## Design tokens (dark theme, default)
| Token | Value | Use |
|---|---|---|
| `--bar` | `#0e0f12` | title bar |
| `--bg` | `#121316` | window / graph background |
| `--side` | `#141519` | sidebar, status bar |
| `--panel` | `#17181c` | inspector |
| `--raise` | `#1e2025` | inputs, buttons, popovers |
| `--line` | `#26282e` | borders, separators |
| `--hover` | `#ffffff0b` | hover fill |
| `--sel` | `oklch(0.29 0.045 285)` ≈ `#2c2940` | selection |
| `--fg` | `#eceef2` | primary text |
| `--fg2` | `#a3a7b0` | secondary text |
| `--fg3` | `#71757e` | tertiary / labels |
| `--acc` | `oklch(0.76 0.13 285)` ≈ `#a8a0f0` | accent (lavender) |
| `--accfg` | `#15141a` | text on accent |
| `--addfg` | `oklch(0.78 0.14 150)` ≈ `#6fd08c` | additions / ok |
| `--delfg` | `oklch(0.74 0.15 25)` ≈ `#f07a6e` | deletions / error |
| `--warnfg` | `oklch(0.82 0.12 85)` ≈ `#e6c26a` | modified / tags / warning |
| `--code` | `#ffffff12` | inline code bg |
| `--l0` | `oklch(0.72 0.14 250)` ≈ `#6aa2ec` | lane 0 (develop/main) |
| `--l1` | `oklch(0.76 0.16 55)` ≈ `#f59a56` | lane 1 |
| `--l2` | `oklch(0.76 0.16 145)` ≈ `#6fcf73` | lane 2 |
| `--l3` | `oklch(0.82 0.14 95)` ≈ `#dcc55a` | lane 3 |
| `--l4` | `oklch(0.72 0.17 350)` ≈ `#ec76a6` | lane 4 |

Hex approximations are for Avalonia (no oklch support) — verify visually.

- **Typography**: system font (SF Pro). Base 13px. Sizes used: 11 (labels, pills), 11.5, 12, 12.5, 13, 15 (palette input), 17 (inspector subject). Weights 400/500/600/700. Mono: IBM Plex Mono (or SF Mono / JetBrains Mono already bundled) at 11–11.5 for SHAs and Git terms.
- **Radii**: 4 (keycaps), 5 (pills), 6 (list rows), 7 (buttons, inputs), 8–10 (popovers), 12 (palette, window).
- **Spacing**: 2/4/6/8/10/12/14/16/18 px; row height 38 (graph), 28–30 (headers/status).
- **Shadows**: tooltip `0 12px 30px #0009`; popover `0 18px 44px #000b`; palette `0 30px 80px #000c`.

## Assets
None bundled. Avatars = user photos or generated initials. Icons = replace Unicode placeholders with SourceGit's icon geometries.

## Files
- `Perfect Git.dc.html` — overview of the 3 states (3a default, 3b drag, 3c palette).
- `PG Screen.dc.html` — the window; source of truth for values and copy.
- `support.js` — runtime needed to open the HTML files locally.
