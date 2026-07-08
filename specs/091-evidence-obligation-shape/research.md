# Phase 0 Research: Slim the Evidence Declaration Shape

**Feature**: `091-evidence-obligation-shape` | **Date**: 2026-07-08

## R1 — Is omitting an always-null key a schema-version-bumping contract change?

**Decision: No. `schemaVersion` stays put.**

FS.GG.SDD#165 asserts, in bold, that "the obligation schema is a **consumed contract** — slimming
the on-disk shape is a versioned-schema change, not a serializer tweak," and makes that the reason
it is a standalone contract-change issue. That assertion does not survive contact with the reader.

Evidence, from `src/FS.GG.SDD.Artifacts/LifecycleArtifacts/Internal.fs`:

```fsharp
let rec tryScalarNonNullAt keys (node: YamlNode) =
    match keys with
    | [] ->
        match node with
        | :? YamlScalarNode as scalar when not (isPlainNullScalar scalar) ->
            Some(Option.ofObj scalar.Value |> Option.defaultValue "")
        | _ -> None
    | key :: rest ->
        node |> tryMapping |> Option.bind (tryChild key) |> Option.bind (tryScalarNonNullAt rest)
```

`tryChild key` returns `None` when the key is **absent**. `isPlainNullScalar` returns `true` — hence
the outer result is `None` — when the key is present with a plain `null`, `Null`, `NULL`, `~`, or
empty value. The two inputs are indistinguishable in the output model.

`syntheticDisclosure` is read by `parseSyntheticDisclosure`, which requires *both*
`tryScalarAt ["syntheticDisclosure"; "standsInFor"]` and `["syntheticDisclosure"; "reason"]` to be
`Some` and non-whitespace. An absent `syntheticDisclosure` mapping fails `tryChild` and yields
`None`; a `syntheticDisclosure: null` also yields `None`. Same conclusion.

Therefore:

- **Forward compatibility**: a slim file is a strict subset of the input language the current
  reader accepts. Nothing new must be understood.
- **Backward compatibility**: a verbose file (older CLI, or hand-authored) still parses to the
  identical model. Nothing is dropped.

A schema version exists to signal "the reader must change." No reader must change. Bumping it would
be cargo cult — it would force every consumer to re-pin for a change that cannot affect them.

**Alternative considered**: bump `schemaVersion` anyway, "to be safe." Rejected. A version bump is
not free: it is a coordination event across FS-GG consumers, and a bump with no semantic delta
teaches consumers that bumps are noise. Worse, it would imply the old form is *unsupported*, which
is exactly backwards — the old form remains readable forever.

## R2 — Is there an external consumer of the on-disk obligation shape?

**Decision: None found; no cross-repo coordination.**

Checked `FS-GG/.github` `registry/`: it contains `dependencies.yml`, `repos.yml`, and `skills.yml`.
There is no `contracts.yml`/`compatibility.yml`, and therefore no registered contract row for the
`evidence.yml` obligation shape to coordinate against.

Checked the `fs-gg-sdd-evidence` process skill (pinned in `registry/skills.yml` at
`sha256: b79c867a…`). **Correction to an earlier draft of this document, which claimed the skill does
not mention these fields — it does.** `.claude/skills/fs-gg-sdd-evidence/SKILL.md` documents
`rationale`, `owner`, `scope`, and `laterLifecycleVisibility` as the four fields a `kind: deferral`
declaration must carry, and `tests/FS.GG.SDD.Commands.Tests/RequiredFieldContractTests.fs`
*enforces* that the skill lists every key in `RequiredKeys.requiredDeferralKeys`, backtick-wrapped,
in registry order. Do not delete those bullets on the strength of this feature.

The conclusion is nonetheless unchanged, for a different reason: this feature edits **no** skill
body. The four fields are still documented, still gate-required, and still rendered whenever they
hold a value. The pinned `sha256` stays valid and no ADR-0017 skill-manifest reconcile is triggered.

Governance's effective-evidence freshness reads parsed values, not raw keys. Parsed values are
unchanged.

**Alternative considered**: open a Coordination issue anyway, per the issue's instruction. Rejected
as ceremony with no counterparty — there is no registry row and no consumer whose behavior changes.
The finding is recorded here instead, which is where a future reader will look.

## R3 — Which fields are in scope?

**Decision: exactly the five named in FS.GG.SDD#165.**

`syntheticDisclosure`, `rationale`, `owner`, `scope`, `laterLifecycleVisibility`.

**Explicitly out of scope: empty inline lists** (`taskRefs: []`, `requirementRefs: []`,
`acceptanceScenarioRefs: []`, `clarificationDecisionRefs: []`, `checklistResultRefs: []`,
`planDecisionRefs: []`, `obligationRefs: []`, `artifacts: []`, `sourceRefs: []`, `notes: []`).

Rationale: an empty list is a *different kind of nothing*. `taskRefs: []` tells the author "this
bucket exists and you may fill it"; it is part of the authoring contract the `fs-gg-sdd-evidence`
skill teaches. The five in-scope fields are inert unless a specific, uncommon situation applies
(a synthetic stand-in, a deferral rationale, an owner override). Removing the discoverable buckets
would trade one ergonomic problem for a worse one: a file that does not show the author what they
may write. `[]` also costs 4 characters on a line that would exist anyway, versus 5 whole lines.

**Alternative considered**: also drop empty lists, per the issue's "~20-line block" framing.
Rejected on the above; and the measured win is already 5 of the ~20 lines, with the ref buckets
carrying real authoring affordance.

### R3a — The affordance argument cuts *against* four of the five fields. Why omit them anyway?

