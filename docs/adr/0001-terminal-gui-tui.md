# ADR 0001: Terminal.Gui for the full-screen terminal UI

- Status: accepted
- Date: 2026-10-13
- Audience: contributors to Todo CLI

## Context

The CLI only offered one-shot commands. Users wanted to browse and manage items
interactively with the full create, read, update and delete cycle, in a UI that
follows the Common User Access (CUA) conventions and works entirely from the
keyboard.

## Decision

Add a `Todo.Tui` project built on
[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) 2.5.0, launched with
`todo tui` and by bare `todo` in an interactive terminal.

- **Separation:** `Todo.Tui.Model` holds UI-independent state and operations
  (`TodoWorkspace`, validation, row formatting, key map). `Todo.Tui.Views` holds
  only Terminal.Gui views. The model is tested with mocks; the views are tested
  by running the real window headlessly.
- **Persistence:** every mutation reloads from Microsoft To Do, so the screen
  never drifts from the service.
- **Update support:** `ITodoItemRepository.UpdateAsync` was added to Core.
  Clearing a due date needs an explicit JSON `null`, which the Graph SDK omits
  for null model properties, so it is written through `AdditionalData`.
- **Authentication:** the previous provider captured one bearer token at
  startup. A long-lived UI outlives it, so the provider now asks MSAL for a
  token on every request (silent, with interactive fallback).
- **Startup:** items are loaded before the UI takes over the screen, so a first
  sign-in (which opens a browser) and any failure are reported in the normal
  console.
- **Keys:** CUA rules override the general TUI guidance where they conflict, so
  `Ctrl+C` is Copy rather than "ask to quit". Custom commands use `Alt+letter`
  avoiding the menu mnemonics; because macOS terminals often do not send Option
  as Alt, Ctrl alternatives are advertised when Alt cannot be confirmed, and
  both always work. `F3` inside a dialog cancels the dialog instead of exiting
  the application, so unsaved input is not lost.
- **Configuration:** the palette and Alt-key mode come from `appsettings.json`
  and `TODO_` environment variables, not from code. `NO_COLOR` is respected.

## Alternatives considered

- **Spectre.Console / Inquirer-style prompts:** simple, but not a full-screen,
  windowed, menu-driven UI.
- **Moving items between lists:** the service does not support it, so the list
  is read-only when editing.
- **Optimistic local updates:** faster feeling, but needs reconciliation on
  failure. Reloading is simpler and correct; revisit if latency is a problem.

## Consequences

- Terminal.Gui requires the Microsoft.Extensions.Configuration packages at
  10.0.11 or newer.
- The harness that drives the views headlessly depends on Terminal.Gui's ANSI
  driver; see [TUI test harness notes](../tui-test-harness-notes.md).
- Behaviors that cannot be verified without a real terminal and account (browser
  sign-in, clipboard, mouse, terminal restore after a signal) need manual checks
  before release.
