# Quickstart: publish + verify the `fsgg-sdd` dotnet tool

Runnable validation for the two-package producer. The offline smoke (C6) needs no network; the
publish + feed checks (C1–C5) run against the org feed via the workflow.

## Prerequisites

- .NET SDK `10.0.x`.
- For publish/feed steps: push access to the canonical `FS-GG/FS.GG.SDD` repo (the workflow uses
  the run-scoped `GITHUB_TOKEN`; no PAT). For feed queries: `gh` authenticated to `FS-GG`.
- A registry fixture to validate, e.g. `tests/fixtures/registry/dependencies.yml` (well-formed).

## C6 — Offline self-containment smoke (the load-bearing local check, FR-010)

Proves the packed tool runs `registry validate` standalone — no SDD source, no feed. Run from a
clean checkout:

```bash
set -euo pipefail
TOOLDIR="$(mktemp -d)"; OUT="$(mktemp -d)"

# 1. Pack the tool at its evaluated product-line version.
dotnet pack src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -c Release -o "$OUT"
ls "$OUT"/FS.GG.SDD.Cli.*.nupkg            # must be non-empty (FR-007)

# 2. Install it into a throwaway tool path FROM THE LOCAL ARTIFACTS ONLY (no org feed).
dotnet tool install FS.GG.SDD.Cli --tool-path "$TOOLDIR" --add-source "$OUT" \
  --version "$(dotnet msbuild src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj -getProperty:Version | tr -d '[:space:]')"

# 3. Run registry validate against a well-formed fixture — expect success (exit 0).
"$TOOLDIR/fsgg-sdd" registry validate tests/fixtures/registry/dependencies.yml --text

# 4. Run against a malformed file — expect a non-zero exit and reported violations.
printf 'not: [valid\n' > "$OUT/bad.yml"
if "$TOOLDIR/fsgg-sdd" registry validate "$OUT/bad.yml" --text; then
  echo "FAIL: malformed registry should have exited non-zero"; exit 1
fi
echo "C6 PASS: the installed tool loaded + validated YAML with no SDD source present."
```

Step 3 succeeding is the proof that the YAML loader (`FS.GG.SDD.Artifacts` + `YamlDotNet`) is
bundled in the tool package — closing gap #2 from issue #31.

## C1 — Dry run (no publish)

```bash
gh workflow run release.yml -R FS-GG/FS.GG.SDD          # workflow_dispatch, no version input
gh run watch -R FS-GG/FS.GG.SDD
```

Expected: both packages pack, **nothing** is pushed. Confirm no new feed versions appear (C5).

## C2/C3 — Real publish (both packages) + idempotent re-run

Publish via dispatch with an explicit Contracts version (the feature-043 path; also publishes the
CLI at its evaluated `0.2.0`):

```bash
gh workflow run release.yml -R FS-GG/FS.GG.SDD -f version=1.1.0
gh run watch -R FS-GG/FS.GG.SDD
```

Expected: `publish-contracts` pushes `FS.GG.Contracts 1.1.0`; `publish-cli` pushes
`FS.GG.SDD.Cli 0.2.0`. Re-running the same command completes green and pushes no duplicates
(`--skip-duplicate`, C3). A product release tagged `v0.2.0` matches the CLI line and likewise
publishes both (C2); a tag matching neither line fails loudly.

## C5 — Feed verification

```bash
gh api /orgs/FS-GG/packages?package_type=nuget --jq '.[].name'   # must include FS.GG.SDD.Cli
gh api '/orgs/FS-GG/packages/nuget/FS.GG.SDD.Cli/versions' --jq '.[].name'
```

Then confirm a clean consumer install from the feed (US1, the `.github#49` shape):

```bash
dotnet tool install --global FS.GG.SDD.Cli --version 0.2.0 \
  --add-source https://nuget.pkg.github.com/FS-GG/index.json
fsgg-sdd registry validate registry/dependencies.yml --text
```

## One-time operational step — make the feed package public (FR-011)

After the **first** `FS.GG.SDD.Cli` publish, set its org package visibility to **Public** (as was
done for `FS.GG.Contracts`): GitHub → `FS-GG` org → Packages → `FS.GG.SDD.Cli` → Package settings →
Change visibility → Public (and/or link it to the `FS.GG.SDD` repo). Until this is done, consumer
CI in other repos cannot restore it with only their run-scoped token.

## Done when

- C6 passes locally (offline self-containment).
- A real publish lists `FS.GG.SDD.Cli` on the org feed (C5) and a clean consumer install + `registry
  validate` succeeds.
- The package is public, unblocking FS-GG/.github#49.
