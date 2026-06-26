# Phase 0 Research: Extract a shared JSON view-parser skeleton

All technical context was resolvable from the repository; there are no open
`NEEDS CLARIFICATION` items. The decisions below record the choices that shape
Phase 1.

## Decision 1 — Where the skeleton lives

**Decision**: Add `parseJsonView` to the existing `module internal Internal` in
`src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`.

**Rationale**:
- `Internal` is `[<AutoOpen>] module internal` with **no `.fsi`**, so anything
  added there is invisible to the public surface — satisfying FR-007 / Principle
  III (no public `.fsi` change) by construction.
- It is compiled at fsproj line 20, **before** all four parsers (Analysis 39–40,
  Verify 43–44, Ship 45–46, Guidance 47–48) and **after** its dependencies
  `SchemaVersion`/`Diagnostics`/`ArtifactRef`/`Identifiers` (12–19). No fsproj
  reordering needed.
- It already hosts the JSON helpers the parsers use (`jsonInt`, `jsonString`,
  `tryJsonProperty`, `jsonArray`, …) and the `sourceArtifact` builder, so the
  skeleton sits next to the primitives it composes.
- All four parser files already `open FS.GG.SDD.Artifacts.*` and inherit the
  AutoOpen `Internal`, so no `open` edits are required.

**Alternatives considered**:
- *New shared module (e.g. `ViewParser.fs`)*: more ceremony, a new fsproj entry,
  and a new file to justify. The spec explicitly allows reusing the R3 `Internal`
  layer; a dedicated module adds nothing here.
- *Put it in `Core.fs` (which has a `.fsi`)*: would force a public-surface
  decision and a baseline update for a purely internal helper. Rejected — it
  contradicts FR-007's "no public `.fsi` change".

## Decision 2 — Skeleton shape and the parameterization boundary

**Decision**: `parseJsonView` owns the invariant outer structure
(parse `JsonDocument`, read `schemaVersion`, `classifyRaw`, the **total**
version/status match, the three schema error arms, and the `try/with` malformed-
JSON catch). Each parser supplies exactly one callback, `build`, that receives
`(artifact, schema, root)` and returns `Result<'view, Diagnostic list>` — it does
the parser-specific identity validation and record/field construction.

Signature:

```fsharp
val parseJsonView:
    label: string ->
    malformedJsonCorrection: string ->
    build: (ArtifactRef -> SchemaVersion -> JsonElement -> Result<'view, Diagnostic list>) ->
    snapshot: FileSnapshot ->
        Result<'view, Diagnostic list>
```

**Rationale**:
- The four bodies are identical from `try`/`use document` down through the schema
  error arms and the catch; only the success body (identity match + record build)
  varies. Delegating just that success body via `build` keeps the skeleton total
  and single-sourced while leaving each parser's record types in its own file.
- The two string parameters absorb the only other per-parser variation:
  - `label` → the prose noun used by both the Malformed arm message
    (`"{label} is missing or has malformed schemaVersion."`) and the catch
    message (`"{label} JSON is malformed: {ex.Message}"`). Confirmed identical
    template across all four (see Decision 4).
  - `malformedJsonCorrection` → the catch-arm correction, which is the only piece
    that differs by artifact path (e.g. `readiness/<id>/analysis.json` vs
    `readiness/<id>/ship.json` vs the agent-commands `guidance.json`).
- `build` returns a `Result`, so each parser keeps its own identity-error arm
  (the nested `match createWorkId …, parseStage …`) verbatim — no behavior shift.

**Alternatives considered**:
- *Pass record-builder + field-parsers as many separate callbacks*: over-
  parameterizes. The success bodies differ structurally (Guidance validates
  `behaviorModelDigest` + non-empty `targetId`; the others validate `stage`), so a
  single `build` callback is both simpler and a better fit than threading N field
  functions through the skeleton.
- *Derive both messages from a single `label` and also derive the correction from
  a path*: rejected — the corrections are not a uniform `readiness/<id>/<x>.json`
  (Guidance uses "the generated agent-commands guidance.json"), so passing the
  correction string verbatim is safer than reconstructing it and risking a
  byte-diff (SC-004).

## Decision 3 — Making the match total (the FS0025 fix)

**Decision**: Replace the four non-exhaustive matches with one total match:

