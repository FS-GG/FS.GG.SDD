#!/usr/bin/env bash
# resolve-acceptance-registry.sh — deterministic registry-source resolver for the
# network-gated composition-acceptance (feature 041, consumer half of the
# `composition-registry-updated` cross-repo dispatch contract; see
# specs/041-composition-acceptance-dispatch/contracts/registry-dispatch.md).
#
# It selects exactly ONE registry source by a deterministic precedence, materializes
# the chosen content verbatim to an ephemeral runner file, and reports the path the
# workflow exports as FSGG_SDD_ACCEPTANCE_REGISTRY. The unchanged acceptance facts
# then run over that path — the source is the only thing that varies (FR-006).
#
# Generic SDD carries NO rendering identity: this script never names a rendering
# package id, template id, path, or docs URL (FR-003 / SC-003). The registry content
# is consumed opaquely and never committed; the materialized file is ephemeral run
# state under RUNNER_TEMP.
#
# ---- Environment contract (the single source of truth tests + workflow assert against) ----
#
#   REGISTRY_PATH_INPUT              manual `registry_path` workflow_dispatch input (precedence 1)
#   FSGG_DISPATCH_REGISTRY_CONTENT   dispatched client_payload.registry_content    (precedence 2)
#   FSGG_DISPATCH_REGISTRY_SHA256_12 dispatched client_payload.registry_sha256_12  (drift signal / integrity)
#   REGISTRY_SECRET_CONTENT          scheduled secret content                       (precedence 3)
#   GITHUB_EVENT_NAME                the triggering event (`repository_dispatch` selects the dispatch branch)
#   RUNNER_TEMP                      ephemeral runner temp dir (materialization target root)
#
# ---- Precedence (FR-004) ----
#
#   1. manual REGISTRY_PATH_INPUT (non-empty)           → export that path as-is
#   2. dispatch (GITHUB_EVENT_NAME=repository_dispatch) → materialize FSGG_DISPATCH_REGISTRY_CONTENT
#   3. scheduled REGISTRY_SECRET_CONTENT (non-empty)    → materialize the secret content
#   else                                                → fail closed (no source)
#
# ---- Fail-closed rules (FR-005 / data-model.md) ----
#
#   * dispatch with missing/empty FSGG_DISPATCH_REGISTRY_CONTENT          → `::error::` + non-zero exit
#   * dispatch whose materialized first-12 sha256 ≠ advertised sha256_12  → `::error::` + non-zero exit
#   Never a false green; never a silent skip.
#
# ---- Modes ----
#
#   (default)        append `FSGG_SDD_ACCEPTANCE_REGISTRY=<path>` to $GITHUB_ENV and the drift
#                    signal to $GITHUB_STEP_SUMMARY (when set), and print the resolved path.
#   --print-env      print `FSGG_SDD_ACCEPTANCE_REGISTRY=<path>` to stdout for `eval` (quickstart
#                    Scenario 3), with no $GITHUB_ENV/$GITHUB_STEP_SUMMARY side effects.

set -euo pipefail

# Mode: default (CI side effects) or --print-env (stdout only, for `eval`).
print_env=0
if [ "${1:-}" = "--print-env" ]; then
  print_env=1
fi

# Defaults so `set -u` never trips on an unprovided source.
REGISTRY_PATH_INPUT="${REGISTRY_PATH_INPUT:-}"
FSGG_DISPATCH_REGISTRY_CONTENT="${FSGG_DISPATCH_REGISTRY_CONTENT:-}"
FSGG_DISPATCH_REGISTRY_SHA256_12="${FSGG_DISPATCH_REGISTRY_SHA256_12:-}"
REGISTRY_SECRET_CONTENT="${REGISTRY_SECRET_CONTENT:-}"
GITHUB_EVENT_NAME="${GITHUB_EVENT_NAME:-}"
RUNNER_TEMP="${RUNNER_TEMP:-/tmp}"

