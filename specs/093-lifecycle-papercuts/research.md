# Phase 0 Research: Lifecycle Authoring Papercuts

All findings verified against the tree at `d1c6e20` (`main`). Line numbers are from that commit.

## R1 — `unresolvedAmbiguityCount` is structurally unfixable where it lives

`Specification.fs:245-250`:

```fsharp
let unresolvedAmbiguityCount =
    body.Split('\n')
    |> Array.filter (fun line ->
        Regex.IsMatch(line, @"\bAMB-\d{3,}\b", RegexOptions.IgnoreCase)
        && not (Regex.IsMatch(line, @"\b(resolved|deferred)\b", RegexOptions.IgnoreCase)))
    |> Array.length
```

`body` is `spec.md`'s text. `SpecificationFacts` never receives `clarifications.md`. Therefore no
`clarify` invocation can move this number — the reported "persisted at 4 after everything resolved"
is the only behavior it can have.

The two counters that *do* gate come from `Clarification.fs` and are correct:

| Counter | Source | Gates? |
|---|---|---|
| `unresolvedAmbiguityCount` | `spec.md` body regex | **no** |
| `remainingAmbiguityCount` | `ClarificationFacts.RemainingAmbiguity.Length` | yes |
| `blockingAmbiguityCount` | `Clarification.fs:359` | yes (`LintEngine.fs:145,194-203` → `unresolvedBlockingAmbiguity`) |

**Decision: remove it from the report surface.** A third counter that agrees adds nothing; one that
disagrees is the bug.

### R1a — Is the key part of the frozen release surface? (No.)

`ReleaseContract.fs` `jsonInventory` (`:350-372`) freezes **top-level** report keys only:
`schemaVersion`, `reportVersion`, `command`, `context`, `invocation`, `outcome`, `changedArtifacts`,
`specification`, `clarification`, `checklist`, `plan`, `tasks`, `analysis`, `evidence`,
`verification`, `ship`, `agentGuidance`, `refresh`, `scaffold`.

`specification` is frozen as a *key*; the counters nested inside it are not enumerated.

- `grep -rn "ambiguit" src/FS.GG.SDD.Artifacts/ReleaseContract.fs docs/release/` → nothing.
- `grep -rni "ambiguit" src/FS.GG.SDD.Artifacts/GovernanceHandoff.fs` → nothing.

So removal crosses no frozen surface and no Governance boundary. `ReleaseConformanceTests` remains the
standing guard.

### R1b — Does `SpecificationFacts` keep the field?

Read sites for `UnresolvedAmbiguityCount`:

- `EarlyStageAuthoring.fs:581` → `SpecificationSummary.UnresolvedAmbiguityCount` (report only)
- `CommandSerialization.fs:37` → JSON key (report only)
- `CommandRendering.fs:26` → text key (report only)
- `RichRenderingTests.fs:76` → constructs the record in a test

Every read is report-facing. **The field is removed from `SpecificationFacts` too**, not just from the
report — leaving a computed-but-unread field would recreate FR-015's defect in a new place.

## R2 — The decision refs have zero read sites

```
$ grep -rn "RelatedRequirementIds\|RelatedStoryIds\|RelatedAcceptanceScenarioIds" src/ --include=*.fs --include=*.fsi
Clarification.fs:40,41,42     # ClarificationQuestion — definition
Clarification.fs:62,63,64     # ClarificationDecisionFact — definition
Clarification.fs:202,203,204  # parseClarificationQuestions — assignment
Clarification.fs:256,257,258  # parseClarificationDecisionsInSection — assignment
Clarification.fsi:36,37,38    # signature
Clarification.fsi:58,59,60    # signature
```

Definitions, signatures, assignments. **No reads.** The parse was always right; nothing ever asked.

Three further, independent drops on the same "a decision's refs" theme:

1. **`RequirementModel.parseDecisions` (`:98-130`)** matches `^\s*-\s*(DEC-\d{3,})\s*:\s*(.+)$` and
   builds `{ Id; Title; Decision; Source; SourceLocation }` — **no ref fields exist on the type at
   all**. This is the parser that feeds `WorkItem.decisions` (`:143-149`) and therefore
   `work-model.json`'s `DecisionEntry`. Fixing `ClarificationDecisionFact` alone would not move
   `work-model.json`.

2. **`parseRemainingAmbiguity` (`Clarification.fs:262-265`)** does
   `ambiguityIdsInLine line |> List.tryHead` — a `Remaining Ambiguity` line naming `AMB-002` and
   `AMB-004` records only `AMB-002`. Note `RemainingAmbiguity.AmbiguityId` is `AmbiguityId option`
   (singular), so this is a type change, not just an expression change.

3. **`TaskGraphAuthoring.clarificationDecisionTasks` (`:271-278`)** calls
   `maybeTask [decision.DecisionId.Value] title [] [decision.DecisionId] …` — the `[]` is the
   `requirements` parameter. It has `decision.RelatedRequirementIds` in hand and discards it.