The R3 rationale above ("an empty list tells the author the bucket exists") applies with more force,
not less, to `rationale`/`owner`/`scope`/`laterLifecycleVisibility`. Those four are not decorative:
`RequiredKeys.requiredDeferralKeys` lists them, and `HandlersEvidence` blocks with
`evidence.missingDeferralRationale` when a `kind: deferral` (or `result: deferred`) declaration
leaves any of them `None`. Calling them "fields that say nothing" is wrong for a deferral.

This was surfaced by an adversarial review of this feature's own artifacts, after the writer was
implemented. It is a real objection and the honest resolution is a bound, not a dismissal:

1. **The gate is value-based, not presence-based.** It reads `Option.isNone declaration.Rationale`
   on the parsed model. A `null` line and an absent key are the same `None`. Omission cannot weaken
   the gate.
2. **The gate blocks *before* the write.** A deferral missing any of the four resolves to
   `CommandOutcome.Blocked` with `changedArtifacts: []` — verified on the real CLI. So
   `renderEvidenceDeclaration` is never reached for an under-specified deferral, and the writer can
   never emit one. Both directions are now pinned by tests
   (`evidence writes every gate-required field of a deferral declaration` and
   `evidence blocks an under-specified deferral before the writer can omit its fields`).
3. **The residual cost is real but small and signposted.** A *non-deferral* scaffolded declaration no
   longer displays the four key names as hints for the day an author converts it to a deferral. When
   that day comes, the blocking diagnostic names all four fields explicitly and
   `RemediationPointers` routes the author to `docs/examples/lifecycle-artifacts/evidence.yml`, whose
   `EV003` is a fully populated deferral. The author is told what to add, not left guessing.
4. **The empty ref buckets have no such backstop.** Nothing blocks on an unfilled `taskRefs: []`, so
   the *only* thing telling an author the bucket exists is the line itself. That asymmetry — a
   blocking diagnostic with a correction, versus nothing at all — is what distinguishes the five
   omitted fields from the ten retained ones. The distinction is not "always null" (the issue's
   framing); it is **"absence is diagnosed"** versus **"absence is silent."**

**Alternative considered**: keep the four deferral keys and omit only `syntheticDisclosure`. Rejected
— it saves 1 line of 5, retains 80% of the boilerplate the issue is about, and buys an affordance the
diagnostic already provides. Recorded here so a future reader sees the trade was weighed, not missed.

## R4 — How should omission be implemented without emitting blank lines?

**Decision: optional renderers return `string option`; splice with `List.choose id`.**

The current `renderEvidenceDeclaration` is one interpolated string whose template puts each optional
renderer on its own physical line:

```fsharp
{renderSyntheticDisclosure declaration.SyntheticDisclosure}
{renderOptionalScalar "rationale" declaration.Rationale}
...
    notes: {declaration.Notes |> yamlInlineList}
```

The obvious edit — `| None -> ""` — leaves the newline that separates the template lines, producing
a blank line per omitted field. Five omitted fields would yield five blank lines: no `null` keys,
but malformed-looking YAML and a violation of FR-004. (YamlDotNet would still parse it; a human
reviewer would rightly reject it, and it would break the "no stray blank lines" invariant the rest
of the emitted artifact holds.)

Returning `string option` and building the block with `List.choose id |> String.concat "\n"`, with
the leading newline carried by the *block* rather than the template, makes the empty case contribute
exactly zero characters. `notes:` then follows `synthetic:` directly.

**Alternative considered**: post-process the rendered string to strip blank lines
(`String.split '\n' |> Array.filter (not << String.IsNullOrWhiteSpace)`). Rejected — it would also
strip blank lines that are legitimately part of a multi-line scalar value, and it fixes a
self-inflicted wound instead of not inflicting it.

**Alternative considered**: switch the whole writer to YamlDotNet's serializer with
`DefaultValuesHandling.OmitNull`. Rejected — the hand-rolled writer exists to control exact byte
output (issue #161's idempotence guarantee depends on it), and a serializer swap is a far larger
blast radius than this feature warrants.

## R5 — What is the migration posture?

**Decision: no migration step; a one-time normalization diff.**

The first `evidence` run after upgrade rewrites an existing verbose `evidence.yml` in the slim form.
That diff is the migration. It is:

- **Safe** — the parsed model is identical before and after (R1).
- **Idempotent** — every subsequent run is byte-identical (FR-007, the existing #161 guarantee).
- **Non-blocking** — no diagnostic, no gate, no exit-code change.

**Alternative considered**: leave existing files alone and only slim newly scaffolded ones.
Rejected — the writer has no notion of "file I previously wrote," the re-render path is the same
code, and two coexisting shapes in one repo is worse than one clean diff.

## R6 — Why is `--satisfy` not in this feature?

**Decision: deferred, on a scheduling constraint, not a design one.**

FS.GG.SDD#165 offers three combinable fixes. The terse `fsgg-sdd evidence --satisfy T001=pass` form
requires a new CLI flag, which lands in `src/FS.GG.SDD.Cli/Program.fs`. FS.GG.SDD#163
(`plan`: refresh own upstream snapshot) is in flight and has declared `Program.fs` in its ADR-0021
`Paths:` touch-set. `scripts/fsgg-coord overlap FS.GG.SDD#165 FS.GG.SDD#163` reports OVERLAP on that
file.

Under ADR-0021, overlapping items are sequenced, not parallelized. Slicing this feature to the
schema half — which touches neither `Program.fs` nor anything else #163 or #174 declares — makes it
startable now, and leaves `--satisfy` as a clean follow-up once #163 merges. The slice is recorded
on the issue body so the deferral is visible to whoever picks it up.

The obligation-seeding option (`--from-tests` extension, auto-classify deferrals) is likewise a
follow-up; it is tracked under the parent epic FS.GG.SDD#160.
