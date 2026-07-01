# Feature Specification: Diff-Driven Remediation Verbs (`doctor` / `upgrade`)

**Feature Branch**: `053-upgrade-doctor-remediation`

**Created**: 2026-07-01

**Status**: Draft

**Input**: User description: "start the next sdd owned item on the coordination board." → resolved to the single not-`Done` SDD-owned Coordination board item: FS.GG.SDD#50 (sub-issue of FS-GG/.github#85), *"`fsgg-sdd upgrade` / `doctor`: explicit diff-driven remediation verb (ADR-0009)"* — the **remediation half** of ADR-0009. Its **detection half** (FS.GG.SDD#49) already shipped as feature `052-cli-version-coherence`.

## Overview

A scaffolded product is coherent only when three inputs agree: the template pin, the
framework, and the **`fsgg-sdd` CLI** that orchestrates it. Feature 052 made the CLI
input *auditable and detectable* — it records the CLI version used and the
provider-declared required-minimum into `.fsgg/scaffold-provenance.json` and warns at
scaffold time when the installed CLI is behind. But 052 gives an author a *diagnosis*
with no *cure*: a product scaffolded by an old CLI silently lacks the seeded
`fs-gg-sdd-*` skills and `.fsgg/early-stage-guidance.md`, and there is no supported,
one-command way to bring it back to currency.

ADR-0009 (*The `fsgg-sdd` CLI is the single orchestrator — detect-and-remediate, not
silent auto-update*) settles the "warn **or** fail, and then what?" fork left open by
ADR-0008. Its decision: the CLI is the **single orchestration and enforcement surface**
for coherence, but it is **not** the source of truth and it **never silently
self-updates or silently rewrites consumer artifacts**. Remediation is an **explicit,
diff-driven verb**, never a side effect.

This feature delivers that remediation half as two new cross-cutting commands:

- **`fsgg-sdd doctor`** — a read-only report of drift: installed CLI vs the pin's
  required minimum, seeded artifacts present vs expected, and a preview of what
  `upgrade` *would* change. It never writes.
- **`fsgg-sdd upgrade`** — the reconciliation verb: CLI self-update, template re-pin,
  and artifact re-seed, **each shown as a diff and confirmed** before it is applied.

Both are cross-cutting commands, not lifecycle stages (like `scaffold`/`refresh`/
`agents`, their `nextLifecycleCommand` is `None`), and both project the same
`CommandReport` three ways (json / text / rich).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Author sees exactly what is out of date, without any writes (Priority: P1)

As a product author (or an auditor inspecting a scaffolded product), I can run
`fsgg-sdd doctor` and get a complete, read-only picture of how my product has drifted
from its coherent set — is my installed CLI behind the pin's required minimum, which
seeded artifacts are missing, and what would a reconciliation change — without any file
being touched.

**Why this priority**: This is the foundational, always-safe capability and the MVP.
Detection (052) tells an author *at scaffold time* that something is wrong; `doctor`
lets them ask *at any time* "what exactly is wrong now, and what would fixing it do?"
It delivers standalone value even before `upgrade` exists: the author can act on the
report manually. Because it never writes, it is safe to run anywhere, anytime, in CI or
locally.

**Independent Test**: In a product scaffolded by a CLI older than the provider-declared
minimum (and therefore missing some seeded skills), run `fsgg-sdd doctor` and confirm it
names the installed CLI version, the required minimum and how far behind it is, the
missing seeded artifacts, and a preview of the changes `upgrade` would make — while
writing nothing (working tree unchanged) and exiting 0.

**Acceptance Scenarios**:

1. **Given** a scaffold whose recorded CLI version is below the provider-declared
   minimum, **When** the author runs `fsgg-sdd doctor`, **Then** the report states the
   installed version, the required minimum, and the behind-by delta, and lists any
   missing seeded `fs-gg-sdd-*` skills / `.fsgg/early-stage-guidance.md`.
2. **Given** any drift, **When** `doctor` runs, **Then** it previews what `upgrade` would
   change (self-update, re-pin, re-seed) as a dry run and makes **zero writes** (the
   working tree is byte-identical before and after), exiting 0.
