# Implementation Plan: The Driver Skill Class, Known But Not Yet Enforced

**Spec**: `specs/106-driver-skill-class/spec.md` · **Item**: FS.GG.SDD#591 · **Contract**:
`skill-registry` (ADR-0015, ADR-0017, ADR-0037, ADR-0054)

## Architecture

The entire change lives in one place — the pure validator's private scope allow-list — because the
model already carries the driver shape:

| Layer | Project | Adds | Why here |
|---|---|---|---|
| Model + pure validator | `src/FS.GG.Contracts` (`Fsgg.Registry`) | `driver` added to the private `skillScopes` set; the `UnknownComponent` message enumerates all three | BCL-only leaf, pure, no I/O (Constitution V). |

Nothing else moves:

- **No `.fsi` change.** `SkillRegistryEntry.Scope`/`.Owner` are already `string`, and `MaterializesWhen`
  already `string option`. The public surface is unchanged, so neither the reflection
  `PublicSurface.baseline` nor the `docs/api-surface/FS.GG.Contracts/Registry.fsi` text baseline moves.
- **No load-edge change.** `SkillRegistryDocument.load` parses `scope`/`owner`/`materializes-when` as
  free strings; a `driver` row already reaches the validator intact.
- **No CLI change.** `registry validate` dispatches on document *kind* (a `skills:` root), not on scope.

### The one edit that carries the feature

```fsharp
// before
let private skillScopes = Set.ofList [ "process"; "product" ]
// after
let private skillScopes = Set.ofList [ "process"; "product"; "driver" ]
```

plus the `UnknownComponent` message, `… (expected 'process', 'product', or 'driver').`

That `owner: .github` and a composed `materializes-when` need no code is the *design*, not an omission:
feature 104 chose to check `owner` is non-blank and `materializes-when` is present-and-non-blank, and to
assert **nothing** about their content ("This validator checks the predicate is PRESENT and non-blank,
never what it MEANS" — `Registry.fsi`). A `.github` owner and an `X and Y` predicate are ordinary
non-blank strings under those rules. Step 1 does not tighten them; it pins that they pass.

### Why known-but-not-enforced, and why it is safe (ADR-0037 §3)

Adding a value to an *accept* set can only make the validator accept more. It cannot reject a document it
accepted before, so it cannot break `.github` HEAD — the exact property step 1 must preserve while the
`skills.yml` `schemaVersion` stays at 1 with the `mirrored` bump owed. Enforcement (a `driver` row *must*
have a composed predicate / `.github` owner) is a *tightening* and is deliberately deferred to step 2,
where it lands against the bumped `schemaVersion`.

## Verification plan

The failure legs are the point — a gate that cannot fail is this repo's recurring defect class (#266).
Each row is **mutation-checked**: disable the change, the named test goes red.

| # | Test | Pins | Mutation that reddens it |
|---|---|---|---|
| 1 | `scope: driver` → `Valid` | AC-001 / FR-001 | drop `"driver"` from `skillScopes` |
| 2 | `driver` row + `owner: .github` + composed `materializes-when` → `Valid` | AC-002 / FR-003 | as #1, or tighten owner/predicate rules |
| 3 | unknown scope → `UnknownComponent`, message names `driver` | AC-003 / FR-004 | drop the message enumeration |
| 4 | `process` / `product` still `Valid` (existing Theory, +`driver`) | AC-004 / FR-002 | any non-monotone edit to `skillScopes` |

Driven, not just asserted: the PR body records the real `fsgg-sdd registry validate` verdict against a
`driver`-row document **and** against the real `registry/skills.yml` (must stay `valid` — the monotonicity
witness at the CLI, FR-002).

## Release (item scope, second PR)

Per the user-approved plan and heron's confirmation on #591: once this merges, a **single** CLI release
bumps `Directory.Build.local.props` `0.16.0 → 0.17.0` and pushes `v0.17.0`. That one publish ships both
the feature-104-plus-#589 wire-contract validator (already on `main`) **and** this driver scope,
unblocking both `.github` flips (ADR-0052 and ADR-0054) with no double publish. The `mirrored` 1→2 bump
stays owed and is **not** paid here.
