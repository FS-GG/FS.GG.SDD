# Data model: Skill-manifest contract types

All types live in `Fsgg.Schemas` (`src/FS.GG.Contracts/Schemas.fs` + `.fsi`), BCL-only.

## SkillScope

```fsharp
type SkillScope =
    | Process   // an SDD lifecycle process skill (fs-gg-sdd-*)
    | Product   // a provider product skill (e.g. fs-gg-* UI skills)
```

The `scope: process|product` of ADR-0014 §Decision 1/4. A producer ships only `Product` skills to a
scaffolded product (the P2/P4 product-boundary guard); `Process` skills are SDD-seeded.

## SkillManifestEntry

```fsharp
type SkillManifestEntry =
    { Id: string                     // skill name (e.g. "fs-gg-sdd-plan")
      Scope: SkillScope
      Sha256: string                 // digest of the canonical body
      Body: string option            // the canonical body, inline …
      ResolvablePath: string option } // … or a resolvable in-package path to it
```

Exactly one of `Body`/`ResolvablePath` carries the body in practice; both are optional in the type
so either form is expressible. The "exactly one" resolution rule is P1 library policy, not a shape
constraint here.

## SkillManifest

```fsharp
type SkillManifest =
    { SchemaVersion: int
      Skills: SkillManifestEntry list }
```

The per-producer declarative contract the fan-out reads (replacing directory scans / per-source
`template.json` strings). Versioned via `skillManifestVersion`.

## agentSkillRoots (AGENT_SKILL_ROOTS)

```fsharp
let agentSkillRoots: string list = [ ".claude"; ".codex"; ".agents" ]
```

The single declared root-set constant (ADR-0014 §Decision 5). Consumers append `skills/`. Adding or
renaming a runtime root is a one-line change here.

## Version constant + registry entry

```fsharp
let skillManifestVersion = 1
// entries gains:
{ Name = "skill-manifest"; SchemaVersion = skillManifestVersion; ContractVersion = None; Owner = Sdd }
```

`entries` count 10 → 11.

## ScaffoldProducedPathEntry / ScaffoldProducedPath — additive Sha256

Contract type (`Schemas.fs`) and runtime record (`ScaffoldProvenance.fs`) both gain:

```fsharp
      Sha256: string option   // per-skill/per-path content digest; None ⇒ not recorded
```

- Serialize: omit the `sha256` key when `None` (byte-identical to today); emit `"sha256": "<hex>"`
  when `Some`.
- Parse: read optional `sha256`; absent or blank ⇒ `None`.
- `scaffoldProvenanceVersion` stays `1`; the cross-repo `scaffold-provenance` **contract** version
  bumps `1.0.0 → 1.1.0` (additive minor) in the `.github` registry.

## Version-of-truth deltas

| Surface | From | To |
|---|---|---|
| `Fsgg.ContractVersion` (`.fs` + `.fsproj`) | `1.2.0` | `1.3.0` |
| `.github` registry `scaffold-provenance.version` | `1.0.0` | `1.1.0` |
| `Schemas.scaffoldProvenanceVersion` (in-code) | `1` | `1` (unchanged) |