# First-12 lowercase hex of sha256 over a file's bytes (sha256sum or shasum -a 256).
sha256_first12() {
  if command -v sha256sum >/dev/null 2>&1; then
    sha256sum "$1" | cut -c1-12
  else
    shasum -a 256 "$1" | cut -c1-12
  fi
}

# Materialize content verbatim (byte-for-byte; multi-line / special chars preserved) to the
# ephemeral registry file and echo its path. Never committed; deleted with the runner.
materialize() {
  local target="${RUNNER_TEMP}/fsgg/providers.yml"
  mkdir -p "${RUNNER_TEMP}/fsgg"
  printf '%s' "$1" > "$target"
  printf '%s' "$target"
}

resolved_path=""
drift_sha=""

if [ -n "$REGISTRY_PATH_INPUT" ]; then
  # Precedence 1: explicit manual input — export the checked-out path as-is (no materialize,
  # no integrity check; this source advertises no sha).
  resolved_path="$REGISTRY_PATH_INPUT"
elif [ "$GITHUB_EVENT_NAME" = "repository_dispatch" ]; then
  # Precedence 2: the dispatched registry content. Fail closed on empty content (FR-005).
  if [ -z "$FSGG_DISPATCH_REGISTRY_CONTENT" ]; then
    echo "::error::composition-registry-updated dispatch carried empty registry_content; failing closed (no false green, no silent skip)." >&2
    exit 1
  fi
  resolved_path="$(materialize "$FSGG_DISPATCH_REGISTRY_CONTENT")"
  # Integrity cross-check (D5): recompute the materialized file's sha and require it to equal
  # the advertised registry_sha256_12. A mismatch (or a missing advertised value) is a wiring
  # defect — fail closed.
  actual_sha="$(sha256_first12 "$resolved_path")"
  if [ "$actual_sha" != "$FSGG_DISPATCH_REGISTRY_SHA256_12" ]; then
    echo "::error::dispatched registry integrity mismatch: recomputed sha256[0:12]=${actual_sha} != advertised registry_sha256_12=${FSGG_DISPATCH_REGISTRY_SHA256_12:-<empty>}; failing closed." >&2
    rm -f "$resolved_path"
    exit 1
  fi
  drift_sha="$actual_sha"
elif [ -n "$REGISTRY_SECRET_CONTENT" ]; then
  # Precedence 3: the scheduled secret content (advertises no sha).
  resolved_path="$(materialize "$REGISTRY_SECRET_CONTENT")"
else
  echo "::error::No registry provided (set the FSGG_SDD_ACCEPTANCE_REGISTRY secret, pass registry_path, or dispatch composition-registry-updated)." >&2
  exit 1
fi

if [ "$print_env" -eq 1 ]; then
  # Stdout only — caller `eval`s this to set the env (quickstart Scenario 3).
  printf 'FSGG_SDD_ACCEPTANCE_REGISTRY=%s\n' "$resolved_path"
else
  # CI: export for downstream steps, surface the drift signal, and echo a human log line.
  if [ -n "${GITHUB_ENV:-}" ]; then
    printf 'FSGG_SDD_ACCEPTANCE_REGISTRY=%s\n' "$resolved_path" >> "$GITHUB_ENV"
  fi
  if [ -n "${GITHUB_STEP_SUMMARY:-}" ] && [ -n "$drift_sha" ]; then
    {
      echo "### composition-acceptance registry"
      echo ""
      echo "- tested registry content sha256[0:12]: \`${drift_sha}\`"
      if [ -n "$FSGG_DISPATCH_REGISTRY_SHA256_12" ]; then
        echo "- advertised registry_sha256_12: \`${FSGG_DISPATCH_REGISTRY_SHA256_12}\`"
      fi
    } >> "$GITHUB_STEP_SUMMARY"
  fi
  echo "Resolved FSGG_SDD_ACCEPTANCE_REGISTRY=${resolved_path}"
fi
