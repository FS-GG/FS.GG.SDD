# Phase 0 Research: Early-Stage Authoring Seeds

**Feature**: 089-early-stage-authoring-seeds
**Date**: 2026-07-08
**Issue**: FS-GG/FS.GG.SDD#174 (blocks FS-GG/FS.GG.Templates#118)

Every finding below was **verified against the running CLI** in a throwaway workspace, not
inferred from reading source. The transcript of that verification is reproduced in
`quickstart.md` as the baseline the tests must invert.

---

## D1 — The seed's derivation inputs (`specify`, §WD7)

**Decision**: Derive the seeded story and acceptance scenario from **`intent.UserValue`** as the
primary fact, with the invocation title as secondary context. Do not attempt to read the charter.

**Rationale**: `requestTitle` (`EarlyStageAuthoring.fs:61`) resolves to
`request.Title |> Option.defaultValue (titleFromWorkId workId)` — this invocation's `--title`,
falling back to the humanized work id. It never reads `work/<id>/charter.md`.

Verified: `charter --work demo --title "Export a session recording as a video file"` followed by
`specify --work demo` (no `--title`) produced a spec titled **`Demo`**. The charter title was not
carried. Any design that "derives the seed from the work title" therefore degrades to deriving it
from the work id, which is meaningless.

`intent.UserValue` is a *required* intent fact — `normalizeSpecificationIntent`
(`EarlyStageAuthoring.fs:195`) lists `user value` among the missing facts that block `specify` —
so it is always present and always non-blank when the seed renders. It is the only reliable
feature-describing fact.

**Alternatives considered**:
- *Read the charter title.* Rejected: `specificationTemplate` has no access to charter text, and
  plumbing it through changes a signature for cosmetic gain. `specify`'s callers pass `--title`
  when they have one.
- *Ask for a story.* Rejected: making `story` a required intent fact is a breaking CLI change and
  is not what the issue asks for.

## D2 — Which fallbacks are reachable (`specify`, §WD7)

**Decision**: Reshape only the **story** and **acceptance-scenario** fallbacks. Leave the scope,
requirement, and non-goal fallbacks alone.

**Rationale**: `normalizeSpecificationIntent` reports `user value`, `scope`, and
`measurable requirement` as required-missing, and `specificationDiagnosticsTextAndSummary`
(`:557`) turns any missing fact into a blocking `missingSpecificationIntent` before the template
renders. So the `scope` fallback (`"Author one chartered SDD work item specification."`) and the
`requirements` fallback (`"Create a specification artifact with stable ids."`) are **dead code** on
every reachable path. Reshaping them would be churn with no observable effect. The non-goal
fallback is reachable but is a genuine default, not a statement about the SDD process.

Verified: the reproduction in D-Baseline below supplied only value/scope/requirement and rendered
the meta story and meta acceptance scenario, with the author's requirement text used verbatim.

## D3 — Blocked runs write nothing, by design (`clarify`, §WD5)

**Decision**: Emitting the skeleton requires a **narrow, named carve-out** in the shared handler
shell's effect gate — not a change to what text `clarify` computes.

**Rationale**: The issue attributes the missing file to
`clarificationDiagnosticsTextAndSummary` returning `None` for the text at
`EarlyStageAuthoring.fs:1187`. That is not the operative cause. `runHandler`
(`Prerequisites.fs:139`) ends with:

```fsharp
let effects = if hasBlocking then [] else writeEffects @ generatedEffects
```

**Every** write effect is discarded whenever any diagnostic is a `DiagnosticError` — the "H-4
not-blocking effect gate". So even the sibling blocked path at `:1194`, which *does* return
`Some text`, writes nothing.

Verified twice. (a) `clarify` with no answers → `outcome: blocked`, no `work/demo/clarifications.md`,
no `readiness/` directory at all. (b) `clarify` with a `still open` answer for one ambiguity —
which returns `Some text` and blocks via `unresolvedBlockingAmbiguity` — also wrote **no file**.
"Blocked ⇒ zero writes" is a repo-wide invariant that `doctor` (strictly read-only), `upgrade`
(zero writes without `--yes`), and `scaffold` (never reports an incomplete scaffold as complete)
all lean on.

**Chosen carve-out**: extend the handler shell's continuation with a fifth channel —
`blockedSeedEffects` — that survives `hasBlocking`, and gate the change so exactly one handler
(`clarify`) ever returns a non-empty value for it:

