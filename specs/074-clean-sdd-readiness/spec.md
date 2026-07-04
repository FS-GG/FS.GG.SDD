# Feature Specification: Clean SDD's own committed readiness tree + guard it by role

**Feature ID**: 074-clean-sdd-readiness
**Branch**: `074-clean-sdd-readiness`
**Date**: 2026-07-04
**Roadmap**: closes [#112](https://github.com/FS-GG/FS.GG.SDD/issues/112) (SDD-repo cleanup child); advances [#110](https://github.com/FS-GG/FS.GG.SDD/issues/110) (cross-repo umbrella)
**Decision**: [ADR-0018](https://github.com/FS-GG/.github/blob/main/docs/adr/0018-transient-durable-sdd-artifact-taxonomy.md) — transient vs durable SDD artifact taxonomy; regenerable output gitignored by role, tree cleanup without history rewrite
**Contract**: none new. Consumes the taxonomy shipped in [073](../073-transient-artifact-taxonomy/spec.md) (`docs/reference/artifact-taxonomy.md`). No product source, no schema, no CLI behavior change.

## Context

Feature 073 ([#113](https://github.com/FS-GG/FS.GG.SDD/pull/113)) delivered the **SDD producer
slice** of ADR-0018: the durable-vs-regenerable taxonomy doc and a no-clobber seeded `.gitignore`
that makes a *scaffolded consumer product* ignore regenerable `readiness/<id>/` output at birth.
It explicitly deferred the **per-repo tree cleanups** — including SDD's own — to the children of
#110.

This feature is SDD's own-repo cleanup (#112). It carries **no** convention work (073 shipped
that) and **no** product-source change — it is repo hygiene plus a role-based ignore guard, applied
to this repo dogfooding-style.

### Reproduced against `HEAD` (per cross-repo discipline)

- **SDD's own tree is NOT clean.** `git ls-files 'specs/*/readiness/*'` returns **214 tracked
  files** (~16.9k lines: 162 `.txt`, 47 `.md`, 5 `.json`) of per-feature run evidence (build/pack
  logs, fsi sessions, focused test output). There is **no `.gitignore` rule** for
  `specs/*/readiness/` — it is committed unguarded.
- **073 mismeasured this.** `specs/073-.../spec.md` lines 37–39 claimed "SDD's own footprint is
  already clean … `specs/*/readiness/` is untracked." That is wrong: it conflated the root
  `readiness/` dir (11 deliberately-pinned proof files under `readiness/019|020|021/`, incl.
  authored `evidence.yml` + captured rich/ansi surfaces) with the 214-file `specs/*/readiness/`
  self-dev tree. #112 correctly identifies the SDD-repo cleanup as still outstanding.
- **SDD's self-dev readiness path differs from a consumer's.** SDD develops itself with standard
  Spec Kit (`specs/NNN/…`), so its per-feature evidence lands at **`specs/<id>/readiness/`** (flat
  files). A product driven by the `fsgg-sdd` lifecycle instead emits `readiness/<id>/…`, which the
  073 seeded fragment (`readiness/*/`) targets. SDD's own rule must therefore target
  **`specs/*/readiness/`**, not the consumer shape.
- **Nothing reads the 214 files from disk.** The only code/test references to `specs/*/readiness/`
  paths are (a) two evidence tests (`EvidenceCommandTests.fs`, `EvidenceArtifactTests.fs`) that
  embed a `specs/011-.../readiness/…` path only as a **string value inside inline YAML heredocs**
  (parsed for diagnostics, never opened), and (b) historical prose pointers in
  `docs/initial-implementation-plan.md`. No test or product code performs a filesystem read of any
  file under `specs/*/readiness/`. So untracking them keeps the suite green.
- **These 214 are all regenerable, none are pinned proofs.** They are exactly the transient
  per-run evidence ADR-0018 classifies as regenerable. The root `readiness/019|020|021/` proofs
  (11 files) were placed there deliberately and are **out of scope** — they stay committed.

## Goal

SDD's own repository stops committing regenerable per-feature readiness output: the 214 tracked
files under `specs/*/readiness/` are removed from the index (working tree and git **history**
preserved — no rewrite), and a single role-based `.gitignore` rule prevents them recurring, so
future SDD features never re-commit readiness by whack-a-mole.

## Functional Requirements

- **FR-001 — Role-based ignore rule (SDD's own tree).** Add one rule to SDD's root `.gitignore`
  that ignores regenerable per-feature readiness output by directory *role* —
  `specs/*/readiness/` — with a comment referencing ADR-0018 /
  `docs/reference/artifact-taxonomy.md`. Ignore by role, not per-feature exception. The rule must
  **not** match the deliberately-pinned root `readiness/019|020|021/` proofs.

- **FR-002 — Untrack the regenerable readiness files.** Remove the 214 tracked files under
  `specs/*/readiness/` from the git index (`git rm --cached`, tree-only, no history rewrite per
  ADR-0018). The working-tree copies remain locally; they are gone from future checkouts and
  retained in history.

- **FR-003 — Pinned proofs preserved.** The 11 committed root `readiness/019|020|021/` files
  (authored `evidence.yml` + captured proof surfaces) stay tracked and untouched. No `specs/*/`
  file that a test or product reads from disk is removed (verified: none is).

- **FR-004 — Suite stays green; regeneration confirmed.** After the rule + untracking, a clean
  Release build and the full test suite pass unchanged — demonstrating nothing depended on the
  committed evidence. The per-feature readiness role remains regenerable on demand (it is run
  output, recreated when a feature's checks are re-run); no fixture is lost.

- **FR-005 — Descriptive, additive, backward-compatible.** No product source, schema, generator,
  gate, CLI behavior, or command-report change. Only `.gitignore` and the tracked-file set change.
  Persisted schemas stay at their current versions.

## Acceptance Criteria

- **AC-001** — SDD's root `.gitignore` contains a `specs/*/readiness/` rule with an ADR-0018 /
  taxonomy-doc comment; `git check-ignore specs/001-sdd-artifact-model/readiness/build.txt`
  reports it ignored, while `git check-ignore readiness/019-spectre-rendering/evidence.yml` does
  **not**. (FR-001, FR-003)
- **AC-002** — `git ls-files 'specs/*/readiness/*'` returns **0** files after the change (down from
  214); `git status` shows a clean tree (the untracked working-tree copies are ignored, not
  reported). (FR-002)
- **AC-003** — `git ls-files 'readiness/*'` still returns the 11 pinned proof files. (FR-003)
- **AC-004** — Clean `dotnet build -c Release` and full `dotnet test` pass with no new failures;
  the two evidence tests that reference `specs/011-.../readiness/…` paths still pass (they parse
  strings, they do not read the files). (FR-004)
- **AC-005** — No JSON-contract byte, exit code, stream routing, schema version, or seeded-skeleton
  set changes; `git diff` touches only `.gitignore` plus the index removal of the 214 files, plus
  this feature's `specs/074-…/` authored sources. (FR-005)

## Scope boundaries (NOT this feature)

- Any change to the 073 **consumer** seeded `.gitignore` fragment or the taxonomy doc (073 owns
  those; this is descriptive of them only).
- Rendering / Governance / Templates exception-list collapse and tree cleanups (other #110
  children).
- Any git **history** rewrite or `filter-repo` (ADR-0018: tree cleanup only; history is retained).
- Removing or re-homing the root `readiness/019|020|021/` pinned proofs.
- Deleting the historical prose pointers in `docs/initial-implementation-plan.md` (they remain
  valid against history).
