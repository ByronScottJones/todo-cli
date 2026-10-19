# Handoff

State of the project at the end of the 2026-10-19 session, and what is left.
Read [AGENTS.md](AGENTS.md) and [ENGINEERING.md](ENGINEERING.md) first; this
file only lists open work and working agreements. Update or delete items as they
are finished.

## State

- `master` passes `make check`: format check, Markdown lint, and 407 tests,
  with warnings as errors.
- Merged this session: project scaffolding (Makefile, `AGENTS.md`,
  `ENGINEERING.md`, community files), tests for terminal restore and token
  handling, token cache hardening, System.CommandLine 3 and command-wiring
  tests, and the trimming fix for `make publish`.
- GitHub Actions workflows exist but are manual-only; there is no Actions
  budget. `make check` run locally is the quality gate.
- The license is MIT because the original project declares none.

## Open work

Roughly in priority order.

1. **Check the Microsoft To Do account.** Testing added two items named `x`
   to the default list by mistake (see Working agreements). One was marked
   completed; confirm both are gone or completed.
2. **Key store "not found" mapping.** `IKeyStorage.GetKey` must throw
   `KeyNotFoundException` only for a missing key. The Windows, macOS
   (`errSecItemNotFound`, -25300), and Linux implementations call native
   stores and are verified by review only. Exercise each on its OS.
3. **Release workflow and other platforms.** `release.yml` has never run, and
   `make publish` was only smoke-tested on osx-arm64 (`--version`, help, parse
   errors). win-x64, linux-x64, and osx-x64 are unbuilt. Either tag a
   pre-release such as `v0.3.0-rc1` and start the workflow from the Actions
   tab (uses Actions minutes), or build each platform locally.
4. **Live Graph and sign-in tests.** Nothing exercises real Microsoft Graph or
   MSAL sign-in. Add a manual smoke checklist or a gated integration suite.
   It is also the precondition for re-enabling trimming.
5. **System.CommandLine** is pinned to `3.0.0-rc.2.26504.105` by decision. Move
   to the 3.0 release when it ships and re-run `CommandLineWiringTests`.
6. **Trimming.** Release binaries are not trimmed (about 45 MB for osx-arm64
   instead of about 13 MB) because `Azure.Core`, Kiota, `InquirerCS`, and
   `RecyclableMemoryStream` raise `IL2104`. Revisit when they are trim-safe.
7. **Smaller items.**
   - A corrupt or wrong-key token cache is dropped silently; consider telling
     the user why they were asked to sign in again.
   - `Todo.CLI.csproj` hardcodes version `0.2.0`; release versions should come
     from the tag.
   - `todo list --bogus` treats `--bogus` as a list name (not a parse error).
   - Dependabot opens NuGet and Actions PRs weekly; review each one and run
     `make check`.
   - Merged remote branches on GitHub may still exist and can be deleted.

## Working agreements

- **Never run the built `todo` binary against real operations.** It uses the
  user's cached sign-in, so `add`, `remove`, and `complete` act on the real
  account. Compare builds with help, version, and parse errors only, and
  confirm the binary is fresh before running it. Prefer parse-level tests and
  mocked repositories.
- Work on a branch per change, push it, and open a pull request. The user
  reviews and merges; after a merge, pull `master`, run `make check`, and
  offer to delete the merged local branches.
- End commit messages and pull request descriptions with the attribution lines
  configured for the session.
- Inside the Claude Code sandbox, builds and `dotnet format` can hang or fail
  on sockets, and git cannot write `.git/config`. Run those unsandboxed, or use
  `make test DOTNET_ARGS="-m:1 -nodeReuse:false"`.
- `agents/` holds copies of the shared guidance in `~/.agents`; refresh them
  when the shared files change.
