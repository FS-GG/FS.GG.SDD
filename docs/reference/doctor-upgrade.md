# `fsgg-sdd doctor` and `fsgg-sdd upgrade`

The **remediation half** of ADR-0009 (*the `fsgg-sdd` CLI is the single orchestrator —
detect-and-remediate, not silent auto-update*). A scaffolded product is coherent only when
three inputs agree: the template pin, the framework, and the `fsgg-sdd` CLI that orchestrates
it. Feature 052 made the CLI input auditable at scaffold time; these two cross-cutting
commands (`nextLifecycleCommand = None`, like `scaffold`/`refresh`/`agents`) let an author
inspect and reconcile drift at any time. Both project the same `CommandReport` three ways
(`--json` default / `--text` / `--rich`).

## `fsgg-sdd doctor` — read-only drift report

```bash
fsgg-sdd doctor --root .
```

`doctor` reports how a scaffolded product has drifted from its coherent set and **never
writes** — the working tree is byte-identical before and after, and it exits `0` whenever it
produces a report (including when drift is present).

It reports, from declarative truth (the feature-052 recorded minimum and the live
provider-declared `minimumFsggSdd`, live wins):

- **CLI axis** — installed CLI vs the required minimum: `behind` (with a behind-by delta) /
  `atOrAbove` / `coherentByAbsence` (no declared minimum) / `undeterminable` (installed
  version unparseable).
- **Artifact axis** — which seeded `fs-gg-sdd-*` process skills and
  `.fsgg/early-stage-guidance.md` are present vs expected, naming the missing ones.
- **Preview** — a dry-run of what `upgrade` would change across the three steps, applying
  none of it.

With no `.fsgg/scaffold-provenance.json` (a bare `init` skeleton or plain repo), `doctor`
reports "no scaffold provenance — nothing to reconcile" and exits 0.

## `fsgg-sdd upgrade` — the reconciliation verb

```bash
fsgg-sdd upgrade --root .          # interactive: confirm each step
fsgg-sdd upgrade --root . --yes    # explicit non-interactive apply
```

`upgrade` reconciles a behind scaffold across up to three steps, and each **actionable** step
is shown as a diff and applied only after its own confirmation (or all at once under `--yes`):

| Step | What it does | Write target |
|---|---|---|
| `cliSelfUpdate` | `dotnet tool update` at the process edge | the CLI installation (takes effect on the *next* invocation) |
| `templateRePin` | value-agnostic; currently a recognized-but-usually-inert step | consumer-owned `.fsgg/providers.yml` only |
| `artifactReSeed` | re-materializes the **missing** seeded artifacts via `init`'s no-clobber seeding | the missing seeded skeleton paths only |

`upgrade` is the **only** command permitted to mutate the CLI installation or consumer
artifacts for remediation — no other command (`scaffold`, `refresh`, `agents`, a lifecycle
stage) ever self-updates or re-seeds as a side effect.

### Ownership & safety invariants

- **Consumer-only writes.** Re-pin rewrites only the scaffold's own `.fsgg/providers.yml`;
  governed registry / provider-descriptor state is never touched.
- **No-clobber re-seed.** Only *missing* artifacts are materialized; a present,
  possibly author-edited artifact is never overwritten.
- **Explicit apply only.** The non-interactive apply is triggered **only** by `--yes`. A
  non-interactive run without `--yes` makes zero writes, does not hang on a prompt, and
  refuses with a pointer to `--yes` (`upgrade.nonInteractiveNoYes`, exit 1). CI keeps the
  tool pinned via `.config/dotnet-tools.json` and relies on the feature-052 fail-closed
  check, not on `upgrade`.
- **Never reports incomplete as complete.** A declined step is `skipped` (residual drift
  surfaced, exit 0); a confirmed step that fails to apply is `failed` (residual drift, exit
  2). After a fully successful `upgrade`, a subsequent `doctor` reports coherent (a CLI
  self-update applied in the same run reconciles on the next invocation).

## Exit codes

| Situation | `doctor` | `upgrade` |
|---|---|---|
| Coherent / nothing to reconcile / no provenance | 0 | 0 |
| Drift reported / steps applied / step declined (residual) | 0 | 0 |
| Non-interactive without `--yes` | n/a | 1 |
| A confirmed step failed to apply | n/a | 2 |