```fsharp
let effects =
    if hasBlocking then blockedSeedEffects   // H-4 carve-out (feature 089): seed-on-blocked
    else writeEffects @ generatedEffects
```

Keep the existing `runHandler` as a thin wrapper that supplies `[]`, so the other eight handlers
are untouched and the carve-out lives at exactly one reviewable line. Crucially the carve-out
returns *only* the seed write — `generatedEffects` stay dropped, so a blocked `clarify` still
writes no `work-model.json`.

**Alternatives considered**:
- *Downgrade `missingClarificationAnswer` to a warning.* Rejected outright: it would unblock the
  stage, letting `checklist` run against unanswered ambiguities. FR-010 forbids it.
- *Bypass `runHandler` for `clarify`.* Rejected: duplicates the missing-WorkId guard, the
  diagnostics sort, and the `hasBlocking` computation the shell exists to single-source.
- *Gate on the write kind instead of a channel* (e.g. "a new `Seed` `ArtifactWriteKind` survives
  blocking"). Rejected: `writeKindValue` is serialized into the `changedArtifacts` JSON automation
  contract, so a new case is an observable vocabulary change (FR-016), and it would let any handler
  opt in by accident.

## D4 — The skeleton must be truthful, and the naive skeleton is a trap

**Decision**: The skeleton reports `status: needsAnswers` and lists every unanswered declared
ambiguity as blocking (FR-007/FR-008) — **and** `clarify` must retire a Remaining Ambiguity line
once its ambiguity acquires a decision or accepted deferral (FR-018).

**Rationale, part 1 (truthfulness)**: `clarificationTemplate` (`:1007`) computes
`status = if answers |> List.exists (fun a -> a.Kind = "stillOpen") then "needsAnswers" else "clarified"`,
and `renderRemainingLine` emits a line only for `stillOpen` answers. With **zero** answers — exactly
the blocked case — that yields `status: clarified` and `"No blocking ambiguity remains."` Emitting
it verbatim would write a file asserting the work is clarified while the command blocks for
unanswered ambiguities.

**Rationale, part 2 (the trap)**: a skeleton that *does* list its ambiguities as blocking cannot be
resolved by re-running `clarify`. The existing-file path (`:1250`) calls
`appendClarificationAnswers`, which only ever **appends** to Remaining Ambiguity
(`appendToSection`); no code path retires a line.

Verified end-to-end. With a hand-placed skeleton exactly as specified, running
`clarify --input "AMB-001: Use the MP4 container / AMB-002: Cap recordings at ten minutes"`:

```
outcome: succeeded          <-- clarify claims success
changedArtifacts: 2
remainingAmbiguities: 2
blockingAmbiguities: 2      <-- ...while two ambiguities still block
```

The decisions were appended (`DEC-001`, `DEC-002`) but both
`- AMB-00N [CQ-00N] blocking: …` lines survived. The next stage then fails:

```
$ fsgg-sdd checklist --work demo
outcome: blocked
why: Blocking ambiguity remains unresolved after clarification planning.
rc=1
```

So the naive fix trades "the operator hand-authors the file" for "the operator must know to delete
lines from a section, after a command told them it succeeded" — strictly worse, because the failure
is now silent at the stage that caused it and loud two stages later.

**Verified sufficient**: clearing the two resolved lines (replacing the section body with the
`No blocking ambiguity remains.` sentinel) makes `clarify` report `blockingAmbiguities: 0` and
`checklist` succeed with `next: fsgg-sdd plan`. FR-018 is therefore both necessary and sufficient.

FR-018 also repairs this latent defect for hand-authored artifacts, which is the only way such a
file can exist today (D3: the tool never writes one).

**Retirement rule** (minimal, non-clobbering): remove a Remaining Ambiguity line **iff** its
`AMB-###` id now carries a concrete decision or accepted deferral. Lines for still-unresolved
ambiguities — including operator-authored prose — are left untouched (FR-014). If the section is
left with no entry, insert the `No blocking ambiguity remains.` sentinel, which
`isNoOutstandingSentinel` (`Clarification.fs:274`) already exempts from the blocking count.

**Alternative considered**: re-derive the whole Remaining Ambiguity section with
`replaceSectionBody` (the precedent used for machine-generated checklist sections). Rejected: it
would clobber operator-authored explanations for still-open ambiguities, violating FR-014.

## D5 — The skeleton's remaining-ambiguity text

**Decision**: Render an unanswered ambiguity's Remaining Ambiguity line with a generic, derived
explanation — mirroring `renderQuestionLine`'s existing generic phrasing — not the ambiguity's
prose.

**Rationale**: `SpecificationFacts` (`Specification.fsi:35`) carries `AmbiguityIds: AmbiguityId list`
— **ids only**. The ambiguity's authored text is not parsed out, and adding it would change a public
`.fsi` (a Tier 1 change under the constitution) for a placeholder line. The existing
`renderQuestionLine` already emits generic text (`"Resolve source ambiguity AMB-001 before checklist."`),
so a generic remaining line is consistent with the artifact's own house style.

Preserve today's behavior where it exists: when a `stillOpen` **answer** is present, keep using that
answer's text (current `renderRemainingLine` output). Only fall back to the generic explanation for
an ambiguity with no answer at all — the new skeleton case. This makes the new rendering a strict
superset of the old, so no existing golden output moves for the `stillOpen` path.

## D6 — Empty-state placeholders survive alongside real entries

**Decision**: Retire a section's empty-state placeholder when `clarify` appends a real entry to it
(FR-019).

**Rationale**: Observed during D4 verification. After appending two decisions, the Decisions section
read:

```markdown
## Decisions
No concrete decisions recorded.

- DEC-001 [CQ-001] [AMB:AMB-001]: Use the MP4 container
- DEC-002 [CQ-002] [AMB:AMB-002]: Cap recordings at ten minutes
```

Harmless to the parser (`parseClarificationDecisionsInSection` keys off decision ids) but a visible
wart. Today it is nearly unreachable because the tool never writes a placeholder-bearing file that
later gains entries (D3). **This feature makes it the common path**: every skeleton ships three
empty-state placeholders, and the operator's first `clarify --input` fills them. So the wart becomes
load-bearing for the artifact this feature exists to deliver, and is in scope.

## D7 — Write kind and no-clobber posture for the seed

**Decision**: Write the seed as `WriteFile(clarificationPath workId, text, AuthoredSource)` — the
same kind `clarify` uses today — and plan the effect **only** when the file is absent.

**Rationale**: `canOverwrite` (`CommandEffects.fs:46`) treats `AuthoredSource` as overwrite-allowed,
so the kind alone does not enforce FR-011. But the seeding branch is reached only from
`match snapshot path model with | None ->`, i.e. the file provably does not exist. No-clobber is
guaranteed by construction, and the `changedArtifacts` write-kind vocabulary is unchanged (FR-016).

**Alternative considered**: use the no-clobber `AgentGuidanceTarget` kind. Rejected: it would
satisfy FR-011 by refusal rather than by construction, it emits an `unsafeOverwrite` diagnostic on a
present file (a behavior change), and its name misdescribes a clarification artifact.

## D8 — Report and outcome invariance

**Decision**: The seed changes `changedArtifacts` and nothing else on the report.

**Rationale**: `ReportAssembly.outcome` (`ReportAssembly.fs:14`) is diagnostics-first — any
`DiagnosticError` yields `CommandOutcome.Blocked` regardless of `changes` — so adding a change entry
cannot flip a blocked run to succeeded. `ChangedArtifacts` is derived from `InterpretedEffects` via
`changeFromEffectResult`, so the one `WriteFile` becomes exactly one entry.

To keep the rest of the report byte-stable, thread the skeleton text through a **separate** channel
from the `clarificationText` that feeds `generatedViewPlan`. If the skeleton were passed as
`clarificationText`, the blocked run's `GeneratedViewState` (which is reported even though its write
effect is dropped) would change content. Keep `clarificationDiagnosticsTextAndSummary`'s existing
`text` result exactly as it is today and add a fourth `seedText` result used only by the carve-out.