3. **Given** a fully coherent scaffold (CLI at/above minimum, all seeded artifacts
   present), **When** `doctor` runs, **Then** it reports "coherent — nothing to
   reconcile" and exits 0.
4. **Given** the same drifted scaffold, **When** `doctor` is projected as json, text, and
   rich, **Then** all three carry the identical set of facts (rich adds/drops no fact and
   changes no JSON byte).

---

### User Story 2 - Author reconciles a behind scaffold with one confirmable command (Priority: P1)

As a product author whose product is behind its coherent set, I can run
`fsgg-sdd upgrade` to reconcile it — self-update the CLI, re-pin the template, and
re-seed the missing skeleton artifacts — where **each** step is shown to me as a diff
and applied only after I confirm it, so I am never surprised by a silent change to my own
source.

**Why this priority**: This is the cure the board item promises and the observable
outcome of ADR-0009. Without it, detection is a dead end (052's US3 could only *point*
at a remedy). It is P1 alongside `doctor` because a diagnosis with no supported cure
leaves the author stuck.

**Independent Test**: In the same behind scaffold as US1, run `fsgg-sdd upgrade`, confirm
each presented diff, and afterwards run `fsgg-sdd doctor` and confirm it reports fully
coherent — and confirm every change that landed was shown as a diff and confirmed first.

**Acceptance Scenarios**:

1. **Given** a behind scaffold, **When** the author runs `fsgg-sdd upgrade` interactively
   and confirms each step, **Then** the CLI self-update, template re-pin, and re-seed of
   missing artifacts are applied, each **after** its own diff + confirmation.
2. **Given** a completed `upgrade`, **When** the author re-runs `fsgg-sdd doctor`, **Then**
   it reports the scaffold coherent with no residual drift.
3. **Given** an already-coherent scaffold, **When** the author runs `fsgg-sdd upgrade`,
   **Then** it is a no-op (nothing to apply), reports "already coherent", and exits 0.
4. **Given** an `upgrade` in progress, **When** the author **declines** a presented step,
   **Then** that step is skipped, no write for it occurs, and the report distinguishes
   applied steps from skipped ones and surfaces the residual drift.
5. **Given** an `upgrade` where a confirmed step fails to apply (e.g. the self-update
   process errors), **When** the run ends, **Then** it reports the failure and the
   residual drift and does **not** report the reconciliation as complete.

---

### User Story 3 - Automation opts into non-interactive reconciliation explicitly (Priority: P2)

As an author scripting a deliberate, unattended reconciliation, I can pass an explicit
apply flag (e.g. `--yes`) to `fsgg-sdd upgrade` so it applies the reconciliation without
prompting — and I am assured this non-interactive apply is only ever triggered by that
explicit flag, never implicitly by any other command or merely because the run is
non-interactive.

**Why this priority**: A remediation verb is only useful in automation if there is an
explicit non-interactive path — but ADR-0009's whole point is that reconciliation must
never happen *implicitly*. Making the non-interactive apply an explicit, opt-in flag (and
nothing else) is what keeps "detect-and-remediate, not silent auto-update" true. P2
because it layers onto the interactive verb (US2).

**Independent Test**: Run `fsgg-sdd upgrade --yes` non-interactively against a behind
scaffold and confirm it reconciles without prompting; then run `fsgg-sdd upgrade`
non-interactively **without** the flag and confirm it makes zero writes and does not hang
on a prompt, instead refusing with a pointer to the explicit flag.

**Acceptance Scenarios**:

1. **Given** a behind scaffold and a non-interactive context, **When** the author runs
   `fsgg-sdd upgrade --yes`, **Then** the reconciliation is applied without prompting and
   the report records that the explicit non-interactive apply path was used.
2. **Given** a non-interactive context and **no** explicit apply flag, **When**
   `fsgg-sdd upgrade` runs, **Then** it makes zero writes, does not block on a prompt, and
   refuses with a message pointing at the explicit flag (consistent with the
   interactive-warn / CI-fail-closed doctrine).
