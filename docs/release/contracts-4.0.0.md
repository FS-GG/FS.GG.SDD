---
title: FS.GG.Contracts 4.0.0 — break declaration for the wire-contract dimension
category: SDD
categoryindex: 6
index: 27
description: The declared binary break that forces FS.GG.Contracts to 4.0.0 — why `ContractEntry` gains a `WireContract` field, why adding a field to a public record has no additive spelling, and the consumer adaptation (there is none).
---

# FS.GG.Contracts 4.0.0

`FS.GG.Contracts` moves `3.0.0` → **`4.0.0`**. This note is the **declaration** of the
break. It is declared, not suppressed: there is no `CompatibilitySuppressions.xml`, and
there will not be one.

The bump follows the
[FS.GG.Contracts version-bump checklist](contracts-version-bump-checklist.md) — source,
feed, and the `.github` registry advance as one coordinated change.

> **The version line.** As with [2.0.0](contracts-2.0.0.md) and
> [3.0.0](contracts-3.0.0.md), `FS.GG.Contracts` is not on the `FS.GG.SDD.*` product line:
> it carries its own `<Version>` and its own SemVer, so the
> [versioning policy](versioning-policy.md)'s `docs/release/migrations/<version>.md`
> obligation does not reach this bump, and this note deliberately does not live there.

## The breaking change

`Fsgg.Registry.ContractEntry` gains a **new field**, `WireContract`:

```fsharp
// 3.0.0
type ContractEntry =
    { Id: string
      Version: string
      Owner: string
      Surface: string
      Consumers: ConsumerDeclaration
      PackageVersion: string option
      Range: string option }

// 4.0.0
type ContractEntry =
    { Id: string
      Version: string
      Owner: string
      Surface: string
      Consumers: ConsumerDeclaration
      WireContract: WireContractDeclaration   // new
      PackageVersion: string option
      Range: string option }

/// One of three PROVENANCES a wire contract can have (ADR-0052).
type WireContract =
    | VendoredProto of upstream: string * upstreamVersion: string
    | OwnedProto of proto: string
    | CodeFirstProtobufNet of surface: string

/// The three-state read: absent is NOT a declaration, and a typo is NEITHER.
type WireContractDeclaration =
    | WireUnspecified
    | WireDeclared of WireContract
    | WireMalformed of raw: string
```

Per this repo's own change-class table
([checklist](contracts-version-bump-checklist.md)) — *"add a field to a public record →
breaking → major, break declaration required"* — this is a major, and the note you are
reading is that requirement discharged.

### There is no additive spelling of this change

Worth stating explicitly, because it reads like an addition. An F# record generates a
**positional primary constructor**, so *every* new field — added at the end, in the
middle, anywhere — changes its arity and **deletes the old constructor**. That is the
first row of the change-class table, and it is the one that
[shipped 2.0.0](contracts-2.0.0.md) and again forced [3.0.0](contracts-3.0.0.md). There
is no additive way to add a field to a public F# record: a parallel carrier record
(`RegistryDocument` gaining a `WireContracts` list) is a new field on a public record too,
so it is *also* a major, and it is also two sources of truth for one fact.

The two new union types (`WireContract`, `WireContractDeclaration`) are, on their own,
**additive** — new types are binary-compatible. It is the record field that forces the
major. So adding the field directly is the honest option, not the expensive one.

## Why a major was spent on this

A networked component's compatibility surface is often not its source `.fsi` at all but the
**wire bytes it exchanges** — the protobuf/gRPC surface — and that surface is
compatible-or-not on rules (`.proto` field numbers, `reserved` ranges, an independently
versioned vendored upstream) that the source-`.fsi` `Surface` field cannot express. The
schema had **no place to record it**, so `.github`'s `registry/dependencies.yml` could not
carry the wire contract of a networked component at all.