**Decision:** (3) is the natural read site for `RelatedRequirementIds`, discharging FR-015 without
inventing a consumer. `RelatedStoryIds`/`RelatedAcceptanceScenarioIds` have no natural consumer on
`ClarificationQuestion`/`ClarificationDecisionFact` — they are threaded onto the `Decision` type
instead (R3), and the unread copies on the two clarification facts are **removed**.

### R2a — "Error on truly-invalid refs" is already implemented

`unknownReferenceDiagnostics` (`EarlyStageAuthoring.fs:882-916`) already builds
`knownAmbiguities`/`knownRequirements`/`knownStories`/`knownScenarios`/`knownQuestions` from
`specFacts` and emits `unknownClarificationReference` — an `errorForRef`, i.e. blocking — for any
`AMB-`/`FR-`/`US-`/`AC-`/`CQ-` id in the `--input` lines that does not resolve. Asserted at
`ClarifyCommandTests.fs:369`.

**No new diagnostic is needed.** The only work is a regression test proving that FR-011's multi-ref
threading does not route around the existing gate. Two gaps stay out of scope: a ref hand-typed into a
committed `clarifications.md` (never re-validated), and a malformed token (not matched by the id
regex, indistinguishable from prose).

## R3 — `sourceIds` vs `decisions`: a generated bucket vs. an authored typed field

The shipped, build-validated `docs/examples/lifecycle-artifacts/tasks.yml`:

```yaml
  - id: T001
    requirements: [FR-001]
    decisions: [DEC-001]
    # no sourceIds:
```

`ExampleArtifactsContractTests` parses it with the live parser on every build. So the *documented
authoring shape omits `sourceIds:` entirely*.

Consumers disagree about where a task's references live:

| Consumer | Reads | Site |
|---|---|---|
| `analyze` | `SourceIds ∪ Requirements ∪ Decisions` | `TaskGraphAuthoring.allTaskDispositionIds:717-724` |
| `evidence` | `SourceIds` **only** | `HandlersEvidence.fs:212` |
| `verify` | `SourceIds` **only** | `HandlersVerify.fs:37,154,344` |
| agent guidance | `Requirements @ Decisions` **only** | `WorkModel.deriveGuidanceModel:879` |

`analyze` reads the union, which is why the incoherence was never caught: the only stage that would
have complained sees everything.

The generator writes the same id twice: `clarificationDecisionTasks` passes
`sourceIds = [decision.DecisionId.Value]` **and** `decisions = [decision.DecisionId]`. That is the
duplication the FS.GG.Game feedback observed as "one looks vestigial".

**Decision: typed fields are canonical; `SourceIds` becomes derived.**

```fsharp
SourceIds =
    (scalarList ["sourceIds"] mapping) @ (requirements |> List.map _.Value) @ (decisions |> List.map _.Value)
    |> List.map _.ToUpperInvariant()
    |> List.distinct
    |> List.sort
```

Rejected alternative — delete `decisions:`, make `sourceIds` authoritative: breaks the shipped
example, discards the FR/DEC type distinction `ViewGeneration.dispositionRelationships` relies on, and
makes a hand-authored graph less legible.

### R3a — Is deriving a schema change?

No. `Task.fs:276-279` already normalizes `SourceIds` (`ToUpperInvariant |> distinct |> sort`). Adding
two more sources to that union is a **strict widening**: every previously valid document still parses,
and an explicit `sourceIds:` is unioned rather than ignored, so no document changes meaning
destructively. `schemaVersion` stays `1`.

Observable consequences, both intended:

- More tasks become visible to `evidence`/`verify` — the bug being fixed.
- `allTaskDispositionIds` is unchanged for every existing file: it already computed the same union
  (`:717-724`), so its output set is identical by construction. FR-022 is satisfied *by definition*,
  and the test is a guard, not a discovery.

### R3b — Why the emitter must stop writing the redundant line (FR-018)

With `SourceIds` derived, an emitted `sourceIds:` that restates `requirements:` + `decisions:` is pure
noise, and it re-introduces the two-fields-one-fact confusion on the next author's screen. The emitter
writes only the residual: ids in `SourceIds` that are *not* recoverable from the typed fields.

Idempotence check: emit residual → parse (union re-derives full `SourceIds`) → emit residual again →
identical. Holds because the residual is a pure function of the parsed model.

### R3c — `deriveGuidanceModel` is the mirror-image bug

`WorkModel.fs:879`: `let relatedIds = (task.Requirements @ task.Decisions) |> List.distinct |> List.sort`.

Once `SourceIds` is the derived superset, `relatedIds = task.SourceIds` is both simpler and strictly
more correct — an id reachable only via an explicit `sourceIds:` (e.g. `SB-002`, a scope boundary)
currently never reaches agent guidance. This **moves generated bytes** in
`readiness/<id>/agent-commands/**`; goldens are re-baselined, and the diff is reviewed as a
deliverable (FR-024), not waved through.

## R4 — The atomic write

`CommandEffects.fs:312-321`:

