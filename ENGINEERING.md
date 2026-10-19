# Todo CLI Engineering Guide

Handover notes and development specification for the engineer or agent who
continues this project. Read it before making changes, and update it in the
same change set whenever architecture, decisions, contracts, verification
status, or known risks change.

Audience: contributors and coding agents. Agents start from
[AGENTS.md](AGENTS.md), which routes to this file and to the project-level
guidance in [`agents/`](agents/). User-facing documentation lives in
[README.md](README.md); contribution workflow lives in
[CONTRIBUTING.md](CONTRIBUTING.md); vulnerability reporting lives in
[SECURITY.md](SECURITY.md).

## 1. How to Develop in This Repository (Spec-Driven)

1. **Specify.** Write or amend the requirement in
   [section 4](#4-requirements) or the contract in
   [section 5](#5-contracts) before touching code. State observable behavior,
   inputs, outputs, and an acceptance criterion a test can check.
2. **Test.** Add the test that encodes the criterion and watch it fail, at the
   narrowest useful layer (see [section 10](#10-verification)).
3. **Implement.** Make the smallest change that satisfies the spec. Match
   existing conventions; keep unrelated refactors out.
4. **Verify.** Run `make check` (format check, Markdown lint, tests).
5. **Record.** Update this file and the README in the same change set.

### Definition of done

- The requirement or contract exists here and the code matches it.
- A test covers the acceptance criterion, including the failure path.
- `make check` passes, or the exact gap is recorded in
  [section 10.3](#103-verification-status).
- README, this file, the man page (`docs/todo.1`), and the HTML help
  (`docs/help.html`) agree with the code.
- Work is on a branch and merged by pull request; direct pushes to `master` are
  not used.

## 2. What This Is

Todo CLI is a cross-platform command-line client for Microsoft To Do, built on
.NET 10 and the Microsoft Graph SDK. It offers one-shot commands (`add`,
`list`, `complete`, `remove`) with interactive or JSON output, and a
full-screen terminal UI (`todo tui`, also bare `todo` in an interactive
terminal) built on Terminal.Gui.

This repository is a fork of `mehmetseckin/todo-cli`. The original declares no
license; see [LICENSE](LICENSE).

### Non-goals

- It is not a sync engine. Every UI mutation reloads from Microsoft To Do.
- It has no offline mode or local task database.
- It supports Microsoft accounts through the Graph `tasks.readwrite` scope
  only; other task providers are out of scope.

## 3. Tech Stack

| Concern | Choice |
| --- | --- |
| Language and runtime | C#, .NET 10 (`net10.0`), SDK pinned in `global.json` |
| Command line | `System.CommandLine` 3.0.0-rc.2 (pre-release, chosen deliberately) |
| Terminal UI | `Terminal.Gui` 2.5.0 |
| Microsoft To Do | Microsoft Graph SDK; MSAL for sign-in |
| Prompts | `phu.Inquirer.cs` |
| Configuration | `Microsoft.Extensions.Configuration` (JSON and environment) |
| Tests | xUnit, Moq, coverlet |
| Build and tasks | GNU Make, `dotnet format`, `markdownlint-cli2` |
| CI and release | GitHub Actions |

Dependency versions are pinned in the `.csproj` files. Dependabot proposes
updates weekly; review breaking changes rather than merging blindly.

## 4. Requirements

Each requirement lists the tests that prove it. A requirement with no test is a
known gap and appears in [section 11](#11-risks-gaps-and-future-work).

### 4.1 Functional requirements

| ID | Requirement | Verified by |
| --- | --- | --- |
| FR-1 | `todo add item <subject>` creates an item, optionally in `--list`, starred with `--star`, with `--due-date` as `yyyy-MM-dd` or `MM-dd`. | `AddCommandTests`, `DueDateParserTests`, `CommandLineWiringTests` |
| FR-2 | `todo add list <name>` creates a list. | `AddCommandTests` |
| FR-3 | `todo list` lists items, with options for list name, completed items, and age. | `ListCommandTests` |
| FR-4 | `todo complete` and `todo remove` act on ids, or on a list filtered by `--older-than`, `--all`, `--completed`. | `CompleteCommandTests`, `RemoveCommandTests` |
| FR-5 | `-o json` emits machine-readable output; the default is interactive. | `OutputFormatterTests` |
| FR-6 | Help is available as `-h`, `-H`, `--help`, `--HELP`, `-?` on every command, plus a man page and an HTML page. | `CommandLineWiringTests` (aliases); man page and HTML by review |
| FR-7 | The TUI supports create, read, update, delete, completion, importance, list choice, search, and theme choice, entirely from the keyboard. | `MainWindowTests`, `TodoWorkspaceTests`, `CommandKeysTests` |
| FR-8 | The TUI validates input (`ItemValidator`) and parses due dates (`DueDateParser`). | `ItemValidatorTests`, `DueDateParserTests` |
| FR-9 | Alt shortcuts work where Alt is available; Ctrl alternatives are advertised and always work. | `AltKeySupportTests`, `CommandKeysTests` |
| FR-10 | Themes are selectable at runtime, persisted per user, and every foreground/background pair meets WCAG AA contrast. | `ThemingTests`, `ThemeUiTests`, `TerminalAndThemeTests` |
| FR-11 | The terminal is restored on exit, errors, and `SIGINT`, `SIGTERM`, `SIGHUP`. | `TerminalRestoreTests` |

### 4.2 Non-functional requirements

| ID | Requirement | Verified by |
| --- | --- | --- |
| NFR-1 | Warnings are errors; `dotnet format` is clean. | `make build`, `make lint/fmt` in CI |
| NFR-2 | The token cache is encrypted at rest (AES-GCM) with a key held in platform key storage, never in the cache file. On Unix the cache is `0600` in a `0700` directory. A corrupt, tampered, or wrong-key cache counts as empty (the user signs in again), and a key store failure never replaces the stored key. Tokens are never logged or committed. | `EncryptedTokenFileTests`; no-logging by review only |
| NFR-3 | `NO_COLOR` is respected, and the UI degrades to ASCII without UTF-8. | `TerminalAndThemeTests` |
| NFR-4 | The UI recalculates layout on resize and keeps the focused row visible. | `ScrollWindowTests`, `MainWindowTests` |
| NFR-5 | Access tokens are refreshed per request, not captured once at startup. | `MsalAuthenticationProviderTests` |

## 5. Contracts

### 5.1 Commands

| Command | Description |
| --- | --- |
| `todo add item <subject>` | Add an item (`--list`, `--star`, `--due-date`) |
| `todo add list <name>` | Add a list |
| `todo list [list-name]` | List items (`--all`, `--no-status`, `--older-than`) |
| `todo complete <id...>` | Complete items (`--list`, `--older-than`, `--all`) |
| `todo remove item <id...>` | Remove items (`--list`, `--older-than`, `--all`, `--completed`) |
| `todo remove list <name>` | Remove a list |
| `todo tui` | Open the terminal UI |

The global option `-o`/`--output` selects `Interactive` (default) or `Json`.
JSON shapes live in `src/Todo.CLI/UI/JsonOutputModels.cs`; treat them as a
public contract and change them only with a documented compatibility note.

### 5.2 Configuration

Settings are read in increasing priority from `appsettings.json` next to the
executable, the per-user `settings.json` the UI writes, and `TODO_`-prefixed
environment variables with `__` as the separator. The full key reference
(`Tui:Theme`, `Tui:AltKeys`, `Tui:Palette:*`) is in the README. Configuration
failures must name the invalid key and never print secret values.

### 5.3 Exit behavior

Commands return a non-zero exit code on failure and write errors to stderr. The
TUI reports problems in its status line and never leaves the terminal in raw
mode.

## 6. Architecture

### 6.1 Projects

| Project | Role |
| --- | --- |
| `src/Todo.Core` | Models, repositories over Microsoft Graph, due-date parsing |
| `src/Todo.Tui` | Terminal UI: `Model` (UI-independent state), `Views` (Terminal.Gui only), `Input`, `Theming` |
| `src/Todo.CLI` | Entry point, commands, handlers, output formatters, MSAL auth, key storage |
| `src/Todo.CLI.Tests` | xUnit tests for all of the above |

Dependency direction: `Todo.CLI` references `Todo.Tui` and `Todo.Core`;
`Todo.Tui` references `Todo.Core`; `Todo.Core` references neither.

### 6.2 Data flow

Command: `Program` builds the host and DI container, `System.CommandLine`
dispatches to a command, its handler calls the repositories, and an output
formatter renders the result. TUI: `TuiHost` loads items before taking over the
screen (so first sign-in and failures use the normal console), then
`TodoWorkspace` mediates every operation and reloads after each mutation.

### 6.3 Module rules

- `Todo.Tui.Model` must not reference Terminal.Gui types; views stay thin.
- Colors never come from code literals in views; they come from themes and
  configuration.
- Repository access goes through `ITodoItemRepository` and
  `ITodoListRepository` so it can be faked in tests.

## 7. Key Decisions and Rationale

| Decision | Why | Rejected alternative |
| --- | --- | --- |
| Terminal.Gui for the TUI | Full-screen, keyboard-first, CUA-capable | Hand-rolled ANSI UI |
| UI reloads after every mutation | The screen never drifts from the service | Local optimistic cache |
| Per-request token acquisition | A long-lived UI outlives a captured token | One token at startup |
| `Ctrl+C` is Copy in the TUI | CUA overrides the general TUI guidance | "Ask to quit" |
| Per-user settings file for theme | `appsettings.json` may be read-only or shared | Rewriting `appsettings.json` |
| `Todo.Tui.Model` separate from views | The model is unit-testable with mocks | Logic inside views |

The full rationale for the terminal UI is in
[ADR 0001](docs/adr/0001-terminal-gui-tui.md). Record new significant
decisions as ADRs under `docs/adr/`.

## 8. Detailed Specifications

### 8.1 Authentication and key storage

MSAL acquires tokens silently, with an interactive browser fallback. The token
cache is an AES-GCM encrypted file (`EncryptedTokenFile`, wired up by
`TokenCacheHelper`) whose key is stored through `IKeyStorage` (`GetKey` throws
`KeyNotFoundException` only when no key exists): DPAPI-backed storage on
Windows, the Keychain on macOS, and `libsecret` on Linux (`KeyStorageFactory`
selects by platform; an unsupported platform throws
`PlatformNotSupportedException`). Scopes are `user.read` and
`tasks.readwrite`. The default client id in `TodoCliConfiguration` is a public
client registration, not a secret; it can be overridden through configuration.

### 8.2 Terminal UI

Key bindings and layout follow [the CUA specification](agents/CUA_TUI_Specification.md)
and [the TUI guidance](agents/tui.md). The user-facing key table is in the
README; keep it, `docs/todo.1`, and `docs/help.html` in step with
`CommandKeys`. Headless tests drive the real window through `UiSession`; see
[the harness notes](docs/tui-test-harness-notes.md).

## 9. Build, Run, and Validate

```bash
make help        # list targets
make build       # restore and build, warnings as errors
make test        # unit tests
make fmt         # apply C# formatting fixes
make check       # format check, Markdown lint, tests (same as CI)
make publish     # self-contained executable for this platform
make publish RID=linux-x64
```

Pass extra dotnet arguments with `DOTNET_ARGS`, for example
`make test DOTNET_ARGS="-m:1 -nodeReuse:false"` in constrained environments.
The GitHub Actions workflows are manual-only (`workflow_dispatch`) because
there is no Actions budget; `make check` run locally is the quality gate.
`ci.yml` runs `make check` on Linux, then tests on macOS and Windows.
`release.yml`, started on a `v*` tag, publishes four self-contained builds and
creates a pre-release.

## 10. Verification

### 10.1 Test layers

- **Unit:** commands, handlers, formatters, parsers, repositories, and the TUI
  model, with Moq fakes and `FakeTodoService`.
- **Headless UI:** `UiSession` runs the real Terminal.Gui window with the ANSI
  driver and reads the screen back as text.
- **Not covered:** live Microsoft Graph and real sign-in (see section 11).

### 10.2 Environment notes

Tests need no network or account. Building and `dotnet format` start local
build-host processes and sockets; sandboxes that block them may hang or fail,
which is an environment limit, not a code defect.

### 10.3 Verification status

| Area | Status | Date |
| --- | --- | --- |
| Unit and headless UI tests | 407 pass locally with warnings as errors | 2026-10-19 |
| `dotnet format --verify-no-changes` | Clean after the formatting commit | 2026-10-14 |
| `markdownlint-cli2` | Clean | 2026-10-14 |
| GitHub Actions workflows | Manual-only; never run (no Actions budget) | 2026-10-14 |
| Release workflow | Not yet exercised with a real tag | 2026-10-14 |
| Real Graph and MSAL sign-in | Not verified in this session | - |

## 11. Risks, Gaps, and Future Work

- **No live Graph tests.** Repository behavior against the real service is
  untested automatically. Add a manual smoke checklist or a gated integration
  suite.
- **Pre-release dependency.** `System.CommandLine` is pinned to
  `3.0.0-rc.2.26504.105` by decision. Move to the 3.0 release when it ships and
  re-run `CommandLineWiringTests`. The migration from 2.0 beta4 renamed the
  `[parse]` directive to `[diagram]`; `[suggest]` and the environment-variable
  directive are unchanged.
- **`make publish` fails on trimming.** With `PublishTrimmed` on (the Release
  default), `dotnet publish` fails with `IL2104` trim warnings from
  `Microsoft.Kiota.Serialization.Json` and `Microsoft.IO.RecyclableMemoryStream`.
  This predates the System.CommandLine 3 change (reproduced on `master`), so
  release builds are broken until the trimming settings or packages are fixed.
- **Command-line parsing for `list` is permissive.** `todo list --bogus` treats
  `--bogus` as a list name (the optional `list-name` argument accepts it), so
  it is not a parse error.
- **macOS Alt keys.** Terminals often do not send Option as Alt; Ctrl
  alternatives are the fallback. See the README section on Alt and Option.
- **Release workflow untested.** Validate with a pre-release tag before relying
  on it. It is manual-only because there is no Actions budget.
- **Upstream license.** The original project has no license; this fork is MIT.
  Re-check upstream periodically and switch if it sets one.
- **Key store "not found" mapping is untested.** `IKeyStorage.GetKey` must
  throw `KeyNotFoundException` only for a missing key; the Windows, macOS
  (`errSecItemNotFound`, -25300), and Linux implementations call native
  stores, so that mapping is verified by review only. A wrong mapping either
  fails sign-in (a missing key treated as an error) or silently replaces a key.
- **Corrupt cache is dropped silently.** A tampered, truncated, or wrong-key
  token cache is treated as empty and the user signs in again; nothing tells
  them why. Add diagnostics if that proves confusing.
- **Signal tests are Unix-only.** `TerminalRestoreTests` sends real signals to
  the test process and returns early on Windows.
- **Linux key storage** depends on `libsecret`; headless servers without a
  secret service cannot cache tokens.

## 12. Change Playbooks

### Add a command

1. Add the requirement in section 4 and the contract in section 5.1.
2. Add a `Commands/<Name>Command.cs` and a handler, and register it in
   `TodoCommand`.
3. Add tests, a JSON model if the output is structured, and update the README,
   `docs/todo.1`, and `docs/help.html`.

### Add a TUI key binding

1. Add it to `CommandKeys` with both Alt and Ctrl forms, avoiding menu
   mnemonics and `Ctrl+Q`.
2. Cover it in `CommandKeysTests` and a `MainWindowTests` scenario.
3. Update the README key table, the in-app help, the man page, and the HTML
   help.

### Add a theme

1. Add the palette to `Theming/TuiThemes.cs`; let `Derive` fill missing roles.
2. Confirm the contrast tests in `ThemingTests` pass without lowering targets.
3. Add it to the README theme table.

### Change a contract

Preserve compatibility, or document the break in the pull request and README,
update the affected tests, and record the decision here.
