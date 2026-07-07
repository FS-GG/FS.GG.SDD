# Feature Specification: Surface Drift Classification (additive vs breaking)

**Feature Branch**: `087-surface-drift-classification`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Extend the existing `fsgg-sdd surface` command (feature 086, which detects a drifted committed `.fsi` baseline under `docs/api-surface/<pkg>/**`) with additive-vs-breaking CLASSIFICATION of the delta. This is the detection/classification slice of the first-class shipped-surface-mutation governed event (FS-GG/.github ADR-0025, issue FS-GG/FS.GG.SDD#170, epic #236). When `surface --check` observes that a COMMITTED baseline differs from the freshly authored `.fsi`, classify the per-file delta and emit a machine-readable verdict: additive (members only added; every prior signature still present) ⇒ minor coherent-set bump; breaking (a member removed, renamed, or its signature changed) ⇒ major coherent-set bump. Classification is advisory-but-loud; the operator confirms — it does NOT change the drift exit code (a drifted baseline still exits 1 under `--check` as today), it adds a classification fact to the report. A surface with NO committed baseline is a NEW surface (fresh registration), not a mutation — out of scope for this classification event. The classification diffs the `.fsi` TEXT (not assembly reflection), consistent with feature 086's byte-for-byte source-of-truth. Emit the verdict per drifted file in the standard three projections (json/text/rich). No persisted-artifact schema change beyond an additive report fact. Generic-SDD purity: no provider/package literal; the `<Pkg>` is derived structurally."

## Overview

Feature 086 gave `fsgg-sdd surface` the ability to *detect* that a committed API-surface baseline (`docs/api-surface/<Pkg>/<Name>.fsi`) has drifted from its authored source signature (`src/<Pkg>/<Name>.fsi`): it reports the file as `drifted` and, under `--check`, exits 1 so CI fails. What it does **not** do is say *how bad* the drift is. A contract-first framework treats a public surface as a governed artifact, and the one event that governance exists for — a mutation of an **already-shipped** surface — needs to be classified so the coherent-set version bump can be chosen correctly: purely additive changes are a **minor** bump, while any removal or signature change is a **major** (breaking) bump (FS-GG/.github ADR-0025).

This feature adds that classification. When `surface` sees a **drifted** baseline (the baseline exists but differs byte-for-byte from the source), it compares the two signature texts at the level of declared members and labels the delta **additive**, **breaking**, or **cosmetic**, and reports a run-level verdict with the recommended coherent-set bump. Classification is advisory-but-loud: it never changes the exit code (a drifted baseline still exits 1 under `--check`, exactly as in feature 086) and it never confirms the bump itself — the operator does. A source with **no** committed baseline is a *new* surface (a fresh registration), not a mutation, and is explicitly outside this event.

## Clarifications

### Session 2026-07-07

- Q: What is the source of truth for the classification — the compiled assembly (e.g. ApiCompat) or the `.fsi` text? → A: The `.fsi` **text**, consistent with feature 086. Classification compares the member declarations parsed from the two signature texts; no build and no assembly reflection. (ApiCompat already gates the *published-package* direction; this classifies the *committed-baseline* direction, at source, before publish.)
- Q: Which files get classified? → A: Only files whose baseline is `drifted` (the baseline exists and differs). A `missing-baseline` file is a *new* surface (fresh registration), explicitly out of scope; a `matched` file has no delta; an `orphan` baseline has no source to compare. Only the `drifted` set is a shipped-surface *mutation*.
- Q: There are three outcomes, but ADR-0025 names only additive and breaking — what is the third? → A: `cosmetic`. A drifted file whose declared-member set is **unchanged** (only comments, blank lines, or member ordering differ) is neither additive nor breaking; it recommends **no** coherent-set bump. Keeping it distinct avoids mislabeling a formatting-only edit as a real surface change. So the outcomes are `additive` / `breaking` / `cosmetic`.
- Q: Does classification change the exit code or block? → A: No. Classification is a reported fact only. A drifted baseline still exits 1 under `--check` (feature 086 behavior, unchanged); a coherent tree still exits 0; `--update` still reconciles and exits 0. Classification adds facts, never gates.
- Q: What is a "breaking" delta, precisely? → A: Any declared member that was present in the baseline is **absent** from the source — whether it was removed, renamed, or had its signature changed (a changed signature reads as the old declaration being gone and a new one present). If any prior member is gone, the delta is breaking regardless of what was added alongside it.
- Q: Is the verdict per file or per run? → A: Both. Each drifted file carries its own classification; the run carries an overall verdict equal to the **most severe** per-file classification (breaking ≻ additive ≻ cosmetic), with the mapped bump recommendation (major / minor / none).

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Classify a drifted committed baseline additive vs breaking (Priority: P1)

A workspace author (or CI) runs `fsgg-sdd surface --check` and one committed baseline has drifted from its source `.fsi`. Feature 086 already reports the file as `drifted` and exits 1. Now the command also tells the author *what kind* of change it is: if the source only **added** members (every member that was in the baseline is still there), the delta is **additive** and the recommended coherent-set bump is **minor**; if any member the baseline declared is **gone** (removed, renamed, or its signature changed), the delta is **breaking** and the recommended bump is **major**. The author uses this to choose the version bump before publishing — without hand-reading the diff.

**Why this priority**: This is the whole feature — turning "the surface drifted" into "the surface drifted, and here is whether it is additive or breaking." Without the per-file classification there is nothing new delivered on top of feature 086.

**Independent Test**: In a fixture where one baseline drifted by *only adding* a `val`, run `surface --check`: confirm the file is classified `additive` (bump `minor`) and the run still exits 1 (drift). In a second fixture where a baseline drifted by *removing* a `val`, confirm the file is classified `breaking` (bump `major`) and the run exits 1. In a third where a `val`'s type changed, confirm `breaking`.

**Acceptance Scenarios**:

1. **Given** a drifted baseline whose source contains every member the baseline declared plus one new `val`, **When** `surface --check` runs, **Then** that file is classified `additive`, its recommended bump is `minor`, and the run still exits 1 (the file is still drift).
2. **Given** a drifted baseline whose source no longer declares a member the baseline had (removed or renamed), **When** `surface --check` runs, **Then** that file is classified `breaking`, its recommended bump is `major`, and the run exits 1.
3. **Given** a drifted baseline whose source changed the signature of an existing member (e.g. a `val`'s type), **When** `surface --check` runs, **Then** that file is classified `breaking` (the prior declaration is gone), and the run exits 1.
4. **Given** a drifted file classified `additive` and, in the same run, another drifted file classified `breaking`, **When** the run completes, **Then** each file carries its own classification and the run-level verdict is `breaking` with recommended bump `major` (the most severe wins).

---

### User Story 2 - Scope the event to shipped-surface mutations only (Priority: P1)

The author runs `surface --check` in a tree that has both a genuinely drifted baseline **and** a brand-new source `.fsi` with no committed baseline yet. The command classifies only the drifted (already-shipped) surface; it does **not** classify the new source as a change, because a surface with no committed baseline is a *fresh registration*, not a *mutation* of a shipped surface. The new file is still reported as `missing-baseline` drift (feature 086), and the run still exits 1, but it carries no additive/breaking verdict — it never inflates the mutation classification.

**Why this priority**: Correct scoping is what makes the classification trustworthy for driving a coherent-set version bump. If a first-ever surface were labeled "additive," the tool would prompt a spurious minor bump on a package that has never shipped. Keeping the event to `drifted`-only is on the critical path.

**Independent Test**: In a fixture with one drifted baseline (additive) and one source `.fsi` that has no baseline, run `surface --check`: confirm exactly one file is classified (`additive`), the baseline-less file is reported `missing-baseline` with **no** classification, the run verdict is `additive`/`minor`, and the run exits 1.

**Acceptance Scenarios**:

1. **Given** a source `.fsi` with no committed baseline, **When** `surface --check` runs, **Then** it is reported as `missing-baseline` drift (feature 086) and it carries **no** additive/breaking/cosmetic classification.
2. **Given** a tree whose only drift is one or more `missing-baseline` files (no `drifted` file), **When** `surface --check` runs, **Then** the run-level classification verdict is `none` (no shipped-surface mutation) and the recommended bump is `none`, even though the run exits 1 for the missing baselines.
3. **Given** a `matched` file (baseline byte-identical to source), **When** `surface --check` runs, **Then** it carries no classification (there is no delta to classify).

---

### User Story 3 - Consistent projections and a cosmetic-change carve-out (Priority: P2)

The author consumes the classification through the standard three projections and gets the same facts from each: the default machine-readable projection is a deterministic automation contract, `--text` is a portable plain report, and `--rich` is a Spectre panel that degrades to plain text. Each carries the per-drifted-file classification and the run-level verdict with its recommended bump. A drifted file whose declared-member set is unchanged — only comments, blank lines, or member ordering differ — is classified `cosmetic` (recommended bump `none`), so a formatting-only edit is never mislabeled as a real surface change while still being honestly reported as drift.

**Why this priority**: Parity across projections and honest cosmetic handling make the verdict trustworthy for both automation (the downstream version-bump prompt keys off the machine-readable verdict) and human review, but they layer on top of the core additive/breaking classification.

**Independent Test**: In a fixture where one baseline drifted by only reordering members and adding a comment (no member added or removed), run `surface --check`: confirm the file is classified `cosmetic` with recommended bump `none`, the file is still reported `drifted` (exit 1), and all three projections carry an identical fact set (per-file classifications, run verdict, recommended bump), with the redirected rich output containing zero color/box control sequences.

**Acceptance Scenarios**:

1. **Given** a drifted baseline whose declared-member set is identical to the source (only comments / blank lines / member ordering differ), **When** `surface --check` runs, **Then** the file is classified `cosmetic`, its recommended bump is `none`, and it is still reported as `drifted` (exit 1).
2. **Given** any classified run rendered as the default projection, `--text`, and `--rich` (redirected/color-disabled), **When** the outputs are compared, **Then** they carry an identical classification fact set (per-file classification, run-level verdict, recommended bump) and the redirected rich output contains zero color/box control sequences.
3. **Given** an unchanged tree run twice with the default projection, **When** the two machine-readable outputs are compared, **Then** they are byte-identical (the classification facts are deterministically ordered).

### Edge Cases

- **No drifted files at all** (all `matched`, or only `missing-baseline`/`orphan`): the run-level classification verdict is `none` and recommended bump is `none`. The classification section is present but empty of per-file entries; the exit code is governed solely by feature-086 drift, unchanged.
- **A member both removed and another added in the same file**: the presence of any removed/changed prior member makes the file `breaking` (recommended `major`) regardless of the additions — severity is dominated by the worst change.
- **Whitespace-/comment-only drift**: classified `cosmetic` (recommended `none`); it is still `drifted` and still exits 1 under `--check`, but recommends no version bump.
- **A source `.fsi` that cannot be parsed into members** (malformed signature text): the file is still reported `drifted`; its classification is `breaking` conservatively (the safe default — an unreadable delta is treated as potentially breaking so the operator inspects it) and this conservative fallback is flagged in the report. It never crashes the command.
- **`--update` mode over a drifted file**: the file's pre-update delta (old baseline vs source) is still classified and reported, so the author sees what the reconcile changed; `--update` still writes the baseline and exits 0 (feature 086 behavior unchanged).
- **Classification of a file with an empty baseline or empty source** (0 members on one side): all-added ⇒ `additive`; all-removed ⇒ `breaking`; both empty but bytes differ ⇒ `cosmetic`.
- **Rich output on a narrow / non-interactive terminal**: degrades to the plain-text classification report with zero color/box control sequences.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: When `surface` detects a `drifted` file (a committed baseline that exists and differs byte-for-byte from its source `.fsi`), the system MUST classify that per-file delta as exactly one of `additive`, `breaking`, or `cosmetic`, derived by comparing the set of declared members parsed from the two signature texts.
- **FR-002**: A delta MUST be classified `additive` when every member declared in the baseline is still present in the source AND the source declares at least one member not in the baseline (members were only added, none removed or changed).
- **FR-003**: A delta MUST be classified `breaking` when at least one member declared in the baseline is absent from the source — covering a removed member, a renamed member, and a member whose signature changed (a changed signature reads as the prior declaration being gone). Any removed/changed prior member makes the file breaking regardless of additions.
- **FR-004**: A delta MUST be classified `cosmetic` when the source and baseline differ byte-for-byte but their declared-member sets are identical (the difference is only comments, blank lines, or member ordering).
- **FR-005**: The system MUST classify only `drifted` files. A `missing-baseline` file (no committed baseline — a new surface / fresh registration), a `matched` file (no delta), and an `orphan` baseline (no source) MUST NOT carry an additive/breaking/cosmetic classification.
- **FR-006**: The system MUST derive a run-level classification verdict equal to the most severe per-file classification, ordered `breaking` ≻ `additive` ≻ `cosmetic`, and `none` when no file is `drifted`.
- **FR-007**: The system MUST report a recommended coherent-set bump mapped from the verdict — `breaking` → `major`, `additive` → `minor`, `cosmetic` → `none`, and `none` → `none` — as advisory guidance; the command MUST NOT itself perform or confirm any version bump.
- **FR-008**: Classification MUST NOT change the process exit code. A `drifted` file still causes `--check` to exit 1 exactly as in feature 086; a coherent tree exits 0; `--update` reconciles and exits 0. Classification adds reported facts only and never introduces a new blocking diagnostic.
- **FR-009**: Classification MUST be computed from the `.fsi` **text** (the parsed member declarations), never from a compiled assembly or reflection, consistent with feature 086's byte-for-byte source-of-truth.
- **FR-010**: Member comparison MUST ignore comments, blank lines, and member ordering — so that a difference in only those classifies as `cosmetic` (FR-004) rather than additive or breaking.
- **FR-011**: A source `.fsi` whose member set cannot be parsed MUST be classified `breaking` (the conservative default so the operator inspects it) with that fallback indicated in the report; it MUST NOT crash the command or change the exit code.
- **FR-012**: The system MUST carry the classification facts — each drifted file's classification, the run-level verdict, and the recommended bump — in all three projections (default/`--json`, `--text`, `--rich`), each carrying an identical fact set; the default projection MUST be deterministic (stable field and file ordering) so repeated runs on an unchanged tree are byte-identical.
- **FR-013**: The `--rich` projection MUST degrade to the plain-text classification report with zero color/box control sequences whenever output is non-interactive, redirected, or color is disabled, adding or dropping no classification fact relative to `--text`/`--json`.
- **FR-014**: The classification MUST add only an additive fact to the in-memory command report; it MUST NOT change any on-disk artifact schema (work model, readiness, provenance) and MUST NOT write any new persisted file.
- **FR-015**: The classification MUST embed NO provider/package-specific name, path, or id; the `<Pkg>` segment MUST be derived structurally from the source-relative path (as in feature 086), so the classification works generically in any scaffolded workspace.

### Key Entities *(include if feature involves data)*

- **Surface Classification**: The additive run-level fact on the command report. Represents the classification pass over the drifted set: the ordered per-file classifications, the run-level verdict (`breaking` / `additive` / `cosmetic` / `none`), and the recommended coherent-set bump (`major` / `minor` / `none`).
- **Classified Entry**: One element — a drifted source-relative `.fsi` path, its per-file classification (`additive` / `breaking` / `cosmetic`), the recommended bump for that file, the members added, the members removed-or-changed, and whether the conservative unparseable fallback was applied.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: For a drifted baseline that only added members, `surface --check` classifies the file `additive` with recommended bump `minor`; for a drifted baseline that removed, renamed, or changed the signature of a prior member, it classifies the file `breaking` with recommended bump `major` — verified on fixtures for each case.
- **SC-002**: Classification never changes the exit code: a drifted tree exits 1 (feature 086) regardless of whether the drift is additive, breaking, or cosmetic, and a coherent tree exits 0 — confirmed by exit-code assertions across all three classifications.
- **SC-003**: A `missing-baseline` file carries no classification and does not contribute to the run-level verdict; a tree whose only drift is missing baselines reports run verdict `none` / bump `none` while still exiting 1 — confirmed on a mixed fixture.
- **SC-004**: A drifted file whose declared-member set is unchanged (only comments / ordering / whitespace differ) is classified `cosmetic` with recommended bump `none`, and is never reported as additive or breaking — confirmed on a formatting-only fixture.
- **SC-005**: For any single run, the default/`--json`, `--text`, and `--rich` projections carry an identical classification fact set (per-file classifications, run verdict, recommended bump); the redirected/color-disabled rich output contains zero color/box control sequences.
- **SC-006**: The run-level verdict equals the most severe per-file classification: a run mixing an additive and a breaking drifted file reports run verdict `breaking` / bump `major` — confirmed on a mixed fixture.
- **SC-007**: No provider/package-specific literal appears in the classification implementation or output; classification produces correct results on a fixture whose package names it has never seen (generic-SDD purity).
- **SC-008**: Repeated `surface --check` runs on an unchanged drifted tree produce byte-identical default-projection output (deterministic classification ordering).

## Assumptions

- **The source of truth is the `.fsi` text, not the compiled assembly.** Classification parses member declarations from the two signature texts and compares them; it needs no build and no reflection, matching feature 086. The published-package direction is already gated separately by ApiCompat; this classifies the committed-baseline direction, at source, before publish.
- **"Member" means a declared signature element** in the `.fsi` — a `val`, a type/record/union/abbreviation declaration, an interface/class member, etc. — identified by its normalized declaration text with comments and surrounding whitespace stripped. Two declarations are "the same member" when their normalized text is equal; a member whose signature changed therefore reads as one declaration removed and one added, which FR-003 treats as breaking.
- **Only `drifted` files are shipped-surface mutations.** A `missing-baseline` file is a fresh registration (a new surface), not a mutation, and is out of scope for this classification event; a `matched` file and an `orphan` baseline have no delta to classify.
- **Classification is advisory-but-loud and never gates.** It maps to a recommended coherent-set bump but performs no bump and changes no exit code. The downstream publishing prompt (the sibling issue FS-GG/FS.GG.SDD#171) and the `.github` registry/consumer reconcile key off this verdict; this feature only produces it.
- **A `cosmetic` third outcome is intentional.** ADR-0025 headlines additive and breaking; a formatting-/ordering-/comment-only drift is neither, so it is reported as `cosmetic` → no bump to avoid a spurious version bump while still honestly reporting the byte-level drift (feature 086).
- **Conservative fallback on unparseable signatures.** If the source `.fsi` cannot be parsed into members, the file is classified `breaking` so the operator inspects it, rather than silently under-reporting; the fallback is flagged in the report.
- **No persisted-artifact schema change.** The classification adds an additive `Classification` fact to the in-memory command report only; it does not change any on-disk artifact schema and writes no new file. It does not replace FS.GG.SDD's own internal reflection-based `PublicSurface.baseline` test (a separate component-repo mechanism, as in feature 086).