## D9 — The skeleton's explanation text is parsed, not just displayed (found by running it)

**Decision**: The generated Remaining Ambiguity explanation must contain neither `defer` nor
`non-blocking` (FR-021). Settled wording:
`- AMB-001 [CQ-001] blocking: Unanswered. Resolve source ambiguity AMB-001 before checklist.`

**Rationale**: `parseRemainingAmbiguity` (`Clarification.fs:277`) classifies a line by scanning its
*prose*:

```fsharp
if lowered.Contains("accepted deferral") || lowered.Contains("deferred") then "acceptedDeferral"
elif lowered.Contains("non-blocking") then "nonBlocking"
else "blocking"
```

(and `answerKindValue` uses the even broader `lowered.Contains("defer")`).

The first implementation rendered the obvious, helpful explanation — *"Unanswered — provide a
concrete decision, an accepted deferral, or an explicit still-open note."* — which **names** the
resolutions as options. It therefore classified as `acceptedDeferral`. Consequences, observed
against the built CLI:

- the seeded skeleton parsed with `BlockingAmbiguityCount: 0`;
- `clarify --text` over the skeleton reported `blockingAmbiguities: 0`;
- **`checklist` succeeded (rc=0) with both ambiguities unanswered** — precisely the gate the
  skeleton exists to hold shut.

