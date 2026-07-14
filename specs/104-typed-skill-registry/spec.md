# Feature Specification: A Typed Assertion Over the Skill Registry, Where Absent Is Not False

**Feature Branch**: `item/420-cross-repo-teach-fsgg-registry-the-skill`

**Created**: 2026-07-14

**Status**: Draft

**Input**: FS.GG.SDD#420 — "teach `Fsgg.Registry` the skills.yml `mirrored` field — an OPTIONAL
boolean whose ABSENT must never coerce to `false`". Filed from `FS-GG/.github` as the SDD half of the
decision taken in `.github#686`. Contract: `skill-registry` (ADR-0015, ADR-0017, ADR-0022 §6).

## Overview

`FS-GG/.github` `registry/skills.yml` is the org's authoritative skill catalog. Its `skill-registry`
contract row names **`Fsgg.Registry` (shipped in the `FS.GG.SDD.Cli` tool) as the typed validator that
owns it**, and `.github#686` decided that the optional additive `mirrored:` field **is** schema growth —
so a `schemaVersion` 1 → 2 bump is owed.

That bump is blocked on this repo, and `.github#686` says why:

> The validator is `Fsgg.Registry`, and it lives in FS.GG.SDD. It cannot be taught from `.github` … and
> it is worse than that: `Fsgg.Registry` **does not assert over this file at all** yet. Bumping a version
> whose validator cannot know the field would be a claim with nothing behind it.

That is exactly the state of the code. `Fsgg.Registry` models `registry/dependencies.yml` and only that.
Driving the shipped CLI against the real catalog shows it plainly:

```
$ fsgg-sdd registry validate registry/skills.yml --text
registry validate: registry/skills.yml → invalid (2 diagnostics)
  - MissingField [<root>]: Registry document has no 'repos'.
  - MissingField [<root>]: Registry document has no 'contracts'.
                                                                          exit 1
```

It parses the skill catalog as a *dependency* document, finds neither of that document's required
sections, and calls the org's own registry invalid. It never sees the `skills:` array at all — so there
is no field for `mirrored` to be absent *from*.

So this feature is not "add a field to an existing model". It is the assertion itself: **a typed
skill-registry document, a pure validator over it, and a CLI that reaches it** — with the three-state
`mirrored` rule got right at the point the model is first written down, rather than retrofitted onto a
model that already coerced it.

### The fail-open this exists to close, and why a `bool` cannot express it

`mirrored: true` asserts an **obligation the owner declares**: *ADR-0022 §6 requires FS.GG.Rendering to
ship a byte-identical copy of this body.* It is not an observation that a same-named file exists in two
trees.

Three states are therefore real, and only three states are honest:

| `mirrored:` | means |
|---|---|
| `true` | the owner asserts the mirror obligation |
| `false` | the owner **considered** this body and asserts there is **no** obligation |
| *absent* | the question **has not been answered** for this body |

`absent` and `false` are different claims, and collapsing them is a fail-open with teeth. A
jq-shaped `select(.mirrored == true)` reads an absent key as false, because `null == true` is false —
so a catalog predating the field answers *"not mirrored"* for **every** body, confidently, and every
real mirror obligation goes unguarded. That is precisely the hole `.github#658` was opened to close, and
a validator that coerced `absent → false` would re-open it *underneath the gate meant to catch it*.

The repo already contains the coercion in question — `Internal.boolAt keys node defaultValue`, whose
final arm is `| _ -> defaultValue`. It maps an absent key **and** an unparseable value onto the caller's
default. Reaching for it here would be the bug. Today's 33 undeclared rows are not asserting "no
obligation"; they are rows nobody has classified.

So this feature models the answer as a three-case union rather than a `bool`, and the collapse becomes
**unrepresentable** rather than merely discouraged.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - The org catalog validates at all (Priority: P1)

As `.github`'s contract-coherence gate, I run `fsgg-sdd registry validate registry/skills.yml` and get a
verdict **about the skill catalog** — not a complaint that it lacks `repos:`. A coherent catalog is
`valid`, exit 0.

### User Story 2 - An absent `mirrored` is not `false` (Priority: P1)

As the owner of a body nobody has classified, I want the validator to carry my row as **unanswered**. A
row with no `mirrored:` key must not round-trip as `mirrored: false`, and must not be reported as
asserting the absence of an obligation.

### User Story 3 - An unparseable verdict is a diagnostic, not a shrug (Priority: P1)

As a reviewer, a row whose `mirrored:` is present but **not** a boolean (`mirrored: yes`,
`mirrored: "true"`, an empty value, a list) is a **diagnostic** — an unparseable verdict — never a
silent skip and never quietly re-read as "unanswered".

