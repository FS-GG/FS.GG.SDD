# Phase 0 Research: Authored-Artifact Codec and Round-Trip Property

All findings verified against source on `main` (`f09c239`) during the 2026-07-08
architecture audit. File:line references are exact.

## R1 — The asymmetry is structural, not incidental (the class)

**Decision**: Treat the parse/render divergence as one defect class with one
root cause, and fix the root, not the instances.

**Evidence**: The parser and renderer for each authored artifact live in
different assemblies and share no field list:

- `evidence.yml` — parser `parseEvidenceArtifact` (`Evidence.fs:192-382`) reads
  via `tryScalarAt`/`scalarList`; renderer `evidenceArtifactText`
  (`HandlersEvidence.fs:773-807`) writes via `yamlString` interpolation.
- `tasks.yml` — parser `Task.fs:105-296`; renderer `TaskGraphAuthoring.fs:518-583`.

Confirmed divergences (each an authored field read then not written, or written
as an invented value):

| Field | Read at | Written at | Failure |
|---|---|---|---|
| `sourceRefs[].id`/`.digest`/`.relatedSourceId` | `Evidence.fs:149-167` (6 fields) | `HandlersEvidence.fs:689-714` (4 fields) | deleted on re-run (#181) |
| `evidence.yml` `lifecycleNotes` | `Evidence.fs:367` | hardcoded canned line `HandlersEvidence.fs:806` | clobbered every run |
| snapshot `digest`/`schemaVersion` | `Evidence.fs:127-147` | `Option.defaultValue ""`/`"1"` `HandlersEvidence.fs:680-687` | invented value (#182) |
| `tasks.yml` `title` | `Task.fs:122` | `requestTitle` ignores parsed `HandlersEvidence`… `TaskGraphAuthoring.fs:519,524` | reverts every run |
| `tasks.yml` `publicOrToolFacingImpact` | `Task.fs:137` | hardcoded `true` `TaskGraphAuthoring.fs:531` | `false`→`true` every run |

**Rationale**: A point-fix (e.g. #161 fixed four top-level scalars) leaves every
field it did not name exposed. The class closes only when the two field sets are
*the same list*.

## R2 — A field-list codec, not a generic/reflective one

**Decision**: One `fields : FieldCodec list` per artifact; `parse` folds each
field's reader over the YAML node, `render` maps each field's writer to a line.
Plain records + `List.fold`/`List.map`.

**Rationale**: This makes "read a field / write a field" the *same declaration*,
so asymmetry is expressible only by editing one list — and a coupling test
(FR-007) reddens when a record label has no field entry. It stays within
Principle IV (records, DUs, functions).

**Alternatives considered**:
- *Reflective/SRTP generic codec* (derive read+write from the record type):
  rejected — Principle IV names reflection and SRTP-heavy code as requiring
  justification, and the field order/format here is hand-tuned for deterministic
  YAML output that a generic serializer would not reproduce byte-for-byte
  (breaking FR-008 idempotence).
- *Keep hand-written, add a round-trip test only*: rejected — a test catches an
  asymmetry after it is written; FR-007 requires the codec and the type to be
  *structurally* coupled so a new field cannot be added read-only or write-only
  in the first place.
- *A YamlDotNet POCO with attributes*: rejected — loses control over
  omit-when-`None`, deterministic key order, and the surgical
  `scalar`/`inline-list`/`scalar-block` layout the existing artifacts use.

## R3 — Authored vs tool-owned field partition

**Decision**: The round-trip property `parse(render(m)) = m` is defined over the
**authored** subset. Tool-owned fields (source snapshots, digests, canonical
`status`/`sourceSpec` the tool normalizes) are regenerated on each run and are
excluded from the property by construction.

**Evidence**: The sole caller overwrites `SourceSnapshots` with freshly-computed
snapshots (`HandlersEvidence.fs:892-893`) — snapshots are meant to refresh, so
their round-trip is *not* the invariant. In contrast `lifecycleNotes`,
`sourceRefs[].{id,digest,relatedSourceId}`, `title`, and
`publicOrToolFacingImpact` carry author intent and today are lost — they belong
in the authored partition. The partition is enumerated in `data-model.md`.

**Rationale**: Without an explicit partition, "the tool regenerated it" and "the
tool silently ate it" are indistinguishable. Naming each field's owner makes the
first legitimate and the second a test failure.

## R4 — Optional-scalar reads are null-aware by default

**Decision**: The codec's optional-scalar reader treats a bare-null YAML token
(`null`/`Null`/`NULL`/`~`/empty) as `None`. This becomes the default for every
optional field, collapsing the current 6-of-123 null-aware ratio to "all".

**Evidence**: `tryScalarNonNullAt` (`Internal.fs:95-106`) backed by
`isPlainNullScalar` (`Internal.fs:85-93`) already exists and is used at exactly 4
sites (`Evidence.fs:303-307`). The gate `Synthetic && Option.isNone
SyntheticDisclosure` is defeated by a bare-null at three sites —
`HandlersEvidence.fs:488`, `:560`, and `HandlersVerify.fs:184` — because
`parseSyntheticDisclosure` (`Evidence.fs:169-181`) uses the null-unaware reader
and `String.IsNullOrWhiteSpace "null"` is `false`.

**Rationale**: Absence is meaningful for every `Option` field; making the reader
null-aware everywhere removes an entire latent-bug axis rather than patching the
one gate #180 names. A quoted `"null"` string still round-trips as the string
(distinct from the bare token) — the edge case is covered by a reader test.

## R5 — FsCheck introduction (the property mechanism)

**Decision**: Add FsCheck (xUnit integration) as a **test-only** dependency and
write one round-trip property per authored artifact over a generator that ranges
each optional field present/absent.

**Evidence**: No property-based testing exists in the repo — `grep` for
`fscheck`/`hedgehog` across `Directory.Packages*.props` and the test `.fsproj`s
is empty. Determinism today is checked only as "run 3× and compare", a weaker
invariant than round-trip.

**Rationale**: FR-005 requires a property; FsCheck is the standard F# choice and
the generators double as the disclosure of what field space is covered
(Principle VI). It is confined to the test assemblies — no `src/` dependency, no
runtime cost. The generator excludes tool-owned fields (R3) so the property is
defined only over the authored subset.

**Alternatives considered**: a hand-rolled exhaustive fixture matrix — rejected,
it re-creates the "only the combinations someone thought of" blindness that let
#181 ship (no fixture carried a fully-populated `sourceRef`).

## R6 — Scope: two YAML artifacts; markdown is out

**Decision**: `evidence.yml` and `tasks.yml` only. The markdown artifacts
(`spec.md`, `plan.md`, `checklist.md`, `clarifications.md`, `charter.md`) are out
of scope.

**Evidence**: The markdown stages do **not** parse→full-render. They are
surgically edited: `ensure*Sections` appends only missing headings,
`replaceSectionBody`/`appendToSection`/`transformSectionBody`
(`EarlyStageAuthoring.fs:1246-1354`, `ChecklistPlanAuthoring.fs:382-389`) mutate
only machine-owned sections; authored front matter and prose in `existing.Text`
are preserved byte-for-byte (`EarlyStageAuthoring.fs:1337-1341`, FR-014 of
feature 090). No field-count asymmetry of the #181 kind exists there.

**Rationale**: The codec abstraction fits full parse↔render round-trips; the
surgical-edit model is a different, already-sound mechanism. Folding it in would
be scope creep with no defect to justify it.

## R7 — Behavior change and migration posture

**Decision**: Ship the #180 gate fix as a disclosed behavior change with a
migration note; no `schemaVersion` bump.

**Evidence**: A workspace with `syntheticDisclosure: { standsInFor: null, reason:
null }` under a `synthetic: true, result: pass` declaration currently passes the
evidence gate (exit 0) and will, after this change, block (exit 1) — at both
`evidence` and `verify`. The on-disk YAML *shape* is unchanged (same keys), so
`schemaVersion` stays `1`; what changes is that a previously-silent authored
value is now correctly interpreted.

**Rationale**: Per Principle VIII this is the correct outcome (synthetic evidence
must not masquerade as real), but per the Change Classification it is a Tier 1
behavior change owed a migration note in `docs/release/migrations/`. The
field-preservation fixes (#181/#182) *restore* authored content and are not
breaking. Whether to add a `RequiredKeys` registry row for `standsInFor`/`reason`
(so the evidence skill documents them) is folded into the tasks, mirroring
`requiredDeferralKeys`.
