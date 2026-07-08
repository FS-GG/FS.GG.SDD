# Phase 1 Data Model: Lifecycle Authoring Papercuts

Five deltas. Three touch `.fsi` signatures and are therefore `.fsi`-first (Constitution §I, §III).

## D1 — `SpecificationFacts` loses `UnresolvedAmbiguityCount`

```diff
  type SpecificationFacts =
      { FrontMatter: SpecificationFrontMatter
        ...
-       UnresolvedAmbiguityCount: int
        Diagnostics: Diagnostic list }
```

Every read site was report-facing (R1b). Removing the field, rather than only the report key, prevents
a computed-but-unread field surviving — the exact defect FR-015 forbids elsewhere.

**Cascades**: `Specification.fs` (computation + record), `Specification.fsi`, `CommandTypes.fs/.fsi`
(`SpecificationSummary`), `EarlyStageAuthoring.fs:581`, `CommandSerialization.fs:37`,
`CommandRendering.fs:26`, `RichRenderingTests.fs:76`.

**Not touched**: `remainingAmbiguityCount`, `blockingAmbiguityCount`, and every gate reading them.

## D2 — `RemainingAmbiguity.AmbiguityId` becomes `AmbiguityIds`

```diff
  type RemainingAmbiguity =
-     { AmbiguityId: AmbiguityId option
+     { AmbiguityIds: AmbiguityId list
        QuestionId: ClarificationQuestionId option
        State: string
        ... }
```

`parseRemainingAmbiguity` (`Clarification.fs:265`) drops `List.tryHead`.

**Count semantics are unchanged.** `RemainingAmbiguityCount = facts.RemainingAmbiguity.Length` counts
*lines*, and `BlockingAmbiguityCount` filters lines by `State = "blocking"`. Neither counts ids. FR-003
holds by construction.

**Observable effect**: `unresolvedBlockingAmbiguity` (`EarlyStageAuthoring.fs:1543`, `:1674`) currently
does `List.choose (fun item -> item.AmbiguityId |> Option.map _.Value)`; it becomes
`List.collect (fun item -> item.AmbiguityIds |> List.map _.Value)` and now names *every* blocking
ambiguity, not the first per line.

## D3 — `Decision` gains its references

```diff
  type Decision =
      { Id: DecisionId
        Title: string
        Decision: string
+       RequirementRefs: RequirementId list
+       StoryRefs: UserStoryId list
+       AcceptanceRefs: AcceptanceScenarioId list
        Source: SourceArtifact
        SourceLocation: SourceLocation option }
```

`RequirementModel.parseDecisions` already has the matched line in hand; it extracts the three id
families from it (the same `\bFR-\d{3,}\b` / `\bUS-\d{3,}\b` / `\bAC-\d{3,}\b` regexes
`Clarification.fs:160-178` uses), sorted and deduplicated.

This is the parser feeding `WorkItem.decisions` → `work-model.json`'s `DecisionEntry`, so it is the
only path that makes the refs reach the work model (R2, drop 1).

**Cascades**: `RequirementModel.fs/.fsi`, `WorkModel.DecisionEntry` + `.fsi`, `Serialization.writeDecision`
(three new JSON arrays), `WorkModel`'s JSON re-parse.

### `work-model.json` shape (additive)

```diff
  {
    "id": "DEC-003",
    "title": "…",
    "decision": "…",
+   "requirementRefs": ["FR-001", "FR-007"],
+   "storyRefs": [],
+   "acceptanceRefs": ["AC-005"],
    "source": "work/003/clarifications.md",
    "linkedTaskIds": ["T004"]
  }
```

Additive keys on a generated view. `work-model.json` carries no `schemaVersion` gate that enumerates
`DecisionEntry` keys; the digest moves, which FR-024 anticipates.

## D4 — `ClarificationQuestion` / `ClarificationDecisionFact` lose the unread copies

```diff
  type ClarificationQuestion =
      { QuestionId: ClarificationQuestionId
        Prompt: string
        SourceAmbiguityIds: AmbiguityId list
-       RelatedRequirementIds: RequirementId list
-       RelatedStoryIds: UserStoryId list
-       RelatedAcceptanceScenarioIds: AcceptanceScenarioId list
        Blocking: bool
        ... }

  type ClarificationDecisionFact =
      { DecisionId: DecisionId
        ...
        SourceAmbiguityIds: AmbiguityId list
        RelatedRequirementIds: RequirementId list     // KEPT — gains its first read site (D5)
-       RelatedStoryIds: UserStoryId list
-       RelatedAcceptanceScenarioIds: AcceptanceScenarioId list
        SourceLocation: SourceLocation option }
```

