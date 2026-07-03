# Research: Surface skillDriftPaths + correct stale report/doc surfaces

## Decision 1 — One `renderText` edit surfaces drift in both text AND rich

**Finding**: The rich renderer does not format the doctor/upgrade summary
field-by-field. `Cli/Rendering.fs:117` builds its "details" table by taking
`FS.GG.SDD.Commands.CommandRendering.renderText report`, splitting on newlines, and
rendering each line as a table row. So every fact the text projection emits also
appears in the rich projection (confirmed by `RemediationProjectionTests`:
`doctorCliAxis: behind` is asserted in the rich output).

**Decision**: Add the `skillDriftPaths` lines only to `CommandRendering.renderText`
— in both the `doctor` block (after `MissingArtifactPaths`) and the `upgrade`
block — mirroring the existing `missingArtifacts` shape:

```fsharp
builder.AppendLine($"doctorSkillDrifts: {List.length doctor.SkillDriftPaths}") |> ignore
doctor.SkillDriftPaths
|> List.sort
|> List.iter (fun path -> builder.AppendLine($"doctorSkillDrift: {path}") |> ignore)
```

and the `upgrade` equivalent (`upgradeSkillDrifts` / `upgradeSkillDrift`). This
satisfies FR-001, FR-002, FR-003 with no rich-renderer change and no duplication.

**Rationale**: Idiomatic simplicity (Principle IV) — reuse the existing shape and
the text→rich derivation. Empty list emits only the `…Drifts: 0` count and no
per-path lines (parity with `missingArtifacts`; FR-001 empty case).

**JSON safety**: `renderText` is the text projection only; the serializer is
untouched, so `doctor`/`upgrade` JSON stays byte-identical (FR-004).

**Alternatives considered**: Add bespoke Spectre widgets for drift in
`Cli/Rendering.fs` — redundant, since the details table already carries the text
facts. Rejected.

## Decision 2 — `projectRoot = "."` is intentional determinism; document, don't change

**Finding**: `CommandRequest.ProjectRoot` is a real field, but the test harness
sets it to an absolute temp dir (`Path.GetTempPath()/fsgg-sdd-<guid>`,
`TestSupport.fs:11`), and **no** golden JSON contains a `projectRoot` other than
`"."`. `buildReport` hardcodes `"."` precisely to decouple the report's
project-root *display* from the request's (absolute, random) root used for file
I/O, keeping JSON reproducible.

**Decision**: Do **not** emit `model.Request.ProjectRoot`. Keep `"."` and add a
comment at the code site (`ReportAssembly.fs`) documenting the determinism
rationale. FR-007 is reclassified: the issue's flag is a false positive.

**Rationale**: Echoing the request root would emit random absolute paths into JSON,
breaking every golden and the deterministic-output principle (II/VIII). Determinism
wins.

## Decision 3 — Full accepted-command set = 16 lifecycle + validate + registry

**Finding**: `parseCommand` accepts the 16 lifecycle tokens (init … upgrade).
`validate` and `registry` are CLI-level peers dispatched in `Program.fs` **before**
`parseCommand` (`:182`, `:187`), so they never reach `unknownCommand` but are still
commands a user can type. `SddCommand` also contains non-command cases
(Json/Text/Rich/AuthoredSource/…), so it is **not** the authoritative command list.

**Decision**: The `unknownCommand` correction (`DiagnosticConstructors.fs`)
enumerates all 18: `init, charter, specify, clarify, checklist, plan, tasks,
analyze, evidence, verify, ship, agents, refresh, scaffold, doctor, upgrade,
validate, registry`. A pin test (`Commands.Tests`) asserts the correction contains
each of these 18 tokens, so adding a command surfaces the omission (SC-003).

**Rationale**: The correction should name everything a user can type. The pin test
is a static expected-set assertion (the command list spans two dispatch layers, so
a fully-derived pin is not worth the machinery — Principle IV).

## Decision 4 — Reseed NextAction gains `.agents/skills`

**Finding**: `NextActionRouting.fs:103` lists `[ ".claude/skills"; ".codex/skills";
".fsgg/early-stage-guidance.md" ] |> List.sort` — omitting the feature-056 neutral
`.agents/skills` root that reseed actually writes.

**Decision**: Add `.agents/skills` to that list (kept sorted). A test asserts the
reseed `NextAction` affected paths include all three skill roots (FR-006/SC-004).

## Cross-cutting — which golden baselines change (FR-012)

Enumerated, reviewed changes only:
- **doctor/upgrade text goldens** — gain `…SkillDrifts:`/`…SkillDrift:` lines when
  drift is present (and a `…SkillDrifts: 0` line otherwise, if the block always
  emits the count — matching `missingArtifacts`, which always emits its count).
- **unknownCommand correction** in any text/json golden that captures it — the
  correction string lengthens.
- **reseed NextAction** goldens — affected-paths list gains `.agents/skills`.

The `doctor`/`upgrade` **JSON** goldens do **not** change (FR-004). Any golden that
shifts for another reason is a bug to fix, not to re-baseline.

**Note on the count line**: `missingArtifacts` always emits `doctorMissingArtifacts:
N` (including `0`). To mirror exactly, `skillDrifts` also always emits its count —
which means doctor/upgrade **text** goldens change even when drift is empty. This is
the enumerated, intended change (FR-012) and is called out so the baseline diff is
expected, not a surprise.