That is a real, filed blocker: **FS.GG.Net** exposes two wire contracts — StarCraft II's
Blizzard-owned `s2clientprotocol` (a vendored external `.proto`) and a BAR contract — and
under [ADR-0052](https://github.com/FS-GG/.github/blob/main/docs/adr/0052-wire-contracts-are-a-registry-dimension.md)
those must be recordable in the registry. The only moves left without a schema change were
to **misfile the wire contract as a source `Surface`** (a lie: the `.fsi` path is not the
compatibility surface, and the vendored upstream has its own version the source line cannot
hold) or to **leave it unrecorded** (which is how the wire dimension stays invisible to
every downstream reader). See
[FS.GG.SDD#589](https://github.com/FS-GG/FS.GG.SDD/issues/589).

### Why three provenances, and why a closed union

The wire contract's **provenance** decides *which artifact is the compatibility surface*,
and the three are genuinely different facts, each carrying only what its provenance needs:

1. **Vendored external `.proto`** — FS-GG does not own it (e.g. `s2clientprotocol`). The
   vendored upstream ref says *which* upstream the bytes match; its **own version**, pinned
   independently of the component's `Version`, is load-bearing because upstream moves on its
   own cadence — tying the two together would force a component bump on every upstream tag.
   It is validated *as a version* (the same SemVer grammar `version` / `package-version` are
   held to), because it is one.
2. **Owned `.proto`** — FS-GG owns the wire contract; the file's field-number / `reserved`
   discipline **is** the compatibility surface, so the owned `.proto` path must be named.
3. **Code-first protobuf-net** — there is **no `.proto` artifact**; the F#
   `[<ProtoContract>]` types **are** the wire contract, so the type surface that carries the
   field numbers must be named.

The union is **closed**, so an unrecognised provenance is unrepresentable as a declared
value and must be rejected at the parse edge as `WireMalformed` — never silently constructed
as a fourth kind, and never guessed into one of the three.

### Why the declaration is three-state, and what makes it safe

`WireContract` is modelled exactly like [`consumers`](contracts-3.0.0.md) and
`MirrorDeclaration`: a `WireContractDeclaration` with three states, not the
`WireContract option` the two-state instinct suggests.

- **`WireUnspecified`** — no `wire-contract:` key at all. **NOT a fault:** most contracts
  are not networked, so this is the common case and validates clean, exactly as an absent
  `range` does.
- **`WireDeclared`** — the owner declared a well-formed provenance. The pure validator then
  checks the fields that provenance makes load-bearing (a blank `upstream`, a non-SemVer
  `upstream-version`, a blank `proto`/`surface`) — this edge classifies, it does not judge
  completeness.
- **`WireMalformed`** — present but unparseable (an unknown/blank provenance, or a value
  that is not a mapping). Carried with its raw text so `validateDocument` **reports** it
  rather than collapsing it into either neighbour.

An `option` has nowhere to put the malformed case: it would collapse into `None` — a phantom
"no wire contract" that tells the author they forgot a line that is right there — or be
guessed into a `Some`, a phantom declaration. Absent and a declared provenance are
**different claims**, and a typo is a **third**; the union is what keeps the collapse
unrepresentable.

## Who is actually affected

**Measured, not assumed** (both declared consumers of `fsgg-contracts`, unchanged from
[3.0.0](contracts-3.0.0.md)):

| repo | references `FS.GG.Contracts` | uses `Fsgg.Registry` | affected |
|---|---|---|---|
| **FS.GG.Governance** | yes (`Fsgg.Schemas`) | **no** | no |
| **FS.GG.Templates** | no | **no** | no |

Governance's own `ContractEntry` is an **unrelated domain type** in its
`specs/007-routing-severity-modes/contracts/Route.fsi`, not `Fsgg.Registry.ContractEntry`.
Neither consumer touches the extended record.

So — exactly as with [2.0.0](contracts-2.0.0.md) and [3.0.0](contracts-3.0.0.md) — **for a
consumer already on 3.0.0 this is a version-number change and no source edit.**

That does not make it a minor. The record surface broke; the number says so. A version that
understates its API is the defect [2.1.0](contracts-2.1.0.md) exists to correct, and
"nobody happens to use it" is not a change class.

## Consumer adaptation

**For Governance and Templates: none.** Advance the pin; there is nothing to edit.

**For any future consumer constructing or reading a `ContractEntry`:**

```fsharp
// Constructing — before
{ Id = "alpha"; …; Consumers = ConsumersDeclared [ "templates" ]; … }

// Constructing — after: the new field is required (there is no default)
{ Id = "alpha"; …; Consumers = ConsumersDeclared [ "templates" ]
  WireContract = WireUnspecified; … }   // most contracts have no wire dimension

// Reading — the three states are the point; do not flatten them back
match entry.WireContract with
| WireUnspecified -> // no wire dimension — NOT "malformed", NOT a fault
| WireMalformed raw -> // present but unparseable — report it, don't guess
| WireDeclared (VendoredProto (upstream, version)) -> …
| WireDeclared (OwnedProto proto) -> …
| WireDeclared (CodeFirstProtobufNet surface) -> …
```

> **Do not reintroduce the collapse in the consumer.** A
> `match … with WireDeclared w -> Some w | _ -> None` shim compiles and restores the
> two-state ergonomics, and it re-creates the exact absent-vs-typo merge this dimension was
> designed to prevent — one level out. If you only need the declared contract, say so about
> the state you are in; do not default the other two to "none".

## The gate that saw it, and the two that could not

This break is a sharper demonstration of the detectors and their blind spots than
[3.0.0](contracts-3.0.0.md) was, and the difference is the whole lesson. 3.0.0 **retyped**
a member, so the `.fsi`-text detector saw a *changed* member and called it `breaking`. This
bump **adds** a field, and a field addition removes and changes *nothing* in the text — so
**both** `.fsi`-based detectors read it as pure growth and are blind to the break. Only the
assembly-level detector, which sees the record's generated constructor, catches it
([checklist](contracts-version-bump-checklist.md), *"The two detectors, and the class each
one is blind to"*):

| detector | verdict on this change | caught it? |
|---|---|---|
| **reflection `PublicSurface.baseline`** | **additions only** — it captures member *names*, so the two new union types and the new `WireContract` member all read as additions. | no |
| **`surface --check`** (`.fsi` text diff) | **`additive` → `minor`** — the classifier lists the new field and types under `addedMembers` with an empty `removedOrChangedMembers`, because a field addition subtracts no text. It **under-classifies**: a record-field addition is a *binary* break its text diff cannot see. | no |
| **`scripts/apicompat-check.sh`** (ApiCompat / Package Validation, built assembly vs the feed baseline) | **`CP0002`** — *"Member `Fsgg.Registry.ContractEntry.ContractEntry(string, string, string, string, ConsumerDeclaration, …)` exists on `[Baseline]` but not on …"*: the old 7-arg positional constructor is gone. **BREAK → forces the major.** | **yes** |

**This is the row of the checklist's detector table that this bump exists to illustrate.**
For a *retype*, `surface --check` is enough. For a *field addition*, `surface --check` says
`additive (minor)` and is wrong about the class — the same blindness the reflection baseline
has — and the **only** gate that catches it is the required `api-compatibility-gate`, which
compares built assemblies and sees the constructor the `.fsi` text does not spell out. Read
the `surface --check` `minor` as "the text grew"; read the `major` off ApiCompat and this
note.

The two `.fsi` baselines are still re-pinned here
(`docs/api-surface/FS.GG.Contracts/Registry.fsi` and
`tests/FS.GG.Contracts.Tests/PublicSurface.baseline`) so `surface --check` and the reflection
test go green — but greening them is *not* the break declaration. This note is.

> **Why the required gate can be green on the introducing PR at all.** ApiCompat baselines
> against **whatever is latest on the feed**, so it reports a break only in the window between
> *introduced* and *published*. Under publish-before-flip (below), `FS.GG.Contracts` 4.0.0 is
> pushed to the feed **before** this PR's gate settles; the gate then compares 4.0.0 source
> against a published 4.0.0 and reports `OK (compatible with 4.0.0)`. Run locally against the
> still-current 3.0.0 baseline it correctly reports the `CP0002` above — that is the break,
> shown in the one window it is visible.

## Sequencing

`Fsgg.Registry` lives here and the registry document lives in `.github`, so this lands as
**two ordered PRs across two repos** — no PR spans both
([ADR-0037](https://github.com/FS-GG/.github/blob/main/docs/adr/0037-schema-growth-is-publish-before-flip.md)
over [ADR-0015 §3](https://github.com/FS-GG/.github/blob/main/docs/adr/0015-cross-repo-contracts.md)):

1. **This repo (publish-before-flip, FR-007):** land the new field + validator, publish
   `FS.GG.Contracts` 4.0.0 and a CLI carrying it to the org feed. The
   `fsgg-sdd registry validate` command already dispatches on document shape, so it
   understands the wire-contract dimension the moment it carries this `Fsgg.Registry`.
2. **`.github`:** advance the `fsgg-contracts` row + the validator pin, bump
   `registry-schema.version` + `schemaVersion` in `registry/dependencies.yml`, and record
   FS.GG.Net's SC2/BAR wire contracts.

**The validator half rejects nothing the current document does.** The wire-contract dimension
is **optional**: an absent `wire-contract:` is `WireUnspecified` and validates clean, so every
contract entry that exists today — none of which declares a `wire-contract:` — is unaffected.
The new diagnostics fire only on a `wire-contract:` that is *present*, so ADR-0037's *"the new
rule may reject nothing the current document does"* is satisfied without a `schemaVersion` gate
on the validator itself.