FR-015 offers "consumed or removed". Resolution:

- `ClarificationDecisionFact.RelatedRequirementIds` → **consumed** by D5. Kept.
- The other five → **removed**. `ClarificationQuestion`'s three have no consumer even in principle
  (a question's refs are a restatement of its ambiguity's), and the decision's story/acceptance refs
  now travel on `Decision` (D3), where the work model actually reads them.

Removing beats inventing a consumer: a field with a fabricated read site is the same defect wearing a
disguise.

## D5 — `WorkTask.SourceIds` becomes derived; the emitter writes only the residual

`Task.fs` parse:

```diff
- SourceIds =
-     scalarList [ "sourceIds" ] mapping
-     |> List.map (fun value -> value.ToUpperInvariant())
-     |> List.distinct
-     |> List.sort
+ SourceIds =
+     [ scalarList [ "sourceIds" ] mapping
+       requirements |> List.map _.Value
+       decisions |> List.map _.Value ]
+     |> List.concat
+     |> List.map (fun value -> value.ToUpperInvariant())
+     |> List.distinct
+     |> List.sort
```

`TaskGraphAuthoring` emitter — write the **residual** only:

```fsharp
let residualSourceIds (task: WorkTask) =
    let typed =
        (task.Requirements |> List.map _.Value) @ (task.Decisions |> List.map _.Value)
        |> List.map _.ToUpperInvariant() |> Set.ofList
    task.SourceIds |> List.filter (fun id -> not (Set.contains (id.ToUpperInvariant()) typed))
```

`clarificationDecisionTasks` stops passing the `DEC-###` as a `sourceId` (it is already the `decision`),
and passes `decision.RelatedRequirementIds` as `requirements` instead of `[]` (FR-014, and the read
site that discharges FR-015 for D4).

`WorkModel.deriveGuidanceModel:879`: `relatedIds = task.Requirements @ task.Decisions` →
`relatedIds = task.SourceIds` (already the sorted, distinct superset).

### Invariants

| Invariant | Why it holds |
|---|---|
| Every existing `tasks.yml` parses (FR-017) | The union only adds; explicit `sourceIds:` entries are concatenated, never dropped. |
| `allTaskDispositionIds` unchanged (FR-022) | It already computed `SourceIds ∪ Requirements ∪ Decisions` (`:717-724`). The derived `SourceIds` *is* that union, so the set is identical by construction. |
| `schemaVersion` stays `1` (FR-017) | Strict widening of the accepted input language; no document becomes invalid. |
| Emitter idempotence (FR-018) | `residual` is a pure function of the parsed model; `emit → parse → emit` reaches a fixpoint after one step. |
| `evidence` / `verify` see typed refs (FR-021) | They read `SourceIds`, which now contains them. No change at those call sites. |

## D6 — `WriteFile` commits atomically (no type change)

```diff
  | WriteFile(path, text, kind) ->
      let existing = snapshotIfExists projectRoot path
      if canOverwrite kind existing text then
          if not dryRun then
              let absolute = fullPath projectRoot path
              Directory.CreateDirectory(parentDirectory absolute) |> ignore
-             File.WriteAllText(absolute, text)
+             writeFileAtomic absolute text
          success effect existing
      else
          failure effect existing (unsafeOverwrite path)
```

```fsharp
/// Commit `text` to `path` so no reader observes a partial write: fill a sibling temp file,
/// then atomically rename it over the destination. A sibling shares the destination's volume,
/// which is what makes the rename atomic. The temp never survives the call.
let private writeFileAtomic (absolute: string) (text: string) =
    let directory = Path.GetDirectoryName absolute
    let temp = Path.Combine(directory, $".{Path.GetFileName absolute}.{Guid.NewGuid():N}.tmp")
    try
        File.WriteAllText(temp, text)
        File.Move(temp, absolute, overwrite = true)
    finally
        if File.Exists temp then File.Delete temp
```

No signature change (`CommandEffects.fsi` exports only `interpret`/`interpretAll`/`driveToReport`).
`snapshotIfExists`, `canOverwrite`, `dryRun`, and the `NoChange`/`unsafeOverwrite` paths are untouched
(FR-009, FR-010).

## D7 — The clarify title (no type change)

`clarificationTemplate` resolves `explicit --title → specFacts.FrontMatter.Title → humanized workId`.
`SpecificationFrontMatter.Title` is a plain `string` (`Specification.fsi:15`), so the middle rung is
guarded by an `IsNullOrWhiteSpace` filter, matching `requestTitle`'s own treatment of `--title`.
