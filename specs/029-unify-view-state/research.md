# Phase 0 Research: Unify generated-view-state construction

All Technical Context unknowns are resolved below. There are no `NEEDS CLARIFICATION`
markers in the spec; the open decisions are design choices, recorded here.

## R-1: Canonical constructor — home, name, and signature

**Decision.** Keep exactly one constructor named **`generatedViewState`** with the
signature of today's `shipGeneratedViewState`:

```
generatedViewState : path:string -> kind:string -> generator:GeneratorVersion
    -> sources:GeneratedViewSource list -> outputDigest:OutputDigest option
    -> currency:GeneratedViewCurrency -> diagnosticIds:string list -> GeneratedViewState
```

Host it in `Foundation.fs`. Delete `analysisGeneratedViewState`,
`verifyGeneratedViewState`, and `shipGeneratedViewState`.

**Rationale.**
- The four bodies are character-for-character identical except the `Kind` literal, and
  `shipGeneratedViewState` already generalizes over `kind` and is in production use for
  five distinct kinds (`"analysis"`, `"verification"`, `"ship"`, `"governance-handoff"`,
  `"agent-commands"`) — so the parameterized shape is proven to type-check and serialize
  correctly today.
- `Foundation.fs` compiles first (fsproj line 14) and already `open`s
  `FS.GG.SDD.Artifacts.WorkModel`, so every type in the record (`GeneratedViewState`,
  `GeneratedViewCurrency`, `GeneratorVersion`, `GeneratedViewSource`) is in scope and the
  binding precedes all ~20 call sites — no reordering of `fsproj` entries needed.
- Name `generatedViewState` (not `shipGeneratedViewState`) because the construct is not
  ship-specific; it is the generic view-state builder. This matches the most-used current
  name and minimizes churn for the 11 workModel sites (they keep the symbol; they only add
  a `"workModel"` argument).

**Alternatives considered.**
- *A fresh name* (`makeGeneratedViewState`, `generatedView`): rejected — needlessly
  renames the dominant symbol and every workModel call site for no readability gain.
- *Host in `ViewGeneration.fs`* (where two defs live today): rejected — `ViewGeneration.fs`
  compiles after nothing depends on it being later, but `Foundation.fs` is the established
  home for cross-handler primitives and keeps the constructor strictly upstream of every
  consumer including `ViewGeneration.fs` itself.

## R-2: The `HandlersAgents` local-name collision

**Decision.** Rename the local string binding `HandlersAgents.fs:364`
`let generatedViewState = "blocked"|…` to `generatedViewStateLabel`, updating the single
`GeneratedViewState = generatedViewState` field assignment to match.

**Rationale.** Once the module-level constructor is named `generatedViewState`, the local
string of the same name is a shadowing readability trap even though it compiles (the
constructor is invoked earlier in the same function, at line 345). The local denotes a
*status label string* for `AgentGuidanceSummary.GeneratedViewState`, a semantically
different thing. The rename is output-neutral (a local binding + one field RHS) and removes
the ambiguity.

**Alternatives considered.** Leave the shadow (compiles, but misleading — rejected);
rename the constructor instead (loses the natural name and churns more sites — rejected).

## R-3: P3 (`blockedWorkModelView`) — include now or defer

**Decision.** Include P3 in this feature. Extract `blockedWorkModelView`:

```
blockedWorkModelView : path:string -> generator:GeneratorVersion
    -> blockingIds:string list -> GeneratedViewState
// = generatedViewState path "workModel" generator [] None GeneratedViewCurrency.Blocked blockingIds
```

**Rationale.** The 9 sites are identical except `path` and `ids`, all in handler files,
and **none** in `computeRefreshPlan` — so the spec's deferral trigger (risk to refresh's
self-contained guard) does not fire. Extracting alongside P1/P2 keeps the change cohesive
and is the same risk class (byte-identical, internal). `ViewGeneration.fs:562` is *similar*
but not identical (non-empty `sources`, `request.GeneratorVersion`, `blockingCommandIds`)
and is **excluded** — it stays a direct `generatedViewState` call.

**Alternatives considered.** Defer P3 to a follow-on spec: rejected — no coupling to
refresh, so the deferral rationale in the spec does not apply; splitting would add a second
PR for ~9 trivially-safe sites.

## R-4: Test posture for a behavior-preserving internal refactor

**Decision.** Rely on the existing command-output golden/snapshot suite as the binding
gate; add **no** new internal-only unit test. Verification is byte-identical
`--json`/`--text` output for every command (esp. those emitting each view kind:
charter/workModel, analyze, verify, ship/handoff, agents, refresh) plus byte-identical
`.fsi` and all four `PublicSurface.baseline` files.

**Rationale.** Constitution VI requires fail-before/pass-after tests for *behavior-changing*
code; this is Tier 2 and behavior-preserving, so the relevant evidence is the unchanged
golden output. The internal bindings live in `FS.GG.SDD.Commands.Internal` with no
`InternalsVisibleTo`, so they are not directly reachable from the test projects — a unit
test would have to be added *inside* the Commands assembly, which is not the established
pattern for these refactors (R1/R2/R5/R6 all gated on golden output, not new internal
tests). If the implementer finds a view kind *not* already covered by a golden case, add a
command-level golden for that kind rather than an internal unit test.

**Alternatives considered.** Add `InternalsVisibleTo` + a constructor-equivalence unit
test: rejected — widens the assembly's test surface for a row that golden output already
pins, and `InternalsVisibleTo` is itself a (small) surface change this Tier-2 row should
avoid.

## R-5: Byte-identical-output guarantee

**Decision.** No special measures needed beyond preserving the body verbatim.

**Rationale.** The unified body is the existing body with `Kind` swapped from a literal to
the `kind` parameter; all callers pass exactly the literal that was previously hardcoded
(`"workModel"`/`"analysis"`/`"verification"`/the ship kinds). `SchemaVersion = Some 1`,
`Sources |> List.sortBy _.Path`, and `DiagnosticIds |> List.distinct |> List.sort` are
unchanged, so every produced record — and therefore every serialized byte — is identical.
`blockingDiagnosticIds` and `blockedWorkModelView` are extract-and-inline-equivalent: same
filter predicate, same `List.map _.Id`, same constructor arguments.

## Resolved unknowns summary

| Unknown | Resolution |
|---|---|
| Constructor name/home/signature | `generatedViewState`, `Foundation.fs`, ship signature (R-1) |
| Local-name collision in HandlersAgents | rename local → `generatedViewStateLabel` (R-2) |
| P3 scope | include now; exclude the non-identical `ViewGeneration.fs:562` (R-3) |
| Test posture | golden/byte-identical; no new internal test (R-4) |
| Output stability | guaranteed by verbatim body + literal-for-literal kind args (R-5) |
