# Security Policy

## Supported Versions

Only the latest release and the `master` branch receive security fixes.

## Reporting a Vulnerability

Do not open a public issue for a security problem. Report it privately through
[GitHub private vulnerability reporting](https://github.com/ByronScottJones/todo-cli/security/advisories/new).

Include the affected version, steps to reproduce, and the impact you expect.
You can expect an acknowledgement within a week. Fixes are released as soon as
they are verified, and reporters are credited unless they ask otherwise.

## Scope and Sensitive Data

Todo CLI signs in to Microsoft To Do with MSAL and stores the token cache
through platform key storage (DPAPI on Windows, Keychain on macOS, and a
file-based store on Linux). Reports about token handling, cache permissions,
logging of sensitive values, and unsafe handling of untrusted task text are in
scope. Never include real tokens or account data in a report.
