#!/usr/bin/env bash
# Purpose: fail early with an actionable message when a required tool is missing.
# Usage:   scripts/require-tool.sh <tool> <hint>
set -euo pipefail

# Tool name and the hint shown to the user when it is not on PATH.
tool="${1:?usage: require-tool.sh <tool> <hint>}"
hint="${2:-install it and retry}"

# Check the tool is resolvable (works for command names and absolute paths).
if ! command -v "$tool" >/dev/null 2>&1; then
  echo "error: '$tool' not found; $hint" >&2
  exit 1
fi
