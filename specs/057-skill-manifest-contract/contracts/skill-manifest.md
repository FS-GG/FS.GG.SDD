# Contract: skill-manifest + AGENT_SKILL_ROOTS + scaffold-provenance sha256

**Owner**: SDD · **Package**: `FS.GG.Contracts` · **Decision**: ADR-0014 · **Issue**:
FS-GG/FS.GG.SDD#60

## skill-manifest (new, SDD-owned)

A per-producer declarative skill set. In-code schema version `skillManifestVersion = 1`; registered
in `Fsgg.Schemas.entries` as `skill-manifest` (owner SDD, no string contract version).

```
SkillManifest        = { schemaVersion: int; skills: SkillManifestEntry[] }
SkillManifestEntry   = { id: string; scope: "process" | "product"; sha256: string;
                         body?: string; resolvablePath?: string }
```

Consumers (P1 library, P2 provider) read the manifest instead of scanning directories or reading
per-source `template.json` strings. Each skill has exactly one canonical body; the roots are copies
of it, addressed by `sha256`.

## AGENT_SKILL_ROOTS (new, SDD-owned constant)

```
agentSkillRoots = [ ".claude"; ".codex"; ".agents" ]
```

The single declared root set. Every fan-out/verify derives its targets from this; skills live under
`<root>/skills/`. Adding/renaming a runtime root is a one-line change.

## scaffold-provenance — additive minor bump 1.0.0 → 1.1.0

Additive optional per-path `sha256`:

```
producedPaths[i] = { path: string; owner: string; sha256?: string }
mirroredPaths[i] = { path: string; owner: string; sha256?: string }
```

- Additive & optional: a provenance file written under `1.0.0` (no `sha256`) parses unchanged.
- Byte-identical today: the current emitter records no digest, so `sha256` is omitted and the JSON
  is unchanged; the field appears only once P1 computes digests.
- In-code `schemaVersion` stays `1` (additive-tolerant, per SDD#32/#49). The **contract** version
  bumps to `1.1.0` in the `.github` registry.

## Compatibility

- **Backward**: a `1.0.0` consumer ignores the unknown `sha256` (additive-tolerant validator).
- **Forward**: a `1.1.0` consumer treats absent `sha256` as "no digest recorded".
- No coherence flag flips in this feature; `skill-mirror-verified` stays `coherent: false` (flips in
  P4).
