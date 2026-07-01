# Quickstart & Validation: Diff-Driven Remediation Verbs (`doctor` / `upgrade`)

Feature `053-upgrade-doctor-remediation` · Date: 2026-07-01

Runnable validation scenarios proving the feature end-to-end. Prerequisites: the solution builds
(`dotnet build`), the .NET SDK is present (for the self-update `RunProcess` edge), and the fixtures
below exist under `tests/fixtures/`.

## Fixtures

| Fixture | Shape |
|---------|-------|
| `behind-scaffold/` | `.fsgg/scaffold-provenance.json` with a producing CLI **below** the provider minimum; some `fs-gg-sdd-*` skills missing; `.fsgg/providers.yml` declaring `minimumFsggSdd.version`. |
| `coherent-scaffold/` | CLI at/above minimum; all seeded artifacts present. |
| `no-minimum-scaffold/` | provider declares no minimum; artifacts present. |
| `no-provenance/` | bare `init` skeleton — no `.fsgg/scaffold-provenance.json`. |
| `author-edited/` | behind scaffold where one present seeded artifact has author edits (no-clobber target). |

---

## A — `doctor` reports drift with zero writes (US1 / SC-001)

```bash
cp -r tests/fixtures/behind-scaffold "$TMP/p" && cd "$TMP/p"
sha=$(find . -type f -exec sha256sum {} + | sort)   # snapshot before
fsgg-sdd doctor            # default json
echo "exit=$?"
find . -type f -exec sha256sum {} + | sort | diff <(echo "$sha") -   # MUST be empty
```

**Expect**: json names installed version, required minimum, behind-by delta, missing seeded
artifacts, and a `previewSteps` block; **exit 0**; the two `sha` snapshots are identical (no writes).

## B — `doctor` on a coherent scaffold (US1-AC3)

```bash
cd "$TMP/coherent" && fsgg-sdd doctor --text
```

**Expect**: "coherent — nothing to reconcile", `isCoherent: true`, exit 0.

## C — `doctor` three-projection parity (US1-AC4 / SC-007)

```bash
fsgg-sdd doctor --json > d.json
fsgg-sdd doctor --text > d.txt
NO_COLOR=1 fsgg-sdd doctor --rich > d.rich   # degrades to zero-ANSI
```

**Expect**: `d.json` byte-identical to the json embedded/derivable from all three; `d.rich` contains
no ANSI escape; text and rich carry the identical fact set.

## D — `upgrade` interactive confirm-each (US2-AC1 / US2-AC2 / SC-002)

```bash
cd "$TMP/behind"
printf 'y\ny\ny\n' | fsgg-sdd upgrade      # confirm self-update, (re-pin), re-seed
fsgg-sdd doctor                             # subsequent doctor
```

**Expect**: each step shown as a diff and applied only after its `y`; `mode: "interactive"`;
subsequent `doctor` reports coherent (per R4 for the same-run self-update case). Every landed change
was confirmed first (nothing silent).

## E — `upgrade` decline one step (US2-AC4)

```bash
printf 'n\ny\n' | fsgg-sdd upgrade         # decline first step, confirm the rest
```

**Expect**: the declined step is `skipped` with no write; `appliedStepIds` vs `skippedStepIds`
distinguished; `residualDrift: true`; exit 0; a subsequent `doctor` still shows the declined-step
drift.

## F — `upgrade --yes` non-interactive apply (US3-AC1)

```bash
cd "$TMP/behind"
fsgg-sdd upgrade --yes < /dev/null
```

**Expect**: reconciles without prompting; `mode: "assumeYes"`; exit 0.

## G — non-interactive without `--yes` refuses (US3-AC2 / SC-004)

```bash
cd "$TMP/behind"
sha=$(find . -type f -exec sha256sum {} + | sort)
fsgg-sdd upgrade < /dev/null                # non-interactive, no --yes
echo "exit=$?"                              # MUST be 1, MUST NOT hang
find . -type f -exec sha256sum {} + | sort | diff <(echo "$sha") -   # empty (zero writes)
```

**Expect**: refuses with a pointer to `--yes` (`upgrade.nonInteractiveNoYes`); **exit 1**; zero
writes; no prompt-hang.

## H — consumer-only + no-clobber (US4 / SC-005)

```bash
cd "$TMP/author-edited"
before=$(sha256sum .claude/skills/fs-gg-sdd-lifecycle/SKILL.md)   # an author-edited, present artifact
fsgg-sdd upgrade --yes < /dev/null
sha256sum .claude/skills/fs-gg-sdd-lifecycle/SKILL.md | diff <(echo "$before") -   # unchanged
```

**Expect**: only **missing** artifacts materialized (no-clobber); the author-edited present artifact
is byte-unchanged; the only writes are `.fsgg/providers.yml` (if re-pin had a target) and the
re-seeded missing paths — no governed registry/provider-descriptor file touched.

## Step-defect & no-provenance

- **Step failure (US2-AC5 / SC-006)**: force a self-update failure (e.g. offline tool source) →
  reconciliation reported incomplete, `failedStepIds` non-empty, `residualDrift: true`, **exit 2**,
  never "complete".
- **No provenance (FR-015)**: in `no-provenance/`, both `doctor` and `upgrade` report "nothing to
  reconcile", write nothing, exit 0.

---

References: [contracts/doctor-command.md](./contracts/doctor-command.md),
[contracts/upgrade-command.md](./contracts/upgrade-command.md),
[contracts/confirm-effect.md](./contracts/confirm-effect.md),
[contracts/drift-model.md](./contracts/drift-model.md), [data-model.md](./data-model.md).
