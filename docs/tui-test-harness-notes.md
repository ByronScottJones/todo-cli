# TUI Test Harness: Findings and MCP Spec Seed

Notes captured while building the headless test harness for the `Todo.Tui`
terminal UI (Terminal.Gui 2.5.0, .NET 10). Purpose: seed the specification of a
reusable MCP service that lets an agent launch, drive and inspect text user
interfaces.

Status: the harness described in section 2 works and is in use
(`src/Todo.CLI.Tests/Tui/UiSession.cs`). It is **in-process and
framework-specific**. A generic MCP service needs the PTY approach in section 5,
which has **not been built or tested** here.

## 1. Problem

An agent building a TUI cannot see it. Compiling and unit tests do not show
layout, focus, truncation, dialogs, key handling or resize behavior. Running the
real binary needs a terminal, and the agent's shell has no TTY. Needed: run the
UI, send keys, read the screen back as text, repeat.

## 2. What was built (in-process, Terminal.Gui-specific)

### 2.1 Mechanism

- `Application.Create()` returns an `IApplication`; `app.Init("ansi")` selects
  the **ANSI driver**, which needs no real terminal. Drivers registered in
  2.5.0: `windows`, `dotnet`, `ansi` (`DriverRegistry.GetDriverNames()`). There
  is **no `fake` driver** in this version; `Init("fake")` throws.
- `app.Driver.SetScreenSize(w, h)` fixes the virtual screen size. It can be
  called again mid-run to simulate a resize (follow with
  `app.LayoutAndDraw(true)`).
- `app.Driver.ToString()` returns the screen buffer as plain text, one line per
  row (characters only: no colors or styles). Wide characters occupy one string
  char but two cells, so line lengths look short.
- `app.Keyboard.RaiseKeyDownEvent(Key)` injects a key as if typed.
  `IApplication.GetInputInjector()` also exists (not tried).
- `app.Navigation.GetFocused()` returns the focused view (type name, text), and
  `app.TopRunnable` the topmost window or dialog. Reporting both with every
  capture made failures easy to diagnose.
- `app.AddTimeout(TimeSpan, Func<bool>)` schedules work on the UI loop;
  `app.Invoke(Action)` marshals from other threads; `app.RequestStop()` stops
  the top runnable.
- Runs fine under `dotnet test` (stdin/stdout redirected). No PTY needed.

### 2.2 Script runner design (the part worth reusing)

Steps are queued and executed one per timer tick (15 ms) on the UI thread:

1. Dequeue the next step.
2. **Schedule the next tick before running the step.** A step that opens a modal
   dialog blocks inside the dialog's nested loop; the next tick must be able to
   fire in that nested loop. If the next tick is scheduled afterwards (or the
   same callback is re-entered) nothing runs and the script hangs.
3. Run the step. A step returning `false` is put back at the front and retried
   (polling waits).
4. When the queue is empty: stop the top runnable repeatedly until only the main
   window remains, then stop it.
5. A global deadline (20 s) aborts a hung script with a message.

Step kinds: `Press(keys...)`, `Type(text)`, `Shot(name)`, `Settle(ticks)`,
`WaitUntil(predicate)`, `WaitIdle()`, `Resize(w,h)`, `Do(action)`.

### 2.3 Pitfalls found (each cost debugging time)

| Pitfall | Symptom | Fix |
| --- | --- | --- |
| Screen buffer is refreshed on the next loop iteration | A capture right after a key shows the previous screen; correct app looks broken | Call `app.LayoutAndDraw(true)` before reading the buffer |
| A key that opens a modal dialog blocks the caller | Later keys in the same call never arrive until the dialog closes; screenshot timer never fires | Drive everything from timer steps scheduled before the blocking call |
| Timer callback cannot re-enter itself | Script silently produces no output | One-shot timer per step, reschedule before running the step |
| Key names | `Key.TryParse("Down")` fails; `"CursorDown"` works. A single char is best built with `new Key(c)`. `"?"` parses to a plain `?` | Parse single chars separately; keep a table of valid names (`Alt+N`, `Ctrl+N`, `F1`, `Enter`, `Esc`, `Space`, `Delete`, `End`, `CursorDown`, `Shift+CursorDown`) |
| Printable keys never reach window-level key handlers | `?` did nothing while Space, Delete, F-keys and Alt chords worked | Focused `ListView` consumes printable keys for type-ahead. Subclass the list and override `OnKeyDown`, or handle in the focused control |
| Default Esc quits the whole app | App exited on Esc | `app.Keyboard.KeyBindings.Clear(Command.Quit)` |
| Dialog's default button is the last one added | `Cancel` was the default (`► Cancel ◄`), so Enter cancelled | Set `DefaultAcceptView`, `OkButton.IsDefault = true`, `CancelButton.IsDefault = false` |
| Dialog focuses its last button when it starts running | Typing went to a button, not the first field | Set focus in `IsRunningChanged` (not `Initialized`) |
| `ObservableCollection` re-set resets selection | Selection jumps after reload | Save and restore the selected item by id |
| Static state across tests | Risk of cross-test interference | Put UI tests in one xUnit collection with `DisableParallelization = true`; dispose the application after each test |
| Obsolete `TextView` | Compiler warning | Use `ListView` of lines for read-only scrolling text |

### 2.4 Things the harness verified that unit tests cannot