So the skeleton was untruthful in the *opposite* direction from D4: instead of over-blocking it
under-blocked, and it silently disabled the ambiguity gate for every work item that received one.

After rewording, verified: the skeleton parses with two blocking entries, `clarify` reports
`blockingAmbiguities: 2`, and `checklist` against the raw skeleton blocks with
`"Blocking ambiguity remains unresolved after clarification planning."` (rc=1).

**Lesson recorded in the contract** (K9): this artifact's prose *is* machine input. Any generated
sentence placed under `Remaining Ambiguity` must be checked against the classifier's keyword set.

## D10 — A partially answered `clarify` persists nothing (pre-existing; unchanged)

**Decision**: Leave it. Document it. Do not extend the carve-out to cover it.

**Rationale**: verified against the built CLI — `clarify --input` answering one of two declared
ambiguities blocks with `missingClarificationAnswer` for the other and leaves the artifact
byte-identical (`changedArtifacts: 0`). Two independent mechanisms produce this: the existing-file
path sets `proposedText = existing.Text` whenever `answerDiagnostics` is non-empty, and the H-4 gate
would drop the write regardless.

This is correct behavior — it never half-writes an operator's artifact — and it is unchanged by this
feature. The operator answers every declared ambiguity in one invocation, or edits the skeleton
directly. Extending the seed carve-out to the existing-file path would mean a blocked run
overwriting an operator-owned file, which FR-011/FR-014 forbid.

An earlier draft of the spec asserted that a partial run "records the answers given"; that was
inference, not observation, and has been corrected.

## D11 — Stale `status` after resolution (FR-020)

**Decision**: When nothing remains blocking, rewrite `status: needsAnswers` → `status: clarified`,
scoped to the leading front-matter block and to that exact value.

**Rationale**: `clarify`'s existing-file path never touches front matter. Before this feature that
was harmless: the tool only ever wrote a whole artifact in one shot with a consistent status. Once a
blocked run seeds `status: needsAnswers`, a later fully-answered run would leave the artifact saying
`needsAnswers` beside `No blocking ambiguity remains.` — reintroducing exactly the self-contradiction
FR-007/FR-008 exist to prevent.

No consumer reads clarification `FrontMatter.Status` (verified: `checklist`, `plan`, and `tasks` gate
on *their own* status fields), so this is a truthfulness fix, not a gate fix.

Scoped so the command corrects only its **own** bookkeeping: it rewrites the literal value it writes
(`needsAnswers`), never an operator's chosen status, and never a `status:` line in the document body.

## D12 — Defects found by code review of the first implementation (all reproduced, then fixed)

Six real defects in 089's own first cut. Each was reproduced against the built CLI before being
fixed, and each now has a regression test. They are recorded because five of them are *classes* of
mistake this artifact invites.

1. **Retirement keyed off any id the line mentions, not the line's subject.** An operator's
   `- AMB-001 blocking: Cannot decide until the AMB-002 question is settled.` was deleted the moment
   AMB-002 was answered — destroying the explanation of a still-blocking item (FR-014). Fixed by
   matching the line's *anchor* id (its first `AMB-###`), exactly as `parseRemainingAmbiguity`
   classifies it. The gate never actually opened (an unanswered ambiguity always re-appends its own
   line), so the harm was content loss, not a bypass.
2. **`retireStaleSentinel` composed before the append instead of after.** Retiring first is a no-op —
   there is no non-sentinel line yet to trigger the drop — so a new blocking line landed beside
   `No blocking ambiguity remains.` Contract K3 forbids exactly this.
