# Implementation Plan: Clean SDD's own committed readiness tree

**Branch**: `074-clean-sdd-readiness` | **Spec**: [spec.md](./spec.md)

## Summary

Pure repo hygiene. Two edits to the repository, zero to product source:

1. Add one role-based rule to SDD's root `.gitignore`.
2. `git rm --cached` the 214 regenerable files under `specs/*/readiness/`.

Then prove the suite is unaffected. No `.fs`, schema, doc-contract, or CLI change.

## Design decisions

- **D1 — Rule shape: `specs/*/readiness/` (trailing slash, directory-only).** SDD dogfoods via
  Spec Kit, so its readiness output is at `specs/<id>/readiness/<files>`, not the consumer
  `readiness/<id>/…` shape that 073's seeded fragment (`readiness/*/`) targets. A trailing-slash
  directory pattern ignores the whole per-feature readiness dir by role. `specs/*/` (single path
  segment glob) matches each feature dir; it does **not** touch the root `readiness/` dir, so the
  pinned `readiness/019|020|021/` proofs are unaffected. Verified with `git check-ignore` (AC-001).

- **D2 — Placement + comment.** Add the rule as its own commented block in `.gitignore`, after the
  existing build/tooling ignores, pointing at ADR-0018 and `docs/reference/artifact-taxonomy.md`
  (the canonical convention 073 shipped) so the rule's provenance is self-documenting and matches
  the taxonomy's "ignore by role, not per-feature exception" doctrine. SDD's rule is intentionally
  *not* byte-identical to the consumer seed (different path shape, per D1) and is **not** drift-
  guarded against it — the seed guard (073 FR-005) governs the consumer fragment; this is SDD's own
  repo config.

- **D3 — Untrack, don't delete; no history rewrite.** `git rm --cached -- specs/*/readiness/`
  (via a glob-expanded file list) removes the 214 from the index while leaving working-tree copies
  in place, exactly ADR-0018's "leave the tree, keep history." No `filter-repo`, no force-push.
  Combined with D1, `git status` is clean afterward (removed-from-index files are now ignored, so
  not reported as untracked).

- **D4 — No pinned-proof loss (verified, not assumed).** Before untracking, the spec's HEAD
  reproduction confirmed the only references to `specs/*/readiness/` paths are string values in
  inline test YAML (never read) and historical doc prose. The full test run (T004) is the
  executable proof: if any file were a live fixture, a test would fail. Root `readiness/` proofs
  are excluded by D1's path shape.

- **D5 — No spec-catalog / release-readiness change.** This feature produces no lifecycle artifact
  and adds no generated view, so `docs/release/release-readiness.json` is untouched. `CLAUDE.md`
  boundary prose is unaffected (no seeded-set or command-behavior change).

## Verification plan

- `git check-ignore` positive (a `specs/*/readiness/` file) and negative (a root `readiness/`
  proof) — AC-001, AC-003.
- `git ls-files 'specs/*/readiness/*'` == 0; `git ls-files 'readiness/*'` == 11 — AC-002, AC-003.
- `dotnet build -c Release` clean; full `dotnet test` green (all suites) — AC-004.
- `git status` clean; final `git diff --stat` touches only `.gitignore`, the 214 index removals,
  and `specs/074-…/` sources — AC-005.

## Risks

- **A future clone loses the local working-tree copies.** Expected and intended — they are
  regenerable run output and remain in history. No test/product path depends on them (D4).
- **GitHub renders `docs/initial-implementation-plan.md`'s prose pointers as dead links** after the
  files leave `HEAD`. Accepted per ADR-0018 (out of scope; the pointers remain valid against
  history). Not rewriting that historical doc keeps the change minimal and reversible.