3. **Given** any other command (`scaffold`, `refresh`, `agents`, a lifecycle stage, …) in
   any context, **When** it runs, **Then** it never triggers a self-update or a re-seed as
   a side effect — only `upgrade` (interactively confirmed, or with `--yes`) mutates.

---

### User Story 4 - Governed state and CI pins are protected from remediation (Priority: P2)

As the owner of governed cross-repo state (the registry / provider descriptor) and of a
CI pipeline, I am assured that `upgrade` only rewrites what the *consumer* owns and never
reaches across the ownership boundary into governed pins, and that CI keeps pinning the
tool itself rather than relying on `upgrade`.

**Why this priority**: ADR-0009's ownership and reproducibility invariants are load-
bearing — a remediation verb that clobbered governed state or auto-ran in CI would break
the coherent-set model it is meant to protect. Framed as a testable guarantee so it is
verified, not assumed. P2 because it constrains US2/US3 rather than adding a new author
journey.

**Independent Test**: Run `fsgg-sdd upgrade --yes` and audit every write: confirm the only
mutations are to the consumer's own `.fsgg/providers.yml` and the re-seeded skeleton
paths, and that no governed registry/provider file is touched. Separately confirm CI
continues to pin the tool via `.config/dotnet-tools.json` and that no `upgrade` runs
implicitly in the pipeline.

**Acceptance Scenarios**:

1. **Given** an `upgrade` that re-pins the template, **When** it applies, **Then** it
   rewrites only the consumer-owned `.fsgg/providers.yml` (surfaced as a reviewable diff)
   and modifies no governed registry / provider-descriptor state.
2. **Given** a scaffold missing some seeded artifacts and with author edits to others,
   **When** `upgrade` re-seeds, **Then** it materializes only the **missing** artifacts
   (no-clobber) and never overwrites an author-edited artifact that is present.
3. **Given** a CI pipeline, **When** it runs, **Then** the tool stays pinned via
   `.config/dotnet-tools.json` and no command auto-upgrades; a behind CLI is caught by the
   052 fail-closed check, not by `upgrade`.

---

### Edge Cases

- **Not a scaffolded product** (a bare `init` skeleton or a plain repo with no
  `.fsgg/scaffold-provenance.json`): `doctor` reports "no scaffold provenance — nothing to
  reconcile" and `upgrade` no-ops; both write nothing and exit 0.
- **Provider declares no minimum**: there is no version to compare against; `doctor`
  reports coherent-by-absence for the CLI axis (still reporting artifact presence) and
  `upgrade` asserts no CLI version target — consistent with feature 052's no-minimum
  handling.
- **CLI already at/above minimum but seeded artifacts missing** (e.g. an early scaffold
  that predates skill seeding): `doctor` still flags the missing artifacts, and `upgrade`
  re-seeds them without attempting a self-update.
- **A reconciliation step is unavailable** (git absent, `dotnet` tool source offline, the
  self-update process errors): the affected step is reported as failed with its cause, the
  reconciliation is reported incomplete with residual drift, and it is never reported as
  complete.
- **Author declines one step but confirms others**: partial application is allowed and
  reported step-by-step; the residual drift from the declined step is surfaced, and a
  subsequent `doctor` still shows that drift.
- **Re-running `upgrade` on an already-coherent scaffold**: a clean no-op that reports
  "already coherent" and exits 0.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a `fsgg-sdd doctor` command as a cross-cutting
  command (not a lifecycle stage; its `nextLifecycleCommand` is `None`), reachable via the
  CLI and never from a lifecycle command path.
- **FR-002**: `doctor` MUST be strictly read-only — it MUST make zero writes to the CLI
  installation or any consumer artifact — and MUST exit 0 whenever it produces a report
  (including when drift is present).
- **FR-003**: `doctor` MUST report the installed CLI version against the pin's required
  minimum (behind / at / ahead, with the behind-by delta), reading the required minimum
  from **declarative truth** — the required-minimum recorded by feature 052 in
  `.fsgg/scaffold-provenance.json` and/or the provider-declared `minimumFsggSdd` — and
  MUST NOT treat a value embedded in the binary as that truth.
