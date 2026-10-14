# Contributing

Thanks for helping. This guide covers the workflow for contributors; agents
should also read [AGENTS.md](AGENTS.md) and [ENGINEERING.md](ENGINEERING.md).

## Setup

Install the .NET SDK version in [`global.json`](global.json), `make`, and
Node.js (used only for the Markdown linter). Then:

```bash
make deps    # restore packages
make check   # format check, Markdown lint, tests
make help    # list every target
```

## Workflow

1. Create a branch from `master`; do not commit to `master` directly.
2. Specify the behavior first: amend the requirement or contract in
   [ENGINEERING.md](ENGINEERING.md), then add a failing test.
3. Make the smallest change that satisfies it. Keep unrelated refactors out.
4. Run `make check`. Warnings are errors, and `dotnet format` is authoritative
   for C# style.
5. Use short, conventional commit messages such as `feat:`, `fix:`, `docs:`.
6. Open a pull request and fill in the template. CI must pass.

## Tests

Tests use xUnit and Moq in `src/Todo.CLI.Tests`. Cover success paths, failure
paths, and empty states. The TUI is tested headlessly through `UiSession`; see
[the harness notes](docs/tui-test-harness-notes.md).

## Security

Never commit tokens or account data. Report vulnerabilities privately as
described in [SECURITY.md](SECURITY.md).

## License

By contributing you agree that your contribution is released under the
[MIT License](LICENSE).
