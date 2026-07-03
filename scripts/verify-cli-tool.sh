#!/usr/bin/env bash
#
# Offline self-containment smoke for the `fsgg-sdd` dotnet tool (quickstart C6; FR-010).
#
# Proves the PACKED tool package is runtime self-contained: installed with NO org feed and
# NO FS.GG.SDD source on PATH, `fsgg-sdd registry validate` still runs — i.e. `dotnet pack`
# bundled the full runtime closure (the RegistryDocument YAML loader from FS.GG.SDD.Artifacts
# + YamlDotNet). This is the load-bearing new property of feature 044; it is verified over
# real fixtures, never a mock (Constitution VI).
#
# Run from the repo root:  bash scripts/verify-cli-tool.sh
# Set FSGG_SDD_PACKAGE_DIR=<dir> to validate an already-packed .nupkg (the release job points
# it at the artifacts dir it is about to push, so the smoke gates the EXACT pushed artifact);
# unset, the script packs a throwaway copy itself.
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
echo "Smoke: FS.GG.SDD.Cli $version (offline install -> run)"

work="$(mktemp -d)"
cleanup() { rm -rf "$work"; }
trap cleanup EXIT

tool_dir="$work/tool"
mkdir -p "$tool_dir"

# 1. Obtain the package source. Prefer an ALREADY-PACKED artifact when the caller supplies
#    one via FSGG_SDD_PACKAGE_DIR — the release job points this at the exact artifacts/packages
#    dir it is about to push, so the smoke validates the pushed .nupkg rather than a fresh
#    re-pack. Standalone/local runs (no env var) pack a throwaway copy.
if [ -n "${FSGG_SDD_PACKAGE_DIR:-}" ]; then
  pkg_dir="$FSGG_SDD_PACKAGE_DIR"
  echo "Validating pre-packed artifact in: $pkg_dir (no re-pack)"
else
  pkg_dir="$work/packages"
  mkdir -p "$pkg_dir"
  dotnet pack "$cli_proj" -c Release -p:Version="$version" -o "$pkg_dir"
fi
if ! ls "$pkg_dir"/FS.GG.SDD.Cli.*.nupkg >/dev/null 2>&1; then
  echo "::error::no FS.GG.SDD.Cli.*.nupkg found in $pkg_dir" >&2
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