- **FR-004**: `doctor` MUST report which seeded skeleton artifacts (the consumer-relevant
  `fs-gg-sdd-*` process skills and `.fsgg/early-stage-guidance.md`) are present vs expected,
  naming the missing ones.
- **FR-005**: `doctor` MUST preview what `upgrade` would change (self-update, re-pin,
  re-seed) as a dry run, without applying any of it.
- **FR-006**: The system MUST provide a `fsgg-sdd upgrade` command as a cross-cutting
  command (`nextLifecycleCommand` = `None`); it is the **only** command permitted to mutate
  the CLI installation or consumer artifacts for remediation.
- **FR-007**: `upgrade` MUST reconcile a behind scaffold across up to three steps — CLI
  self-update, template re-pin, and re-seed of the missing seeded artifacts — and MUST show
  **each** step as a diff and apply it only after confirmation (or after the explicit
  non-interactive apply flag, per FR-011).
- **FR-008**: No command other than `upgrade` MAY mutate the CLI installation or the
  consumer's artifacts as a side effect; detection surfaces (`doctor` and any per-command
  staleness check) MUST never write.
- **FR-009**: `upgrade`'s template re-pin MUST rewrite only consumer-owned state (the
  scaffold's own `.fsgg/providers.yml`) and MUST NOT modify governed registry / provider-
  descriptor state (a governed pin bump remains a PR in its owning repo).
- **FR-010**: `upgrade`'s re-seed MUST be no-clobber — it materializes only the **missing**
  seeded artifacts using the same authored-skeleton seeding semantics as `init`, and MUST
  never overwrite a present (possibly author-edited) artifact.
- **FR-011**: `upgrade` MUST provide an explicit non-interactive apply flag (e.g. `--yes`)
  that applies the reconciliation without prompting; this path MUST be triggered only by that
  explicit flag and MUST NOT be triggered implicitly by any other command or by
  non-interactivity alone.
- **FR-012**: In a non-interactive context **without** the explicit apply flag, `upgrade`
  MUST NOT block on a prompt; it MUST make zero writes and refuse with a pointer to the
  explicit flag (consistent with the interactive-warn / CI-fail-closed doctrine). CI keeps
  pinning the tool via `.config/dotnet-tools.json` and relies on the 052 fail-closed check,
  not on `upgrade`.
- **FR-013**: `upgrade` MUST never report an incomplete reconciliation as complete — if a
  confirmed step fails, or a step is declined/skipped and drift remains, the report MUST
  surface the residual drift; a clean no-op (already coherent) exits 0, and a defect
  (a step that failed to apply) exits non-zero.
- **FR-014**: `doctor` and `upgrade` MUST both project the same `CommandReport` three ways
  (json / text / rich) with the flag precedence and degradation rules used by every
  `fsgg-sdd` command; rich MUST add and drop no facts and change no JSON byte.
- **FR-015**: `doctor` and `upgrade` MUST degrade gracefully when there is no scaffold
  provenance / nothing to reconcile (a bare skeleton or plain repo) — a clear "nothing to
  reconcile" report, zero writes, exit 0.
- **FR-016**: When the selected provider declares **no** minimum, neither command MAY assert
  CLI staleness — `doctor` reports coherent-by-absence for the CLI axis and `upgrade` asserts
  no CLI version target — consistent with feature 052.
- **FR-017**: After a successful `upgrade`, a subsequent `doctor` MUST report the scaffold
  coherent with no residual drift.

### Key Entities *(include if feature involves data)*

- **Drift report**: the read-only picture `doctor` emits — installed CLI vs required
  minimum (with delta), seeded-artifact presence vs expected, and the previewed
  reconciliation steps.
- **Reconciliation step**: one confirmable unit of `upgrade` (CLI self-update, template
  re-pin, or artifact re-seed), each carrying a before/after diff and an applied / skipped /
  failed outcome.