3. **`neutralizeIds` was case-sensitive** (`[A-Z]{2,3}`) while every id scanner in the artifact layer
   uses `RegexOptions.IgnoreCase`. A lowercase `amb-001` in the author's `value:` survived into the
   seeded `US-001`/`AC-001` lines, and the specification parser counted it: `unresolvedAmbiguityCount:
   3` on a spec whose `## Ambiguities` said *"No material ambiguities recorded."* Fixed with
   `IgnoreCase` over the known id families, and by neutralizing **before** decapitalizing (which could
   itself turn `Amb-001` into `amb-001`).
4. **`transformSectionBody` stripped every blank line from a section it rewrote — including on no-op
   passes.** `retirePlaceholders` runs over all four sections on every invocation, so an operator's
   paragraph breaks (and blank lines inside a fenced block) were reflowed on every `clarify --input`
   (FR-014). Fixed: blanks ride through the transform untouched, and a transform that keeps every line
   leaves the section byte-identical.
5. **Retirement could not reach a question-id-only line.** `parseRemainingAmbiguity` counts
   `- CQ-001 blocking: …` as blocking, but retirement only matched `AMB-###`. Fixed with a question-id
   fallback.
6. **`List.forall isSentinelLine` is vacuously true on an empty list.** An absent or hand-emptied
   Remaining Ambiguity section was read as proof that everything was resolved, flipping `status` to
   `clarified` on no evidence. Fixed with a non-empty guard.

**The through-line**: defects 1, 3, 5 and (in a different guise) D9 are all the same mistake — treating
this artifact's *prose* as display text when the parsers read it as data, and matching ids loosely
where the parser matches them precisely. Any code that generates or edits a lifecycle artifact must be
written against the parser that will read it back.

---

## D-Baseline — Reproduction transcript (pre-change)

`fsgg-sdd` built from `main` @ `66545c9`, run in a scratch workspace.

**§WD7 — meta seed** (`specify` with value/scope/requirement, no story, no acceptance):

```markdown
## User Stories
- US-001 (P1): As a maintainer, I can specify Demo after chartering the work item.

## Acceptance Scenarios
- AC-001 [US-001] [FR-001]: Given a chartered work item, when specify runs with intent, then spec.md is created with stable ids.

## Functional Requirements
- FR-001: The exported file plays back in a standard media player (Stories: US-001; Acceptance: AC-001)
```

Note the title `Demo` (D1) and the author's requirement text used verbatim (D2).

**§WD5 — blocked clarify writes nothing**:

```
$ fsgg-sdd clarify --work demo --text
lifecycle: 3/10 clarify (blocked) · work=demo · outcome=blocked
why: Clarification input is missing answers for blocking ambiguity: AMB-001, AMB-002.

$ ls work/demo/    →  charter.md  spec.md          (no clarifications.md)
$ ls readiness/    →  (no such directory)
```

**The trap** (D4): hand-placed skeleton + `clarify` answering both ambiguities →
`outcome: succeeded`, `blockingAmbiguities: 2`; then `checklist` → `outcome: blocked`, rc=1.

**The fix, confirmed sufficient**: clearing the two resolved lines → `clarify`
`blockingAmbiguities: 0`; `checklist` → `outcome: succeeded`, `next: fsgg-sdd plan`.

---

## E1 — Local build environment (not a repo defect; do not "fix")

`dotnet build` fails locally with `NU1403: Package content hash validation failed for
FSharp.Core.10.1.301`. The `packages.lock.json` hash is `FwQFuq…`; the bytes this sandbox downloads
from `api.nuget.org` hash to `excLf2…`. CI's `gate` workflow restores in `--locked-mode` and is
**green on `main` @ 66545c9**, so the committed lock file is correct and this sandbox's network is
serving different FSharp.Core bytes.

**Do not run `dotnet restore --force-evaluate`** and do not commit a regenerated lock file — that
would silently re-pin a supply-chain artifact to locally-observed bytes.

Local builds and tests must instead bypass lock validation without touching the tracked files:

```sh
dotnet build FS.GG.SDD.sln \
  -p:RestorePackagesWithLockFile=false \
  -p:NuGetLockFilePath=<scratch>/nolock.json
```

Verified: builds clean, `git status` shows `packages.lock.json` untouched. CI remains the authority
on restore integrity.
