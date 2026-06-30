# Contract: `.fsgg/early-stage-guidance.md` (static early-stage authoring guidance)

**Owner**: FS.GG.SDD (generic, lifecycle-generic). **Producer**: `fsgg-sdd init`.
**Kind**: authored skeleton artifact (Markdown authoring surface) — *not* a generated
view, *not* a release-catalog entry, *not* a machine contract.

## Production

- Seeded by one new `WriteFile(".fsgg/early-stage-guidance.md", earlyStageGuidanceText,
  AgentGuidanceTarget)` in `Foundation.initEffects` (alongside
  `.fsgg/constitution.md`).
- `earlyStageGuidanceText` is an embedded F# string literal with **no** date,
  timestamp, random value, repo name, or provider/rendering token (mirroring
  `constitutionText`). → `init` output is byte-identical on every run.

## No-clobber semantics (US3 AC3)

Governed by the existing `canOverwrite` (`CommandEffects.fs:42-48`):

| On-disk state at re-run | Behavior |
|---|---|
| absent | written |
| byte-identical to seed | idempotent no-op (`NoChange`, `preserveExisting`) |
| present, author-edited (differs) | **refused** — bytes preserved, `unsafeOverwrite` surfaced |

`refresh` never regenerates it (authored, not in `refreshCanonicalViews`).

## Content contract

For **each** pre-work-model stage (`charter`, `specify`, `clarify`, `checklist`) the
file MUST state:

1. the `fsgg-sdd` command that runs the stage;
2. the required section headings the stage's artifact must contain — **verbatim** the
   live standard-section list for that stage;
3. the stable-id formats the artifact uses (e.g. `FR-###`, `AC-###`, `US-###`,
   `AMB-###`, `CQ-###`, `DEC-###`, `CHK-###`, `CR-###`).

The file MUST also restate, from `docs/reference/authoring-contracts.md` (never
redefining them):

4. **§1.1 acceptance coverage line** — `checklist` marks `FR-###` covered only for a
   list item of the form `- FR-###: <prose> (AC-### …)` with the acceptance reference
   on the same line; bold ids, colon-less ids, and off-line acceptance refs do **not**
   establish coverage;
5. **§1.2 `evidence.yml` satisfaction** — an obligation is satisfied **only** by a
   matching declaration with `result: pass` **and** `synthetic: false`; a synthetic
   pass discloses a stand-in and does not satisfy; `deferred`/`fail`/`missing`/`stale`/
   `blocked` do not satisfy.

And a **lifecycle pointer** (FR-008): once `verify`/`ship` builds
`readiness/<id>/work-model.json`, the generated
`readiness/<id>/agent-commands/<target>/` views are authoritative; this static
guidance covers only the pre-work-model window and does not shadow them.

## Verification

- **SC-001 / FR-001–003**: present and complete in a freshly `init`-ed skeleton.
- **SC-004 / FR-007**: two `init` runs produce byte-identical file content.
- **US3 AC3**: an edited copy is preserved on re-run (no-clobber).
- **SC-003 / FR-007**: drift-guard (see `guidance-drift-guard.md`) — every named
  heading/id/command/path/contract resolves.
- **FR-009**: one generic file; no per-target divergence.