- **Required minimum**: the declarative datum the commands *read* (the provenance-recorded
  required-minimum and/or the provider-declared `minimumFsggSdd`) — never owned or embedded
  by the CLI.
- **Consumer-owned pin (`.fsgg/providers.yml`)**: the sole writable target of the re-pin
  step; governed registry / provider-descriptor state is out of bounds.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: On a behind scaffold, `fsgg-sdd doctor` reports the drift (behind-CLI and/or
  missing seeded artifacts) with **zero** writes (working tree byte-identical before and
  after) and exit 0.
- **SC-002**: `fsgg-sdd upgrade` reconciles a behind scaffold such that a subsequent
  `fsgg-sdd doctor` reports fully coherent, and **every** applied change was shown as a diff
  and confirmed (or applied under the explicit `--yes` flag) — never silently.
- **SC-003**: Across all `fsgg-sdd` commands, **100%** of consumer/CLI mutations made for
  remediation originate from `upgrade`; **zero** originate as a side effect of any other
  command (verifiable by an artifact-write audit).
- **SC-004**: In a non-interactive run of `upgrade` without the explicit apply flag, the
  command makes **zero** writes and does not block on a prompt (no hang).
- **SC-005**: `upgrade` writes **only** the consumer's own `.fsgg/providers.yml` and the
  re-seeded skeleton paths; it makes **zero** writes to governed registry / provider-
  descriptor state.
- **SC-006**: An `upgrade` in which a confirmed step fails reports the reconciliation
  incomplete and exits non-zero; it **never** reports a partial reconciliation as complete.
- **SC-007**: For both commands, the json, text, and rich projections carry the identical
  set of facts (rich adds/drops no fact and changes no JSON byte), and rich degrades to
  zero-ANSI plain text when output is non-interactive/redirected or color is disabled.

## Assumptions

- **Re-seed reuses `init`'s seeding, not `refresh`.** The re-seed step re-materializes the
  missing `fs-gg-sdd-*` skills and `.fsgg/early-stage-guidance.md` via the same no-clobber
  authored-skeleton (`AgentGuidanceTarget`) seeding effects that `init`/`scaffold` use —
  consistent with feature 052's US3, which explicitly notes the re-seed path is `init`'s
  seeding effects and **not** `refresh` (which does not re-seed). ADR-0009's phrase "artifact
  re-seed (`refresh-agents`)" is read as shorthand for that re-seed step; to be confirmed in
  `/speckit-clarify`.
- **CLI self-update runs at the process edge.** `upgrade`'s self-update step is orchestrated
  by invoking `dotnet tool update` at the same `RunProcess` boundary `scaffold` uses for the
  provider, as one confirmable step; the updated binary takes effect on the next invocation.
- **Confirmation is per-step.** Each of self-update / re-pin / re-seed is confirmed
  independently, matching ADR-0009's "each shown as a diff and confirmed"; the explicit
  `--yes` flag confirms all steps at once.
- **The required minimum comes from 052's recorded facts and the live descriptor.** The
  commands read the `.fsgg/scaffold-provenance.json` fields recorded by feature 052 and/or
  the live provider-declared `minimumFsggSdd`; if the recorded and live values disagree, the
  reports evaluate against the live declarative minimum.
- **Exit-code taxonomy follows the existing CLI convention.** `doctor` exits 0 whenever it
  reports; `upgrade` exits 0 on success or a clean no-op, exit 1 for user-input refusals
  (e.g. non-interactive without `--yes`), and exit 2 for step defects — mirroring the
  `scaffold` user-input-vs-defect split.
- **Scope is the remediation half only.** This feature depends on the minimum-reading
  detection primitives delivered by feature 052 (FS.GG.SDD#49) and delivers the `doctor` /
  `upgrade` verbs (FS.GG.SDD#50). Any broadening of per-command staleness detection beyond
  what 052 shipped, and all Governance-owned effective-evidence freshness / gate enforcement,
  remain out of scope and downstream.
- **No registry surface change.** Per ADR-0009, this feature constrains *how* the ADR-0008
  minimum field is enforced, not *what* it is; it introduces no new versioned cross-repo
  contract surface.
