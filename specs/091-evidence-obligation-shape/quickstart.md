# Quickstart: Slim the Evidence Declaration Shape

Runnable validation scenarios proving the feature end-to-end. Run from repo root.

## Prerequisites

```bash
dotnet build FS.GG.SDD.sln
CLI="dotnet run --project src/FS.GG.SDD.Cli --"
```

You need a work item that has reached the `evidence` stage (charter → specify → clarify → checklist
→ plan → tasks → analyze). The test fixture `initializedAnalyzedProject` does this in-process; the
scenarios below assume an existing workspace with `work/<id>/tasks.yml` and
`readiness/<id>/analysis.json`.

## Scenario 1 — A scaffolded evidence.yml carries no `null` boilerplate (FR-001, FR-002, SC-001)

```bash
$CLI evidence --id 001-example
grep -cE '^\s+(syntheticDisclosure|rationale|owner|scope|laterLifecycleVisibility):' \
  work/001-example/evidence.yml
```

Expected: `0`. Every declaration ends `synthetic: false` → `notes: []` with nothing between.

Before this feature the same grep returned `5 × <obligation count>` (e.g. `80` at 16 obligations).

## Scenario 2 — No blank lines were left behind (FR-004)

```bash
grep -n '^\s*$' work/001-example/evidence.yml          # no output inside the evidence: block
grep -nE ' +$' work/001-example/evidence.yml           # no trailing whitespace
$CLI evidence --id 001-example --json | jq -e '.outcome != "Blocked"'
```

Expected: the two greps print nothing; the re-parse succeeds (a malformed emission would block).

## Scenario 3 — Re-runs are byte-idempotent (FR-007, issue #161)

```bash
$CLI evidence --id 001-example >/dev/null
cp work/001-example/evidence.yml /tmp/first.yml
$CLI evidence --id 001-example >/dev/null
diff /tmp/first.yml work/001-example/evidence.yml && echo IDEMPOTENT
```

Expected: `IDEMPOTENT`, no diff.

## Scenario 4 — An old verbose file still parses, then normalizes once (FR-005, R5)

Hand-write a declaration in the old shape:

```bash
python3 - <<'PY'
import re, pathlib
p = pathlib.Path("work/001-example/evidence.yml")
t = p.read_text()
t = t.replace("    synthetic: false\n",
              "    synthetic: false\n"
              "    syntheticDisclosure: null\n"
              "    rationale: null\n"
              "    owner: null\n"
              "    scope: null\n"
              "    laterLifecycleVisibility: null\n", 1)
p.write_text(t)
PY

$CLI evidence --id 001-example --json | jq -e '.outcome != "Blocked"'   # parses fine
grep -c 'rationale: null' work/001-example/evidence.yml                  # 0 — normalized away
```

Expected: the verbose file parses without diagnostic (backward compatibility), and the run rewrites
it in the slim form. That single diff is the whole migration; run it again and nothing changes.

## Scenario 5 — A populated optional field is preserved (FR-003)

```bash
python3 - <<'PY'
import pathlib
p = pathlib.Path("work/001-example/evidence.yml")
t = p.read_text()
t = t.replace("    synthetic: false\n",
              "    synthetic: false\n"
              '    rationale: "accepted deferral, see DEC-004"\n'
              '    owner: "platform"\n', 1)
p.write_text(t)
PY

$CLI evidence --id 001-example >/dev/null
grep -E 'rationale:|owner:' work/001-example/evidence.yml
```

Expected: both lines survive verbatim. Omission applies to `None`, never to a value.

## Scenario 6 — A quoted `"null"` is a real string, not an absence (FR-006)

```bash
python3 - <<'PY'
import pathlib
p = pathlib.Path("work/001-example/evidence.yml")
t = p.read_text()
t = t.replace("    synthetic: false\n",
              '    synthetic: false\n    rationale: "null"\n', 1)
p.write_text(t)
PY

$CLI evidence --id 001-example >/dev/null
grep -F 'rationale: "null"' work/001-example/evidence.yml && echo QUOTED-NULL-PRESERVED
```

Expected: `QUOTED-NULL-PRESERVED`. A quoted `"null"` keeps its quotes and is never omitted — this is
the exact boundary feature 161 established (`isPlainNullScalar` checks `ScalarStyle.Plain`).

## Scenario 7 — Omission does not silence the synthetic-disclosure diagnostic (FR-009)

Mark a declaration `synthetic: true` with no `syntheticDisclosure` key at all:

```bash
python3 - <<'PY'
import pathlib
p = pathlib.Path("work/001-example/evidence.yml")
t = p.read_text().replace("    synthetic: false\n", "    synthetic: true\n", 1)
p.write_text(t)
PY

$CLI evidence --id 001-example --text | grep -i disclosure
```

Expected: the existing "synthetic evidence requires a disclosure" diagnostic still fires. It derives
from the parsed model (`SyntheticDisclosure = None`), not from the presence of a `null` line.

## Automated equivalents

| Scenario | Test |
|---|---|
| 1, 2 | `EvidenceCommandTests` — scaffolded text carries none of the five keys, no blank lines |
| 3 | `EvidenceCommandTests` — `evidence re-run is byte-idempotent …` (adapted #161 test) |
| 4 | `EvidenceArtifactTests` — verbose and slim fixtures parse to equal declarations |
| 5 | `EvidenceCommandTests` — populated optionals survive a re-render |
| 6 | `EvidenceArtifactTests` + `EvidenceCommandTests` — quoted `"null"` retained |
| 7 | `EvidenceCommandTests` — synthetic-without-disclosure still diagnosed |

Run them all:

```bash
dotnet test tests/FS.GG.SDD.Artifacts.Tests --filter 'FullyQualifiedName~Evidence'
dotnet test tests/FS.GG.SDD.Commands.Tests --filter 'FullyQualifiedName~Evidence'
```
