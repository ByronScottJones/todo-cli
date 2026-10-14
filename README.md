<p align="center">
    <img 
        src="./assets/logo.png"
        width="200"
        height="200"
    />
</p>

# Todo CLI

A cross-platform command-line interface to interact with Microsoft To Do, built using .NET 10.

## Build Status

| Platform | Status |
| ------ | ------------ |
| CI | [![CI build status](https://dev.azure.com/mtseckin/todo-cli/_apis/build/status/CI)](https://dev.azure.com/mtseckin/todo-cli/_build/latest?definitionId=1) |
| CD | [![Windows (x64) build status](https://dev.azure.com/mtseckin/todo-cli/_apis/build/status/CD)](https://dev.azure.com/mtseckin/todo-cli/_build/latest?definitionId=5)

## Getting Started

### Install

If you just want to give it a spin, head over to [releases](https://github.com/mehmetseckin/todo-cli/releases/). Download a release and extract to somewhere in your `PATH`, and run `todo --help` to get started.

### Build

```
# Clone the repository
git clone https://github.com/mehmetseckin/todo-cli.git

# Navigate into the source code folder
cd .\todo-cli\src

# Build the project
dotnet build
```

### Run

The application will automatically prompt you to sign in with your Microsoft account, and ask for your consent to access your data when needed.

```
# Run using dotnet run
dotnet run -p .\Todo.CLI -- --help

# Run from build output
.\src\publish\todo.exe --help
```

### Publish (self-contained .exe)

```
# Windows x64
dotnet publish src\Todo.CLI -c Release -r win-x64 -o .\src\publish

# macOS (Intel)
dotnet publish src\Todo.CLI -c Release -r osx-x64 -o .\src\publish

# macOS (Apple Silicon)
dotnet publish src\Todo.CLI -c Release -r osx-arm64 -o .\src\publish

# Linux x64
dotnet publish src\Todo.CLI -c Release -r linux-x64 -o .\src\publish
```

## Usage

### Commands

| Command | Description |
|---------|-------------|
| `todo add item <subject>` | Add a new to-do item |
| `todo add list <name>` | Add a new to-do list |
| `todo list [options]` | List items |
| `todo complete <id>` | Mark an item as completed |
| `todo remove <id>` | Remove an item |
| `todo tui` | Open the full-screen [terminal UI](#terminal-ui) (also what bare `todo` does in an interactive terminal) |

Help is available with `-h`, `-H`, `--help`, `--HELP` or `-?`. See also the [man
page](docs/todo.1) and the [HTML help](docs/help.html).

### Options

| Option | Description |
|--------|-------------|
| `--list <name>` | Target a specific list |
| `--star` | Mark the item as important |
| `--due-date <date>` | Set a due date (`yyyy-MM-dd` or `MM-dd`) |

### Examples

```
# Add an item with a due date
todo add item "Buy groceries" --due-date 2026-05-20

# Add an item with MM-dd format (current year is used)
todo add item "Doctor appointment" --due-date 06-15

# Add a starred item to a specific list
todo add item "Important report" --star --list Work

# List all tasks
todo list

# Mark a task as completed
todo complete <task-id>

# Remove a task
todo remove <task-id>
```

### Output Example

```
Work (3):
- Buy groceries - NotStarted (05-20)
✓ Completed task - Completed (2026-05-10)
- Important report - NotStarted
```

## Terminal UI

`todo tui` (or just `todo` in an interactive terminal) opens a full-screen,
keyboard-driven interface built with
[Terminal.Gui](https://github.com/gui-cs/Terminal.Gui) that follows the Common
User Access (CUA) conventions: a title bar, a menu bar, a list of items, a
status line and a key bar that shows the commands available.

It covers the whole life of an item: view, add, modify, complete or reopen, mark
important, and remove. You can switch between lists, show or hide completed
items, and search by subject. Every action works from the keyboard; mouse input
is accepted where the terminal supports it but is not covered by automated
tests.

If input or output is redirected (for example in a pipe), `todo tui` exits with
code 2 and a message. Bare `todo` then prints help instead.

### Keys

| Key | Action |
| --- | --- |
| `Alt+N` / `Ctrl+N` | New item |
| `Alt+M` / `Ctrl+E` (or `Enter`) | Modify the selected item |
| `Space`, `Alt+C` / `Ctrl+T` | Complete or reopen |
| `Alt+I` / `Ctrl+P` | Mark or unmark important |
| `Delete`, `Alt+R` / `Ctrl+R` | Remove (asks to confirm) |
| `Alt+L` / `Ctrl+L` | Choose the list shown |
| `?`, `Alt+S` / `Ctrl+F` | Search by subject |
| `Alt+A` / `Ctrl+A` | Show or hide completed items |
| `Alt+O` / `Ctrl+O` | Choose a color theme |
| `Ctrl+C` | Copy the selected subject |
| `F1` | Help |
| `F3`, `Alt+X` | Exit (`F3` closes an open dialog instead) |
| `F5` | Reload from Microsoft To Do |
| `F7` / `F8`, `PgUp` / `PgDn` | Page up / down |
| `F10` | Menu bar (`Alt+T`, `Alt+F`, `Alt+E`, `Alt+V`, `Alt+H` open menus) |
| `Tab` / `Shift+Tab` | Move between controls |
| `Esc` | Cancel a dialog or close a menu (it never exits the app) |

`Ctrl+Q` is not used because terminals reserve it for flow control. In a dialog,
`Enter` is `[ OK ]` and `Esc` is `[ Cancel ]`.

### Item form

- **Subject** is required.
- **Due date** is optional: `yyyy-MM-dd` or `MM-dd` (current year). Leave it
  blank for none; clearing it on an existing item removes the due date.
- **Important** is a checkbox.
- **List** is chosen when adding. Items cannot be moved between lists.

`[ OK ]` stays disabled and an inline `!` message is shown until the form is
valid.

### Alt and Option keys on macOS

Many macOS terminals capture the Option key instead of sending it as Alt. At
startup the UI checks whether Alt can be relied on. For Terminal.app it reads
the `useOptionAsMetaKey` setting of the default profile; other macOS terminals
cannot be probed, so they count as unconfirmed. When Alt is unconfirmed, the key
bar and menus show the Ctrl alternatives. Both sets of keys always work.

To use Alt on macOS, enable **Use Option as Meta key** in your terminal profile,
or force the behavior with the `Tui:AltKeys` setting below.

### Themes

Choose a theme with **View > Theme...** or `Alt+O` / `Ctrl+O`. The colors
preview as you move through the list; `Enter` applies and saves the choice,
`Esc` restores the previous theme.

| Theme | Notes |
| --- | --- |
| Default | 16-color friendly: white on black, blue bars, light gray dialogs |
| One Dark Pro | `#282c34` background |
| Dracula | `#282a36` background |
| Tokyo Night | `#1a1b26` background |
| Catppuccin Mocha | `#1e1e2e` background |
| GitHub Dark | `#0d1117` background |
| Night Owl | `#011627` background |
| Gruvbox Material | `#282828` background |
| SynthWave '84 | `#262335` background |

Terminals without true-color support approximate the hex colors with the nearest
of their 16 or 256 colors, so the themes look best in a true-color terminal.

Every theme is readable by construction: each text color is checked against the
background it is drawn on and, if it is too faint, its lightness is adjusted
(keeping its hue) until it reaches a 4.5:1 contrast ratio (3:1 for dimmed text
such as disabled controls). That applies to your own palette entries too.

For the built-in themes, the background, foreground, the two highlight colors
and the comment color come from the theme's editor palette: the "function" color
is used for the title, menu and key bars and for highlighted dialog controls,
the "keyword" color for the selected row and menu item, and the comment color
for dimmed text. The roles those palettes do not define are derived in the
perceptual OKLCH color space so they match the theme:

- **Dialog surface**: the background, slightly lighter, with the same hue.
- **Warning** (shortcut letters) and **error** (messages): amber and red with
  the same visual weight as the theme's highlights. They also differ in
  lightness, not just hue, so they stay distinguishable with red-green color
  blindness.

### Configuration

Settings are read from, in increasing priority:

1. `appsettings.json` next to the executable (optional).
2. The per-user settings file the UI writes when you choose a theme:
   `~/Library/Application Support/todo/settings.json` on macOS,
   `~/.config/todo/settings.json` on Linux, `%APPDATA%\todo\settings.json` on
   Windows.
3. Environment variables prefixed `TODO_`, with `__` as the separator, for
   example `TODO_Tui__Theme=Dracula` or `TODO_Tui__AltKeys=on`.

```json
{
  "Tui": {
    "AltKeys": "auto",
    "Theme": "Dracula",
    "Palette": {
      "Accent": "#7aa2f7"
    }
  }
}
```

- `AltKeys`: `auto` (detect), `on` (trust Alt) or `off` (advertise Ctrl keys).
- `Theme`: a theme name from the table above. Case, spaces and punctuation are
  ignored, so `tokyo-night` works. An unknown name falls back to Default, and
  the status line says so.
- `Palette`: optional single-color overrides applied on top of the chosen theme.
  The entries are `Background`, `Foreground`, `Accent`, `AccentText`,
  `Selection`, `SelectionText`, `Warning`, `Error`, `Muted`, `DialogBackground`
  and `DialogText`. Values are terminal color names (for example `Black`,
  `BrightRed`) or hex. An unrecognized color is replaced by its default, and the
  UI says so in the status line. Do not list the whole palette in
  `appsettings.json`: it would override every theme.
- Colors are never the only signal: completed items use `☑`, important ones `★`,
  overdue ones `!`, and errors begin with `!`.
- Set `NO_COLOR` to any non-empty value for a monochrome display. Without a
  UTF-8 locale the UI falls back to ASCII symbols (`[x]`, `*`).

The terminal is restored on exit, on errors, and on `SIGINT`, `SIGTERM` or
`SIGHUP` sent from outside the UI. Inside the UI, `Ctrl+C` copies instead of
interrupting.

## Contributing

Interested? You are awesome. Feel free to fork, do your thing and send a PR! Everything is appreciated.

## Code of Conduct

Be nice to people, give constructive feedback, and have fun!

## Stack

This project is built using the following nuggets of awesomeness, and many more. Many thanks to the folks who are working on and maintaining these products.

- [.NET 10](https://github.com/dotnet/core)
- [Terminal.Gui](https://github.com/gui-cs/Terminal.Gui)
- [System.CommandLine](https://github.com/dotnet/command-line-api)
- [Microsoft Graph Beta SDK](https://github.com/microsoftgraph/msgraph-beta-sdk-dotnet)
- [Inquirer.cs](https://github.com/hayer/Inquirer.cs)