### User Story 4 - The dependency registry is byte-for-byte unaffected (Priority: P1)

As `.github`'s existing gate, `fsgg-sdd registry validate registry/dependencies.yml` behaves exactly as
it does today. This feature adds a document kind; it changes none of the existing one's verdicts.

### User Story 5 - A declared `false` survives as a declared `false` (Priority: P2)

As the owner of a body I **did** consider and rule out, my `mirrored: false` stays distinguishable from
the 33 rows I never looked at.

## Requirements *(mandatory)*

- **FR-001**: `Fsgg.Registry` MUST model `registry/skills.yml` as a typed document
  (`SkillRegistryDocument`) with `schemaVersion`, `parameters`, and `skills[]` rows carrying
  `{id, scope, owner, source, sha256, mirrored?, materializes-when?}`. (covers AC-001)
- **FR-002**: The `mirrored` field MUST be modelled as a **three-state** value — declared-true,
  declared-false, and *unspecified* — such that "absent" and "false" are **distinct and
  non-interconvertible**. A two-state `bool` with a default is expressly forbidden. (covers AC-002)
- **FR-003**: A `mirrored:` that is **present but not a boolean** MUST reach the validator as a distinct
  malformed state carrying its raw text, and MUST be reported as a diagnostic — never dropped, and never
  reclassified as unspecified. (covers AC-003)
- **FR-004**: `validateSkillRegistry` MUST be a **pure, BCL-only** function over the typed document
  (Constitution V), emitting diagnostics in document order. All I/O (YAML) lives at the
  `FS.GG.SDD.Artifacts` load edge. (covers AC-004)
- **FR-005**: `fsgg-sdd registry validate <path>` MUST dispatch on the document's **shape** — a root
  `skills:` key selects the skill registry; everything else keeps today's dependency-registry behaviour
  unchanged. (covers AC-005, AC-006)
- **FR-006**: The validator MUST report, per row: a missing/blank `id`, a duplicate `id`, a missing or
  unknown `scope` (∉ {`process`, `product`}), a missing `owner`, a missing `source`, a missing or
  malformed `sha256` (not 64 lowercase hex), and a malformed `mirrored`. (covers AC-007)
- **FR-007**: A load/parse failure MUST stay distinct from a content diagnostic (Constitution VIII) —
  surfaced as a single `MalformedDocument`-class diagnostic, never a crash and never a cascade.
  (covers AC-008)

## Acceptance Criteria

- **AC-001**: The real `registry/skills.yml` (41 rows, 8 carrying `mirrored`) loads into
  `SkillRegistryDocument` and validates **`valid`, exit 0**.
- **AC-002**: A row with **no** `mirrored:` key does **not** round-trip as `false`; it is `Unspecified`,
  and `Unspecified <> Declared false`.
- **AC-003**: `mirrored: yes` (and an empty / non-scalar value) yields a `MalformedField "mirrored"`
  diagnostic naming the row — not a skip, not `Unspecified`.
- **AC-004**: `validateSkillRegistry` references no I/O type and is callable from the BCL-only Contracts
  leaf.
- **AC-005**: `registry validate` on the real `dependencies.yml` remains **valid, exit 0** — byte-for-byte
  today's behaviour.
- **AC-006**: `registry validate` on the real `skills.yml` no longer reports `no 'repos'` / `no 'contracts'`.
- **AC-007**: Each rule in FR-006 has a test that goes red when its arm is disabled.
- **AC-008**: A malformed YAML file yields one `MalformedDocument` diagnostic and exit 1, not an exception.

## Out of Scope

- **Replacing `.github`'s `scripts/fsgg-skill-registry-check`.** That 1194-line Python does what a pure
  BCL validator structurally **cannot**: it hashes producer bodies, walks Rendering's tree to test the
  mirror obligation, and reconciles rows from producer manifests. This feature is the *document-schema*
  tier only — the same tier `validateDocument` occupies for `dependencies.yml`. The two are complementary,
  and this spec claims no authority over content the other verifies.
- **The `schemaVersion` 1 → 2 bump and the `skill-registry` contract `version` bump.** Those are
  `.github`'s files and `.github`'s PR, ordered *after* a CLI carrying this ships (`.github#686` step 4).
- **Flipping `coherence: skill-registry-published` → true.** `.github`-side, and downstream of the bump.
- **Emitting `mirrored` from SDD's own producer manifest.** SDD's 16 process rows carry no ADR-0022 §6
  mirror obligation, so they correctly declare nothing. This feature *reads* the field; it does not make
  SDD assert one.
