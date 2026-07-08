# Phase 1 Data Model: Authored-Artifact Codec

The deliverable is one **codec per authored artifact** plus an explicit
**authored / tool-owned field partition**. This document defines the codec type,
the two partitions, and the invariants the round-trip property enforces.

## `FieldCodec<'M>` (new — `FS.GG.SDD.Artifacts/LifecycleArtifacts/ArtifactCodec.fs` + `.fsi`)

A field descriptor binds one field's *reader* and *writer* to one YAML key, so
the two cannot be declared independently. The concrete shape (sketched in
`contracts/artifact-codec.md`):

```
type FieldCodec<'M> =
    { Key    : string                          // the YAML key this field owns
      Read   : YamlMap -> 'M -> 'M              // fold: parse this key into the model-in-progress
      Write  : 'M -> string option }            // map: render this key, or None to omit
```

- An **artifact codec** is `FieldCodec<'M> list` plus an empty/seed model.
- `parse : YamlMap -> Result<'M, string>` = `List.fold (fun m f -> f.Read map m) seed fields`.
- `render : 'M -> string` = `fields |> List.choose (fun f -> f.Write m) |> String.concat "\n"`.

The single `fields` list is the shared field set: a key absent from it is neither
read nor written; a key present in it is both. This is the structural coupling
FR-001/FR-007 require.

### Optional-scalar helpers (R4)

- `optionalScalar key get set` — reads null-aware (`isPlainNullScalar` → `None`),
  writes omit-when-`None`. This is the default for every `'a option` field and is
  what closes #180 and #182 in one place.
- `requiredScalar key get set` — reads, errors if absent, always writes.
- `inlineList` / `scalarBlock` / `nested` — mirror the existing
  `yamlInlineList`/`renderScalarBlock`/nested-map shapes so byte-idempotence
  (FR-008) holds.

## Coupling test (FR-007)

`ArtifactCodecTests.fs` asserts, per artifact, that the set of `FieldCodec.Key`
values equals the set of authored-record labels (via a hand-maintained
`authoredKeys` list checked against the record, or reflection **in test code
only** — reflection is permitted in tests, not `src/`). Adding an authored field
to the record without a codec entry fails this test.

## Partition A — `evidence.yml`

### Authored (must round-trip)

| Field | Today | After |
|---|---|---|
| `evidence[].id/kind/subject/*Refs/artifacts/result/synthetic/rationale/owner/scope/laterLifecycleVisibility/notes` | round-trips (21 fields) | unchanged, via codec |
| `evidence[].syntheticDisclosure.standsInFor/reason` | null-unaware → `"null"` (#180) | null-aware; bare-null → `None` → gate fires |
| `evidence[].sourceRefs[].id` | **read, not written** (#181) | round-trips |
| `evidence[].sourceRefs[].digest` | **read, not written** (#181) | round-trips |
| `evidence[].sourceRefs[].relatedSourceId` | **read, not written** (#181) | round-trips |
| `evidence[].sourceRefs[].kind/path/uri/result` | round-trips | unchanged |
| `lifecycleNotes` | **clobbered with canned line** | round-trips (authored value preserved) |

### Tool-owned (regenerated; excluded from the property)

| Field | Owner |
|---|---|
| `sourceSnapshots[].label/path/digest/schemaVersion` | recomputed each run (`HandlersEvidence.fs:892`); the #182 invented-default is removed but the field stays tool-computed |
| `schemaVersion/workId/stage/status/sourceSpec/source*` | canonical/normalized by the evidence validator (`HandlersEvidence.fs:509-520`) |

## Partition B — `tasks.yml`

### Authored (must round-trip)

| Field | Today | After |
|---|---|---|
| `work.title` | **reverts to humanized id** | round-trips (parsed title preserved) |
| `work.publicOrToolFacingImpact` | **hardcoded `true`** | round-trips (`false` preserved) |
| `tasks[].id/title/status/owner/dependencies/requirements/decisions/sourceIds/requiredSkills/requiredEvidence/skipRationale` | round-trips | unchanged |
| `acceptedDeferrals/advisoryNotes/lifecycleNotes` | threaded on merge | via codec, explicit |

### Tool-owned (regenerated; excluded from the property)

| Field | Owner |
|---|---|
| `work.stage/status` (`tasksReady`) | canonical |
| `sources[].label/path/digest/schemaVersion` | recomputed from current source texts (`TaskGraphAuthoring.fs:534`) |
| `findings[]` | tool-signal, re-derived (`TaskGraphAuthoring.fs:908,1048`) — documented; a follow-up may make authored findings first-class, out of scope here |

> **Note on the `SourceIds`/`Requirements`/`Decisions` union (#189 / Gap D):**
> this codec preserves whatever the author wrote in each of the three fields
> byte-for-byte. It does **not** change which consumer reads which set — that
> reconciliation is invariant 5 (issue #204) and lands via #189, on which this
> feature's implementation is `Blocked by`. The codec must not pre-empt it.

## Invariants (enforced by the round-trip property + coupling test)

1. **Symmetry**: for every artifact, `parse(render(m)) = m` over the authored
   partition, for all generated `m` (FR-001, FR-005).
2. **Omission**: an authored `'a option = None` renders no line for its key —
   not an empty value, not an invented default (FR-002).
3. **Null-as-absence**: a bare-null YAML token parses to `None`; a quoted
   `"null"` string parses to `Some "null"` (R4, FR-003).
4. **Coupling**: the codec `Key` set equals the authored-record label set; a new
   authored field with no codec entry fails a test (FR-007).
5. **Idempotence**: re-rendering an unchanged authored file produces
   byte-identical output (FR-008).
6. **No contract drift**: no change to the `--json` command report, exit codes,
   or stream routing, except the #180 gate now firing (FR-009).
