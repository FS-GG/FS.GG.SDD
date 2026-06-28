#!/usr/bin/env bash
#
# Offline self-containment smoke for the `fsgg-sdd` dotnet tool (quickstart C6; FR-010).
#
# Proves the PACKED tool package is runtime self-contained: packed to a throwaway dir and
# installed with NO org feed and NO FS.GG.SDD source on PATH, `fsgg-sdd registry validate`
# still runs — i.e. `dotnet pack` bundled the full runtime closure (the RegistryDocument
# YAML loader from FS.GG.SDD.Artifacts + YamlDotNet). This is the load-bearing new property
# of feature 044; it is verified over real fixtures, never a mock (Constitution VI).
#
# Run from the repo root:  bash scripts/verify-cli-tool.sh
# Prints "C6 PASS" and exits 0 on success; fails loudly (exit non-zero) otherwise.

set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cli_proj="$repo_root/src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj"
good_fixture="$repo_root/tests/fixtures/registry/dependencies.yml"

# Evaluated CLI <Version> — the source of truth (matches the producer's resolve-versions).
version="$(dotnet msbuild "$cli_proj" -getProperty:Version | tr -d '[:space:]')"
if [ -z "$version" ]; then
  echo "::error::could not read <Version> from FS.GG.SDD.Cli.fsproj" >&2
  exit 1
fi
echo "Smoke: FS.GG.SDD.Cli $version (offline pack -> install -> run)"

work="$(mktemp -d)"
cleanup() { rm -rf "$work"; }
trap cleanup EXIT

pkg_dir="$work/packages"
tool_dir="$work/tool"
mkdir -p "$pkg_dir" "$tool_dir"

# 1. Pack the tool to a throwaway local source.
dotnet pack "$cli_proj" -c Release -p:Version="$version" -o "$pkg_dir"
if ! ls "$pkg_dir"/FS.GG.SDD.Cli.*.nupkg >/dev/null 2>&1; then
  echo "::error::pack produced no FS.GG.SDD.Cli.*.nupkg" >&2
  exit 1
fi

# 2. Install from the local source ONLY (no org feed) — proves self-containment.
dotnet tool install FS.GG.SDD.Cli \
  --tool-path "$tool_dir" \
  --add-source "$pkg_dir" \
  --version "$version"

fsgg_sdd="$tool_dir/fsgg-sdd"

# 3a. Success leg: a well-formed registry validates clean (exit 0).
if ! "$fsgg_sdd" registry validate "$good_fixture" --text; then
  echo "::error::C6 FAIL — installed tool could not validate the well-formed fixture (self-containment broken)" >&2
  exit 1
fi

# 3b. Failure leg: a malformed registry exits non-zero (loud failure preserved).
bad_fixture="$work/malformed.yml"
printf 'this: is: not: valid: registry\n  - broken\n' > "$bad_fixture"
if "$fsgg_sdd" registry validate "$bad_fixture" --text; then
  echo "::error::C6 FAIL — installed tool reported a malformed registry as valid (exit 0)" >&2
  exit 1
fi

echo "C6 PASS"
