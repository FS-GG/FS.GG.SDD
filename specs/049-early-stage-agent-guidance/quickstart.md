# Quickstart: Early-Stage Agent Guidance Bootstrap

Runnable validation scenarios proving US1–US3. Run from a clean scratch directory.
`fsgg-sdd` is the CLI under test (`dotnet run --project src/FS.GG.SDD.Cli -- …` or the
built tool). Contracts and id/heading details live in `./contracts/` and
`./data-model.md` — not duplicated here.

## Prerequisites

- Repo builds: `dotnet build FS.GG.SDD.sln`.
- A clean, empty working directory for the seeded skeleton.

## Scenario A — US1: authoring help exists from an empty/early work item (P1)

```bash
mkdir demo && cd demo
fsgg-sdd init
test -f .fsgg/early-stage-guidance.md          # exists from stage zero (SC-001)
```

**Expected**: `.fsgg/early-stage-guidance.md` is present and, for each of `charter`,
`specify`, `clarify`, `checklist`, names the `fsgg-sdd` command, the required section
headings, and the stable-id formats; and it states the §1.1 coverage-line rule and the
§1.2 evidence rule (SC-005). No reference in it dangles (SC-003). Following its
`charter` section produces a `charter` artifact that passes `fsgg-sdd specify` — with
no decompilation.

## Scenario B — US2: `agents` / `refresh` are no longer an early-stage dead end (P2)

```bash
# work item with only early artifacts, no readiness/<id>/work-model.json
fsgg-sdd charter ...            # author a charter only
fsgg-sdd agents   ; echo "exit=$?"
fsgg-sdd refresh  ; echo "exit=$?"
```

**Expected**:
- Both exit `0` (not a blocking dead end — SC-002).
- `agents` emits an advisory `agents.earlyStageGuidance` (not a bare
  `agents.missingWorkModel` error), a `NextAction` pointing to
  `.fsgg/early-stage-guidance.md`, and best-effort facts naming which early artifacts
  exist and the next lifecycle command.
- `refresh` reports the situation as a recognized, navigable early-stage state
  (advisory `refresh.earlyStageGuidance`), not only `refresh.blockedUpstreamView`.
- Any best-effort guidance is explicitly labeled **early-stage / partial** (FR-006)
  and **no** `readiness/<id>/agent-commands/**` file is written (FR-008/FR-011):
  ```bash
  test ! -e readiness/*/agent-commands     # nothing digest-stamped at early stage
  ```
- Negative control: a *malformed* `work-model.json` still blocks (`exit 1`,
  `agents.malformedWorkModel`).

## Scenario C — US3: deterministic and self-consistent (P3)

```bash
# determinism of the seed
fsgg-sdd init && cp .fsgg/early-stage-guidance.md /tmp/g1
rm .fsgg/early-stage-guidance.md && fsgg-sdd init
diff .fsgg/early-stage-guidance.md /tmp/g1     # byte-identical (SC-004)

# no-clobber of author edits
printf '\nMY NOTE\n' >> .fsgg/early-stage-guidance.md
fsgg-sdd init                                   # refuses to overwrite; surfaces unsafeOverwrite
grep -q 'MY NOTE' .fsgg/early-stage-guidance.md # author bytes preserved (US3 AC3)

# determinism of the early-stage report
fsgg-sdd agents --json > /tmp/r1 && fsgg-sdd agents --json > /tmp/r2
diff /tmp/r1 /tmp/r2                             # byte-identical report (SC-004)
```

**Expected**: all `diff`s are empty; the author note survives; every reference in the
guidance resolves (the `EarlyStageGuidanceContractTests` drift-guard passes — SC-003).

## Scenario D — SC-006 regression guard: buildable work model unchanged

```bash
# advance the item until verify/ship builds readiness/<id>/work-model.json, then:
fsgg-sdd agents --json > /tmp/full
# (compare against the pre-feature golden for the same work model)
```

**Expected**: with a buildable work model the early-stage branch is **not** taken; the
generated `agent-commands` views are byte-identical to their pre-feature output — the
full-guidance contract is untouched.