```fsharp
match compatibility.Version, compatibility.Status with
| Some schema, SchemaCompatibilityStatus.Current
| Some schema, SchemaCompatibilityStatus.Deprecated -> build artifact schema root
| _, SchemaCompatibilityStatus.Malformed
| None, SchemaCompatibilityStatus.Current
| None, SchemaCompatibilityStatus.Deprecated ->
    Error [ Diagnostics.malformedSchemaVersion artifact $"{label} is missing or has malformed schemaVersion." ]
| _, SchemaCompatibilityStatus.Unsupported ->
    Error [ Diagnostics.unsupportedSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
| _, SchemaCompatibilityStatus.Future ->
    Error [ Diagnostics.futureSchemaVersion artifact (rawVersion |> Option.defaultValue "") ]
```

**Rationale**:
- Coverage over `(SchemaVersion option) × (5-case status)`: `Some,Current` and
  `Some,Deprecated` → build; `None,Current`, `None,Deprecated`, and any
  `_,Malformed` → malformed-schema error; `_,Unsupported` → unsupported;
  `_,Future` → future. All 10 combinations have a defined outcome — the compiler
  sees the match as exhaustive, so FS0025 disappears (FR-003/FR-005/SC-001).
- The chosen outcome for the previously-unreachable `(None, Current/Deprecated)`
  state is "treat as malformed schema version" — the least-surprising error and
  the same diagnostic family as the existing Malformed arm (FR-004), per the
  spec's Assumptions.
- Folding `None, Current/Deprecated` into the same arm as `_, Malformed` keeps
  the error constructed in exactly one place (FR-002) and produces a byte-
  identical diagnostic to today's Malformed arm.

**Alternatives considered**:
- *Change `classifyRaw` to a single sum type that makes the impossible state
  unrepresentable*: a cleaner long-term model, but it edits `SchemaVersion`'s
  public `.fsi` and ripples into `Internal.schemaVersion` (the YAML path) and
  every other `classifyRaw` consumer — out of scope for a Tier 2 refactor that
  must hold public signatures. Deferred; not required to make the four parsers
  total.
- *A catch-all `| _ ->` arm*: would silence FS0025 but also silence FS0025 if a
  future status case is added, defeating the warning's purpose. Rejected in favor
  of enumerating statuses explicitly.

## Decision 4 — Confirming byte-identical messages (SC-004 safety)

**Decision**: Treat the existing per-parser message strings as the contract and
reproduce them exactly through `label` + `malformedJsonCorrection`.

**Evidence** (from the four current bodies):

| Parser | `label` | Malformed-arm message (template `"{label} is missing or has malformed schemaVersion."`) | Catch correction (`malformedJsonCorrection`) |
|---|---|---|---|
| Analysis | `Analysis view` | "Analysis view is missing or has malformed schemaVersion." | "Regenerate readiness/<id>/analysis.json with valid JSON." |
| Verify | `Verification view` | "Verification view is missing or has malformed schemaVersion." | "Regenerate readiness/<id>/verify.json with valid JSON." |
| Ship | `Ship view` | "Ship view is missing or has malformed schemaVersion." | "Regenerate readiness/<id>/ship.json with valid JSON." |
| Guidance | `Generated agent guidance` | "Generated agent guidance is missing or has malformed schemaVersion." | "Regenerate the generated agent-commands guidance.json with valid JSON." |

Catch message template is `"{label} JSON is malformed: {ex.Message}"` for all
four. The Unsupported/Future arms already use the identical
`(rawVersion |> Option.defaultValue "")` form across all four — no
parameterization needed there.

**Rationale**: Verifying the templates line up across all four parsers is what
makes the single skeleton safe; any divergence would surface as a failing
existing test (the suites assert on these diagnostic messages) and as a non-byte-
identical view (SC-004). The optional-defaults inside each success body
(`viewVersion` default `"1.0"`, status defaults like `"blocked"` /
`"needsVerificationCorrection"` / `"needsShipCorrection"`) stay inside each
parser's `build`, so they are untouched.

## Decision 5 — Regression strategy

**Decision**: The existing 437-test suite is the binding gate; no test is
weakened, skipped, or rewritten except mechanical call-site updates if any
(FR-008/SC-002). Add at most one new optional assertion for the impossible-state
totality case (SC-005). Verify FS0025 = 0 and unchanged FS3261 counts via a clean
`dotnet build` before/after (FR-005, FR-009, SC-001).

**Rationale**: No new behavior is introduced for any input the suite already
exercises, so the suite is a sufficient behavioral oracle; the only genuinely new
path (constructed `version = None` with current/deprecated status) is unreachable
through real fixtures and therefore needs a constructed-input assertion to be
demonstrated at all.
