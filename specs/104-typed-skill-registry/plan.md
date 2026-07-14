# Implementation Plan: A Typed Assertion Over the Skill Registry

**Spec**: `specs/104-typed-skill-registry/spec.md` · **Item**: FS.GG.SDD#420 · **Contract**:
`skill-registry` (ADR-0015, ADR-0017, ADR-0022 §6)

## Architecture

Three layers, mirroring exactly how `registry/dependencies.yml` is already handled — the pattern is
established, and this feature is deliberately the *same shape* rather than a new one.

| Layer | Project | Adds | Why here |
|---|---|---|---|
| Model + pure validator | `src/FS.GG.Contracts` (`Fsgg.Registry`) | `MirrorDeclaration`, `SkillRegistryEntry`, `SkillRegistryDocument`, `validateSkillRegistry`, `MalformedField` rule | BCL-only leaf. Pure, no I/O (Constitution V). |
| YAML load edge | `src/FS.GG.SDD.Artifacts` | `SkillRegistryDocument.load` / `.detectKind` | I/O at the edge, never in the leaf (Constitution V). |
| CLI | `src/FS.GG.SDD.Cli` | `registry validate` dispatches on document kind | The reachability `.github` pins. |

### The one type that carries the whole feature

```fsharp
type MirrorDeclaration =
    | MirrorUnspecified          // no `mirrored:` key — NOT answered. Never `false`.
    | MirrorDeclared of bool     // the owner answered
    | MirrorMalformed of raw: string   // present, unparseable — a diagnostic, not a shrug
```

`bool option` was the issue's suggestion and is *almost* right: it expresses two of the three states but
has nowhere to put a present-but-unparseable value, which would then have to collapse into `None` —
silently re-reading a malformed verdict as "unanswered" and losing FR-003. The union has room for all
three, so **no state has to impersonate another**. That is the entire design.

`absent → false` is not merely discouraged here; there is no `false` for it to become.

### Document-kind dispatch (FR-005)

`registry validate <path>` must keep working for `dependencies.yml` — `.github`'s contract-coherence
gate runs it on every push. So detection is **additive and conservative**: peek the parsed root mapping;
a `skills:` key selects the skill registry, and **everything else** (including a malformed file) takes
today's dependency path and produces today's diagnostics, unchanged.

Detection is on document *shape*, not filename: the path `.github` passes is
`dotgithub/registry/skills.yml`, and a filename rule would be one rename away from silently validating
the wrong schema.

## Verification plan

The failure legs are the point — a gate that cannot fail is the defect class this repo keeps finding
(#266). Every rule below is **mutation-checked**: disable the arm, the named test goes red.

| # | Test | Pins |
|---|---|---|
| 1 | absent `mirrored` is `MirrorUnspecified`, and `MirrorUnspecified <> MirrorDeclared false` | AC-002 / FR-002 |
| 2 | `mirrored: yes` → `MalformedField "mirrored"` naming the row | AC-003 / FR-003 |
| 3 | empty and non-scalar `mirrored` → malformed, **not** `Unspecified` | AC-003 / FR-003 |
| 4 | `mirrored: true` / `false` / `True` round-trip as `MirrorDeclared` | FR-002 |
| 5 | per-row rules: blank id, duplicate id, unknown scope, missing owner/source, malformed sha256 | AC-007 / FR-006 |
| 6 | the **real** `skills.yml` (checked-in fixture, 41 rows) → `valid` | AC-001 |
| 7 | the **real** `dependencies.yml` → `valid`, and its verdicts are unchanged | AC-005 / FR-004 |
| 8 | malformed YAML → one `MalformedDocument`, exit 1, no exception | AC-008 / FR-007 |
| 9 | CLI: `registry validate <skills.yml>` no longer says `no 'repos'` | AC-006 / FR-005 |

**Driven, not just asserted.** #422 landed a fail-open that 1,715 green tests could not see, because the
tests never put the tool in the state the defect needed. So the real CLI is driven against the **real**
org catalog end-to-end and the output recorded in the PR — the unit tests are the regression net, not
the evidence.

## Sequence

1. Spec + plan (this).
2. `Fsgg.Registry` — model + `validateSkillRegistry` + `MalformedField`. Update `PublicSurface.baseline`.
3. `FS.GG.SDD.Artifacts` — `SkillRegistryDocument.load` / `.detectKind`. **Not** `Internal.boolAt`.
4. `FS.GG.SDD.Cli` — dispatch in `RegistryValidate`.
5. Tests (table above) + a checked-in fixture of the real catalog.
6. Drive the real CLI against the real `skills.yml` and `dependencies.yml`; record both.

## Ordering beyond this repo (publish-before-flip, FR-007 of #420)

This PR is **step 1 of two**, and it does not close FS.GG.SDD#420 — the item stays open until a CLI
carrying this is **published**, because `.github` pins a published artifact, not a merge commit.

Step 2 is `.github`'s: bump `skills.yml` `schemaVersion` 1 → 2 and the `skill-registry` contract
`version` 1 → 2, and advance the `contract-coherence.yml` `FS.GG.SDD.Cli` pin to the published version.
That is `.github#701`'s neighbour and cannot be done from here.