```fsharp
| WriteFile(path, text, kind) ->
    let existing = snapshotIfExists projectRoot path
    if canOverwrite kind existing text then
        if not dryRun then
            let absolute = fullPath projectRoot path
            Directory.CreateDirectory(parentDirectory absolute) |> ignore
            File.WriteAllText(absolute, text)      // truncate-then-write
        success effect existing
    else
        failure effect existing (unsafeOverwrite path)
```

`File.WriteAllText` opens with `FileMode.Create` — the file is truncated to zero and then filled. Any
reader between those two operations sees a prefix (commonly empty, sometimes partial). This is the
"transient thin/boilerplate `FR-001`-only state" of Audio §3.9.

**Every authored artifact and generated view flows through this one case.** `spec.md` is not special;
one fix covers all of them.

### R4a — `File.Replace` vs. temp + `File.Move(overwrite)`

| | `File.Replace` | `File.Move(src, dst, overwrite = true)` |
|---|---|---|
| Destination must exist | **yes** — throws `FileNotFoundException` on first write | no |
| Atomic same-volume rename | yes | yes (`rename(2)` / `MoveFileEx` + `MOVEFILE_REPLACE_EXISTING`) |
| Needs a backup path | yes (or `null`) | no |

`WriteFile` must handle create-and-replace uniformly, so `File.Replace`'s precondition disqualifies it.
**Chosen: temp sibling + `File.Move(temp, absolute, overwrite = true)`.**

The temp file is a sibling (same directory ⇒ same volume ⇒ `rename` is atomic) named
`.<name>.<guid>.tmp`. `try/finally` deletes it on any failure so a crashed write leaves no residue.

### R4b — Determinism and the temp name

The temp name contains a GUID, which is nondeterministic — but it never survives the call and never
appears in any report, digest, or artifact. Determinism contracts observe committed bytes only. The
leading `.` keeps it out of `readiness/**` / `work/**` globs even in the crash window.

### R4c — What must not change

`snapshotIfExists` and `canOverwrite` run **before** the write and are untouched, so
`ArtifactOperation.NoChange` (identical content ⇒ no write at all) and `unsafeOverwrite` (refusing to
clobber an authored file) keep their exact semantics. `dryRun` still short-circuits before any I/O —
including the temp write.

### R4d — Test posture

There is **no `CommandEffectsTests.fs`**; the interpreter is only exercised transitively
(`CommandWorkflowTests`, `SurfaceCommandTests:141-147`, `LifecycleSmokeTests`). This feature adds the
first direct test, which means `tests/FS.GG.SDD.Commands.Tests/FS.GG.SDD.Commands.Tests.fsproj` gains a
compile entry. That file is in #164's declared `Paths:` and **not** in FS.GG.SDD#177's (verified: #177
extends `FoundationTests.fs`/`ShipCommandTests.fs` rather than adding a file). Re-check `overlap`
before touching it if #177 widens.

Atomicity itself is not directly observable from a single-threaded test. The tractable, meaningful
assertions are:

1. **Fault injection** — make the commit step throw (read-only destination), assert the destination's
   prior bytes survive and no `.tmp` sibling remains. This is the property that actually protects the
   author.
2. **Structural** — no `File.WriteAllText` call remains on a destination path in `CommandEffects.fs`.
3. **Behavioral non-regression** — `NoChange`, `unsafeOverwrite`, `dryRun`, create-new all unchanged.

Asserting "no observer ever sees a prefix" would need a concurrent reader racing the writer: flaky, and
it would test `rename(2)`, not our code. Rejected.

## R5 — The clarify title

`EarlyStageAuthoring.fs:1107`, inside `clarificationTemplate request workId (specFacts: SpecificationFacts) answers`:

```fsharp
let title = requestTitle request workId
```

`requestTitle` (`:61-65`) is `request.Title |> Option.filter (not << isNullOrWhiteSpace) |> Option.map _.Trim() |> Option.defaultValue (titleFromWorkId workId)`.

`specFacts` is already a parameter and `SpecificationFacts.FrontMatter.Title: string` exists
(`Specification.fsi:15`). The fix is one expression, inserting the spec's title as the middle rung:

```fsharp
let title =
    request.Title
    |> Option.filter (String.IsNullOrWhiteSpace >> not)
    |> Option.map _.Trim()
    |> Option.orElseWith (fun () ->
        specFacts.FrontMatter.Title
        |> Option.ofObj
        |> Option.filter (String.IsNullOrWhiteSpace >> not)
        |> Option.map _.Trim())
    |> Option.defaultValue (titleFromWorkId workId)
```

`--title` still wins; a blank/absent front-matter title still falls back to the humanized work id.

The same shape appears at `ChecklistPlanAuthoring.fs:203`, `:917` and `TaskGraphAuthoring.fs:511`,
each of which also holds the relevant facts. **Out of scope** unless a test proves the same symptom —
widening a papercut fix into a sweep is how papercuts become epics. Noted for follow-up.

`fs-gg-sdd-clarify`'s skill body does not state where the front-matter `title:` comes from, so no
skill edit and no `skill-manifest.json` / `registry/skills.yml` sha reconcile.
