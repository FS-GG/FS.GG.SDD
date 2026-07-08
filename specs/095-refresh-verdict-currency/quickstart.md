# Quickstart: Validating Feature 095

**Feature**: 095 · **Issue**: FS.GG.SDD#188 · **Date**: 2026-07-08

How to see the three defects, and how to confirm they are fixed. Scenarios map 1:1 onto the cells of
[contracts/refresh-currency-matrix.md](./contracts/refresh-currency-matrix.md).

## Prerequisites

```sh
cd /home/developer/projects/FS.GG.SDD-188      # the item/188 worktree
dotnet build
```

> **Known local snag**: `dotnet build` can fail `NU1403` on `FSharp.Core` (lock-file hash mismatch).
> Force-evaluate the restore, then revert the lock churn before committing:
> `dotnet restore --force-evaluate && git checkout -- '**/packages.lock.json'`.

## Automated validation (the real gate)

```sh
# The whole refresh contract, incl. the 10-cell exit-code table (SC-004)
dotnet test tests/FS.GG.SDD.Commands.Tests --filter "FullyQualifiedName~RefreshCommandTests"

# Full sweep — proves FR-008 (valid ship views byte-identical) and FR-002
# (analysis/verify currency untouched) across the suite
dotnet test
```

**Before the source change**, two tests must be **red**:
- ``a ship json that is valid json but not a ship view blocks the verdict with a diagnostic`` —
  corrected to assert the true facts (cell 5).
- the new stale-source/absent-verdict test (cell 8).

If either is green before the fix, the test is not exercising the defect — stop and fix the test.

**After**, all green, and these four must never have changed (research R5): the malformed-JSON test,
the fresh-clone test, the edited-source test, the both-missing test.

## Manual validation

Set up a shipped work item:

```sh
FSGG=./src/FS.GG.SDD.Cli/bin/Debug/net10.0/fsgg-sdd     # or `dotnet run --project src/FS.GG.SDD.Cli --`
W=095-demo
$FSGG init && $FSGG charter --work "$W" --title "Demo" && ... && $FSGG ship --work "$W"

SHIP=readiness/$W/ship.json
VERDICT=readiness/$W/ship-verdict.json
```

Read the two facts under test with `jq`. **Two things to get right**, both verified against the real
binary rather than assumed:

- A **`Blocked` report routes to stderr**, not stdout (`Cli/Program.fs:91`). Every scenario below is a
  blocked run, so redirect `2>&1` or you will `jq` an empty string.
- `generatedViews[]` entries are keyed by **`kind`**; there is no `viewId` field
  (`CommandSerialization.fs:553`). The `viewId` naming *does* exist, but on `refresh.perViewState`.

```sh
# `2>&1` is load-bearing: a blocked refresh writes its report to stderr.
report() { $FSGG refresh --root . --work "$W" 2>&1; }

currency() {
  report | jq -r '.generatedViews[] | select(.kind=="ship" or .kind=="ship-verdict"
                                             or .kind=="governance-handoff")
                  | "\(.kind): \(.currency)"'
}
diags() { report | jq -r '.diagnostics[] | "\(.severity) \(.id) \(.artifact.path)"'; }
```

### Scenario A — the headline defect (matrix cell 5)

```sh
cp "$VERDICT" /tmp/verdict.before
echo '{ "schemaVersion": 99 }' > "$SHIP"     # valid JSON, not a valid ship view
currency
```

| | Output |
|---|---|
| **Before** | `ship: current` · `ship-verdict: malformed` · `governance-handoff: blocked` |
| **After** | `ship: malformed` · `ship-verdict: blocked` · `governance-handoff: blocked` |

**`malformed` must name exactly one artifact** (FR-017) — the one whose bytes do not parse:

```sh
report | jq -r '[.generatedViews[] | select(.currency=="malformed") | .kind]'
# => ["ship"]   (a naive fix yields ["ship","governance-handoff"] — the handoff inherits its
#                source's class, so `Malformed` must be mapped to `Blocked` on the way through)
```

Confirm the committed artifact was never touched and is not what was called malformed:

```sh
diff /tmp/verdict.before "$VERDICT" && echo "verdict untouched (FR-006)"
jq . "$VERDICT" >/dev/null && echo "verdict is well-formed JSON — it was never malformed"
diags     # after: the sole `malformedGeneratedView` names ship.json, not ship-verdict.json
```

And the bucket projection of the same fact (FR-003a):

```sh
report | jq '.refresh | {alreadyCurrentViewIds, blockedViewIds}'
# before: "ship" in alreadyCurrentViewIds   after: "ship" in blockedViewIds
```

**Exit code is unchanged (non-zero) before and after** — `echo $?` after each. That is the point: the
run always failed; it just blamed the wrong file.

### Scenario B — the severity asymmetry (matrix cells 7 vs 8)

```sh
$FSGG ship --work "$W"                                      # restore a good ship.json + verdict
printf '\n## Appended\n' >> work/$W/spec.md               # make the source stale

rm "$VERDICT" && diags | grep ship-verdict                # cell 8: verdict ABSENT
$FSGG ship --work "$W" && printf '\n## Again\n' >> work/$W/spec.md
diags | grep ship-verdict                                 # cell 7: verdict PRESENT
```

| | Cell 8 (absent) | Cell 7 (present) |
|---|---|---|
| **Before** | `error refresh.blockedUpstreamView` | `warning refresh.staleView` |
| **After** | `warning refresh.staleView` | `warning refresh.staleView` |

Same underlying state, same remediation (`re-run ship`), now the same severity. Currency stays
`missing` in cell 8 — the verdict genuinely is absent (FR-010).

### Scenario C — the guardrails hold

```sh
# Cell 3: not JSON at all -> ship malformed, as before. The stronger oracle subsumes the weaker.
echo '{ not json' > "$SHIP" && currency          # ship: malformed · ship-verdict: blocked

# Cells 9-10: a valid ship view is byte-identical to pre-change behavior (FR-008). These runs are
# NOT blocked, so the report lands on stdout -- no `2>&1` here.
$FSGG ship --work "$W" && $FSGG refresh --root . --work "$W" > /tmp/a.json
$FSGG refresh --root . --work "$W" > /tmp/b.json
diff /tmp/a.json /tmp/b.json && echo "deterministic across runs (SC-007)"

# FR-002: analysis/verify keep the weaker gate on purpose (deliberately out of scope)
echo '{ "schemaVersion": 99 }' > readiness/$W/analysis.json
report | jq -r '.generatedViews[] | select(.kind=="analysis") | .currency'
# -> "current"  (unchanged; a known weakness, spec §Out of Scope)

# FR-016: a DEPRECATED but supported schemaVersion still parses -> still current. Adopting
# `parseShipView` adopts the artifact layer's compatibility policy; it does not invent a stricter one.
$FSGG ship --work "$W" && sed -i 's/"schemaVersion": 1/"schemaVersion": 0/' "$SHIP"
report | jq -r '.generatedViews[] | select(.kind=="ship") | .currency'    # -> "current"
```

### Scenario D — the dead branch (FR-013)

Not runnable: the arm is unreachable by construction. Verified by inspection —
`HandlersRefresh.fs`'s `(AlreadyCurrent, _)` / `| None ->` case must carry a comment naming the
invariant (`shClass = AlreadyCurrent` ⇒ the snapshot exists) and the line establishing it.

## Success checklist

- [ ] Two tests red before the source change, green after (Scenario A cell 5, Scenario B cell 8).
- [ ] The four pinned regression tests never changed (research R5).
- [ ] `jq . ship-verdict.json` succeeds in every state where `refresh` previously said `malformed`
      about it (SC-002).
- [ ] No cell reports `ship: current` while `ship.json` fails `parseShipView` (SC-003).
- [ ] `echo $?` matches, cell for cell, before and after (SC-004) — the table test enforces this.
- [ ] Cells 7 and 8 emit equal severities (SC-005).
- [ ] `git status` shows no golden under `tests/…/goldens/readiness` moved (research R6).
- [ ] `git diff --stat` touches exactly `HandlersRefresh.fs` and `RefreshCommandTests.fs`.