Layout at 100x24 and 62x14, truncation with an ellipsis, key bar wrapping onto
extra rows, a "terminal too small" warning below 60x12 and recovery on resize,
scroll indicators, dialog layout, inline validation messages, initial focus,
focus restored to the list after a dialog, Esc/F3 behavior, menu opening (F10,
Alt+F), modal flows (create, edit, delete confirm, search, list picker, help),
error dialogs when the service throws.

### 2.5 Reading colors back (added after the theming work)

`ToString()` is characters only, but `app.Driver.Contents[row, col]` is a `Cell`
with a nullable `Attribute` (foreground, background, style). Reading it showed
things a text dump cannot:

- the title bar, key bar and dialogs really use the theme colors;
- a **menu dropdown was rendering Terminal.Gui's default colors** because
  popover menus look their scheme up by name and ignored the scheme set on the
  view (fixed by registering schemes under the built-in names);
- the contrast of a focused button can be asserted numerically (WCAG ratio of
  the cell's foreground and background), which turned "hard to read" into a
  test.

Helpers worth copying into the MCP service: find text on screen (column, row),
read the attribute at a cell, compute contrast. Beware that named colors are
framework-specific (`DarkGray` was lighter than `Gray`), so tests should compare
resolved RGB values.

### 2.6 Known gaps of the in-process approach

- Plain text only: no colors, text styles, or cursor position. Color-blind
  palette and NO_COLOR behavior cannot be asserted from `ToString()`. The driver
  exposes `Contents` (a `Cell[,]` with attributes) and `ToAnsi()`, which were
  not explored but would add attributes.
- Tied to Terminal.Gui and to .NET; the app must be modified to run under the
  harness (inject a clock, an `Invoke` seam, a zero flash duration).
- Does not exercise real terminal behavior: raw mode, signal handling,
  Alt/Option encoding, bracketed paste, mouse protocol, alternate screen,
  terminal state restore on exit.

## 3. Capabilities an MCP "TUI driver" service should offer

Derived from what was needed repeatedly:

| Tool | Purpose |
| --- | --- |
| `launch` | Start a command in a PTY with `cols`, `rows`, `env`, `cwd`; returns a session id |
| `send_keys` | Named keys and chords (`Alt+N`, `Ctrl+C`, `F10`, `Shift+Tab`, `Esc`) |
| `type_text` | Literal text (bracketed paste option) |
| `screenshot` | Current screen as text rows; options: include attributes (fg/bg/bold/reverse), cursor position, trim trailing blanks |
| `wait_for` | Block until text/regex appears, screen is stable for N ms, or timeout; **return the screen on timeout** |
| `resize` | Change cols/rows; delivers SIGWINCH |
| `get_cursor` | Row, column, visibility |
| `scrollback` / `get_title` | Optional: terminal title (OSC), alternate-screen state |
| `send_signal` | SIGINT, SIGTERM, SIGHUP, to test clean shutdown |
| `exit_status` | Whether the process exited, with code, plus whether terminal modes were restored |
| `close` | Kill and clean up |

Design requirements learned the hard way:

1. **Every capture must be current.** Provide a "settle" notion (screen
   unchanged for N ms) instead of fixed sleeps; never return a stale frame.
2. **Report context with each capture:** focused element where known, screen
   size, process alive.
3. **Deterministic timing:** a clock-injection or "advance" facility is not
   possible for arbitrary apps, so expose explicit waits and generous but
   bounded timeouts, and return the last screen on failure.
4. **Key encoding fidelity:** make the encoding mode explicit (legacy xterm,
   kitty keyboard protocol, Alt as ESC prefix vs CSI u), because Alt/Option
   handling differs per terminal and the app under test may probe for it.
5. **Safety:** run commands only on request, restrict `cwd` and env, no network
   by default, kill on session close and on server shutdown, cap output size and
   session lifetime.

## 4. Suggested test-result shape

```json
{
  "size": {"cols": 100, "rows": 24},
  "cursor": {"row": 4, "col": 12, "visible": true},
  "lines": ["...", "..."],
  "attributes": "optional run-length encoded per row",
  "process": {"alive": true},
  "settled": true,
  "elapsed_ms": 240
}
```

## 5. Generic approach for the MCP service (not yet built or tested)

To work for any TUI (Go, Rust, Python, .NET) the service should run the program
under a real pseudo-terminal and interpret its output with a terminal emulator:

- PTY: `forkpty` / `openpty` (POSIX), ConPTY on Windows.
- Emulator to turn the byte stream into a screen model: for example `pyte`
  (Python), `vt100` or `termwiz` (Rust), `xterm-headless` (Node).
- Input: write encoded key bytes to the PTY master; resize with `TIOCSWINSZ`
  plus SIGWINCH.

Open questions to settle in the spec: implementation language; how to represent
colors and styles; whether to support the Kitty keyboard protocol and mouse
events; how sessions are authorized; whether the service runs inside the sandbox
or needs PTY access (PTY allocation may be blocked by this environment's
sandbox: untested).

## 6. Where the working code is

- `src/Todo.CLI.Tests/Tui/UiSession.cs`: script runner and capture
- `src/Todo.CLI.Tests/Tui/FakeTodoService.cs`: in-memory service with failure
  injection
- `src/Todo.CLI.Tests/Tui/MainWindowTests.cs`: about 40 headless UI tests using
  the runner
