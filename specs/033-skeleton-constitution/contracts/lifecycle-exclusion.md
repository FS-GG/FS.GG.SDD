# Contract: no-clobber on re-run; `refresh` leaves the constitution untouched

**Scope**: behavior contract over `init` re-run and the `refresh` command — reuses existing
no-clobber and refresh-exclusion machinery, no code path added.

## Guarantees

1. **No-clobber on re-`init` (FR-008)**. When `.fsgg/constitution.md` already exists with
   author-modified content, re-running `fsgg-sdd init` MUST preserve it. Mechanism:
   `AgentGuidanceTarget` ⇒ `canOverwrite` returns `false` for a differing existing file
   (`CommandEffects.fs:48`) ⇒ the write is refused; the report records
   `Operation = Refuse`, `SafeWriteDecision = "refused"`, and the file bytes are unchanged. An
   *unmodified* re-`init` is a `NoChange`/`"preserveExisting"` no-op (identical content). This is
   the identical policy applied to `CLAUDE.md`/`AGENTS.md`.

2. **`refresh` leaves it untouched (FR-009)**. `fsgg-sdd refresh` MUST NOT regenerate
   `.fsgg/constitution.md`, and MUST NOT report it as a generated view, a stale view, or an
   externally-owned (`generatedProduct`) path. Mechanism: `refresh` regenerates only a fixed set
   of `readiness/<id>/*` views and configured agent-guidance targets
   (`HandlersRefresh.fs:178-295`); no generator targets any `.fsgg/` root file. The constitution
   is authored content with no source digests or generator version, so no stale-view diagnostic
   can apply to it.

3. **Optional preserved-authored reporting (D3)**. `.fsgg/constitution.md` MAY be added to the
   informational `authoredPreserved` list (`HandlersRefresh.fs:113-123`) so the refresh summary
   reports it as preserved-authored, symmetric with `.fsgg/project.yml`/`sdd.yml`/`agents.yml`.
   This does not change protection (refresh never touches it regardless).

## Verification (real-filesystem, public surface)

- **US3-AC1**: `init`, edit `.fsgg/constitution.md`, re-run `init` ⇒ the edited bytes are
  preserved; the report shows the constitution as refused/preserved, not overwritten.
- **US3-AC2**: with an author-modified constitution present, run `refresh` ⇒ the file is byte-
  unchanged and appears in the refresh report (if at all) only as preserved-authored — never as a
  generated view, stale view, or `generatedProduct` path.
- **Determinism cross-check**: the modified content survives both `init` and `refresh` with zero
  unintended bytes changed (SC-004).

## Non-guarantees

- No change to the no-clobber policy of any other artifact; only the constitution's `kind`
  selection places it under the existing refuse-on-differing rule.
