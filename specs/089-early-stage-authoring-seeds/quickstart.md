# Quickstart: validating Early-Stage Authoring Seeds

**Feature**: 089-early-stage-authoring-seeds

This guide reproduces the two defects end-to-end and states the outcome the feature must produce.
Every "before" block below was captured from `main` @ `66545c9` — see `research.md` §D-Baseline.

## Prerequisites

Building in this sandbox requires bypassing the lock file, which cannot be restored here
(research E1 — an environment artifact, **not** a repo defect; CI's `--locked-mode` gate is green on
`main`). Never commit a regenerated `packages.lock.json`.

```sh
SCRATCH=$(mktemp -d)
dotnet build src/FS.GG.SDD.Cli/FS.GG.SDD.Cli.fsproj \
  -p:RestorePackagesWithLockFile=false \
  -p:NuGetLockFilePath="$SCRATCH/nolock.json"

CLI="$PWD/src/FS.GG.SDD.Cli/bin/Debug/net10.0/FS.GG.SDD.Cli"
git status --short -- '*packages.lock.json'   # must print nothing
```

## Setup: a work item with two ambiguities and no story

```sh
W=$(mktemp -d) && cd "$W"
"$CLI" init --text
"$CLI" charter --work demo --title "Export a session recording as a video file" --text
"$CLI" specify --work demo --input "value: Let a player keep a highlight of their match
scope: Encode the captured frame buffer to a shareable file
requirement: The exported file plays back in a standard media player
ambiguity: Which container format should the export use
ambiguity: What is the maximum recording length" --text
```

## Scenario 1 — §WD7: the seed reads as the feature, not the process

```sh
sed -n '/## User Stories/,/## Functional Requirements/p' work/demo/spec.md
```

**Before** (the defect):

```markdown
- US-001 (P1): As a maintainer, I can specify Demo after chartering the work item.
- AC-001 [US-001] [FR-001]: Given a chartered work item, when specify runs with intent, then spec.md is created with stable ids.
```

**After** (expected):

```markdown
- US-001 (P1): As a user, I can let a player keep a highlight of their match.
- AC-001 [US-001] [FR-001]: Given Demo is available, when the user exercises it, then they can let a player keep a highlight of their match.
```

Checks: the ids and `[US-001] [FR-001]` references are unchanged (FR-004); neither line contains
`charter`, `specify`, `spec.md`, or `stable ids` (SC-007); the `FR-001` line still ends
`(Stories: US-001; Acceptance: AC-001)`.

Note the title renders as **`Demo`**: `specify` reads `--title` from its own invocation, not from the
charter (research D1). The user value is what carries the meaning.

## Scenario 2 — §WD5: a blocked clarify leaves a truthful skeleton

```sh
"$CLI" clarify --work demo --text; echo "rc=$?"
ls work/demo/ readiness/ 2>&1
```

**Before**: `outcome: blocked`, `changedArtifacts: 0`, no `clarifications.md`, no `readiness/`.

**After** (expected):
- `outcome: blocked` and the same exit code and diagnostics as before (FR-010, SC-003);
- `changedArtifacts: 1` — the only report delta (SC-001);
- `work/demo/clarifications.md` exists with `status: needsAnswers` (SC-002);
- both ambiguities appear under Remaining Ambiguity as `blocking`;
- **still no `readiness/`** — the carve-out passes only the skeleton, not the generated view.

```sh
grep '^status:' work/demo/clarifications.md            # → status: needsAnswers
sed -n '/## Remaining Ambiguity/,/## Lifecycle/p' work/demo/clarifications.md
find . -name work-model.json                           # → nothing
```

### Scenario 2b — the skeleton's entries must *really* block

The sharpest check on the skeleton (FR-021 / K9 / K10). A skeleton whose explanation text mentioned
"an accepted deferral" as an option would parse as an accepted deferral, zero the blocking count, and
let this command pass with both ambiguities unanswered.

```sh
"$CLI" clarify --work demo --text | grep blockingAmbiguities   # → blockingAmbiguities: 2
"$CLI" checklist --work demo --text | grep -E '^outcome|^why'; echo "rc=$?"
#   → outcome: blocked
#   → why: Blocking ambiguity remains unresolved after clarification planning.
#   → rc=1
```

## Scenario 3 — the trap: answering the skeleton must actually unblock

This is the scenario that fails today and is the reason FR-018 exists.

```sh
"$CLI" clarify --work demo --input "AMB-001: Use the MP4 container
AMB-002: Cap recordings at ten minutes" --text | grep -E '^outcome|blockingAmbiguities'
"$CLI" checklist --work demo --text | grep -E '^outcome|^why|^next'; echo "rc=$?"
```

**Before** (with a hand-placed skeleton — the tool never writes one today):

```
outcome: succeeded          <-- clarify claims success...
blockingAmbiguities: 2      <-- ...with two ambiguities still blocking

outcome: blocked
why: Blocking ambiguity remains unresolved after clarification planning.
rc=1
```

**After** (expected):

```
outcome: succeeded
blockingAmbiguities: 0

outcome: succeeded
next: fsgg-sdd plan
rc=0
```

Also check the retirement rules landed:

```sh
sed -n '/## Remaining Ambiguity/,/## Lifecycle/p' work/demo/clarifications.md
#   → No blocking ambiguity remains.            (R1 / FR-018)
sed -n '/## Decisions/,/## Accepted/p' work/demo/clarifications.md
#   → DEC-001 and DEC-002, with NO "No concrete decisions recorded." line   (R2 / FR-019)
grep '^status:' work/demo/clarifications.md
#   → status: clarified                         (R3 / FR-020)
```

Note "`AMB-001: … / AMB-002: …`" means **both** answers in one invocation. A partially answered run
blocks and persists nothing — pre-existing behavior, unchanged (research D10):

```sh
"$CLI" clarify --work demo --input "AMB-001: Use the MP4 container" --text \
  | grep -E '^outcome|^changedArtifacts'      # → blocked / changedArtifacts: 0, file untouched
```

## Scenario 4 — re-running is safe

```sh
# From a fresh blocked skeleton: re-run with no answers.
"$CLI" clarify --work demo --text | grep -E '^outcome|^why'      # still blocked, same diagnostic
grep -c '^- CQ-' work/demo/clarifications.md                     # → 2, never 4 (FR-013)

# Byte-identical skeleton across two blocked runs (FR-015).
cp work/demo/clarifications.md /tmp/first && rm work/demo/clarifications.md
"$CLI" clarify --work demo --text >/dev/null
diff /tmp/first work/demo/clarifications.md && echo "deterministic"
```

## Scenario 5 — an existing artifact is never clobbered (FR-011, SC-006)

```sh
printf '<!-- fsgg-sdd: unsafe-overwrite -->\n' > work/demo/clarifications.md
sha256sum work/demo/clarifications.md > /tmp/before
"$CLI" clarify --work demo --text | grep -E '^outcome'
sha256sum -c /tmp/before && echo "not clobbered"
```

Repeat with a malformed front matter and with a mismatched `workId`; in each case the pre-existing
diagnostic is reported and the file's digest is unchanged.

## Scenario 6 — the happy path is untouched (FR-005, SC-009)

```sh
"$CLI" specify --work other --input "value: …
scope: …
requirement: …
story: As an operator, I can roll back a bad deploy
acceptance: Given a bad deploy, when I roll back, then the prior version serves traffic"
```

The author's story and scenario appear verbatim; no seed is substituted.

## Automated coverage

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests \
  -p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath="$SCRATCH/nolock.json"
dotnet test tests/FS.GG.SDD.Artifacts.Tests \
  -p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath="$SCRATCH/nolock.json"
```

Scenarios 1–6 map onto `tests/FS.GG.SDD.Commands.Tests/EarlyStageSeedTests.fs`; the skeleton's
parse + blocking-count invariant (K5) is asserted in
`tests/FS.GG.SDD.Artifacts.Tests/ClarificationArtifactTests.fs`.
