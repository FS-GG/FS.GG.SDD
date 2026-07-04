# Implementation Plan: Emit the `fs-gg-sdd-*` process skill-manifest

**Branch**: `072-process-skill-manifest` | **Date**: 2026-07-04 | **Spec**: [spec.md](./spec.md)

## Summary

Greenfield **emission** over pre-existing contract types. SDD gains a committed,
deterministic `skill-manifest` v1 document enumerating the 16 seeded `fs-gg-sdd-*`
process skills (`scope: process`, `sha256` = canonical-body digest,
`materializes-when: always`), regenerable/checkable via a new `registry
skill-manifest` CLI sub-verb, and pinned to the seeded set by a drift guard. No
lifecycle behavior, no persisted-schema, and no Contracts-type change.

## Ground truth (verified against `HEAD`)

- **Types already exist** in `FS.GG.Contracts` (`Fsgg.Schemas`):
  `SkillManifest = { SchemaVersion: int; Skills: SkillManifestEntry list }`,
  `SkillManifestEntry = { Id; Scope: SkillScope; Sha256; Body: string option;
  ResolvablePath: string option }`, `SkillScope = Process | Product`,
  `skillManifestVersion = 1`, `agentSkillRoots = [".claude";".codex";".agents"]`.
  Registered in the schema table as `skill-manifest` v1, owner `Sdd`.
  **No `materializes-when`/`supplied-by` field on the type** (it is ADR-0014's
  `{id,scope,sha256}` core) — see Decision D2.
- **Canonical hasher exists**: `Fsgg.SkillMirror.sha256 : string -> string`
  (`src/FS.GG.Contracts/SkillMirror.fs`) — `CRLF→LF` normalize, UTF-8, lowercase
  hex; the exact algorithm `.github`'s registry declares
  ("byte-equivalent to `sha256sum SKILL.md`"). **Verified**: this reproduces the
  registry's provisional digests exactly (`fs-gg-sdd-analyze` → `5e9e90ca…`,
  `fs-gg-sdd-charter` → `9f066602…`), and `fs-gg-sdd-troubleshooting` → `03c65640…`
  is the 16th (registry-absent) row.
- **Skill set single source**: `SeededSkills.skillNames`
  (`src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs`) — 16 sorted names,
  `fs-gg-sdd-project` excluded. `seededSkills ()` yields `(name, body)` from
  embedded resources `SeededSkill.<name>` (the same bytes `init`/`scaffold` seed).
- **No manifest is emitted anywhere today** — this feature adds the serializer +
  emitter. No `.agents/` tree is checked in; the manifest introduces its first
  file under `.agents/skills/`.
- **CLI seam exists**: `registry` verb dispatches to `RegistryValidate.run`
  (`src/FS.GG.SDD.Cli/Program.fs:188`), sitting outside the lifecycle
  `parseCommand`/`CommandReport` contracts — the home for a sibling sub-verb.
- **Org expectation** (`.github` design `docs/coordination/skill-registry.md`):
  the owning generator "emits it into `.agents/skills/` (mirrored)"; process
  sha256s "come from SDD's manifest once it publishes one".

## Decisions

- **D1 — Committed canonical path: `.agents/skills/skill-manifest.json`** (SDD repo).
  This is the process producer manifest `.github` reconciles from — the analog of
  Rendering's committed `template/skill-manifest/skill-manifest.json`, placed at the
  neutral/mirror-authority root (`providerSourceRoot = ".agents"`) the design doc
  names. Documented as *process-only, all `scope: process`* to distinguish it from a
  scaffolded product's runtime union manifest at the same relative path.
- **D2 — `materializes-when` is a serializer-level constant, not a new type field**
  (Design A). Every process skill is unconditionally `always`; the serializer writes
  `"materializes-when": "always"` for `scope: process`. This keeps `FS.GG.Contracts`
  and `skillManifestVersion` **unchanged** — no apicompat baseline bump (cf. #87),
  no schema growth. Modeling per-entry predicates on the type is deferred until a
  process skill needs a non-`always` condition (YAGNI); the ADR itself treats
  `materializes-when` as additive with `absent ⇒ always`.
- **D3 — Emitted entry shape mirrors Rendering's proven-consumable product manifest**:
  `{ id, scope, sha256, resolvablePath, materializes-when }`, sorted by `id`,
  `schemaVersion: 1`. `resolvablePath = .agents/skills/<id>/SKILL.md`. `body` (None)
  and `supplied-by` are omitted — process skills are single-producer with no
  cross-producer seam, and `supplied-by` is optional in the org shape.
- **D4 — Grammar**: `materializes-when` uses the ADR-0017 canonical grammar; for all
  16 skills the literal value is `always` (no `(`, `&&`, `||`, or quoted tokens —
  the form that broke Rendering#77).
- **D5 — Generate from `seededSkills ()`** (embedded bodies), one hasher, sorted-by-
  `skillNames` order → deterministic bytes. Chain of trust: manifest ← embedded
  bodies ← on-disk `.claude`/`.codex` (already pinned by `SeededSkillsTests`).

## Technical Context

- **Language**: F# on .NET 10, warnings-as-errors (baseline 0/0). Constitution V
  keeps `FS.GG.Contracts` BCL-only/pure → the JSON **serializer lives in
  `FS.GG.SDD.Artifacts`** (the I/O edge), following `ScaffoldProvenance.fs` house
  style (deterministic `Utf8JsonWriter`, field-omit-when-None, LF newlines).
- **New/changed code**:
  1. `src/FS.GG.SDD.Commands/CommandWorkflow/SeededSkills.fs` (or a sibling
     `SkillManifestModel` module) — pure `processSkillManifest : unit -> SkillManifest`
     building one `SkillManifestEntry` per `seededSkills ()` pair
     (`Id=name; Scope=Process; Sha256=SkillMirror.sha256 body; Body=None;
     ResolvablePath=Some ".agents/skills/<name>/SKILL.md"`), ordered by `skillNames`.
  2. `src/FS.GG.SDD.Artifacts/` — new `SkillManifestJson.serialize : SkillManifest ->
     string` emitting the D3 shape with the `materializes-when: always` constant for
     `Process` scope (+ `.fsi`).
  3. `src/FS.GG.SDD.Cli/RegistryValidate.fs`(+`.fsi`) or a new `RegistrySkillManifest`
     module wired at `Program.fs` `registry` dispatch — sub-verb `skill-manifest`:
     `--check` (regenerate in-memory, compare to committed file, non-zero + diff on
     drift), `--write` (write/update the committed file), bare (print JSON to stdout).
     Deterministic; outside the `CommandReport` contract like `registry validate`.
  4. `.agents/skills/skill-manifest.json` — the committed generated artifact.
  5. Docs: short note in `CLAUDE.md`/`AGENTS.md` boundary section (the process
     producer manifest) — kept **byte-identical** (`AgentSurfaceDriftTests`); and a
     one-liner where the skill-manifest contract is described.
- **Drift guard** — new `tests/FS.GG.SDD.*.Tests/ProcessSkillManifestTests.fs`:
  - id set of committed manifest == `SeededSkills.skillNames` (16; troubleshooting in,
    project out);
  - each `sha256` == `SkillMirror.sha256` of the named authored `.claude/skills/<id>/
    SKILL.md` recomputed from disk;
  - every entry `scope == "process"` and `materializes-when == "always"`;
  - `schemaVersion == 1`; deserializes under the `skill-manifest` v1 reader/shape;
  - **staleness**: committed bytes == fresh `serialize (processSkillManifest ())`
    (the `--check` invariant);
  - **grammar**: every `materializes-when` parses canonical + == `always`; no `(`,
    `&&`, `||`, quotes;
  - **determinism**: two serializations byte-identical; entries sorted by `id`; LF.

## Approach

1. **RED-first guard**: write `ProcessSkillManifestTests` against the intended
   committed path + invariants → red (no file, no code).
2. Add the pure `processSkillManifest` builder (Commands) and `SkillManifestJson`
   serializer (Artifacts); unit-test the serializer shape/determinism.
3. Wire the `registry skill-manifest` sub-verb; generate the committed
   `.agents/skills/skill-manifest.json` via `--write`.
4. Green the guard; add the doc notes (byte-identical agent surfaces).
5. **Verify** (real evidence): `dotnet test` full suite; `registry skill-manifest
   --check` exits 0 on the committed file and non-zero after a deliberate body edit
   (RED demo for AC-007); cross-check the 15 shared digests still equal the registry's
   provisional values and troubleshooting is the 16th.
6. **Close the cross-repo loop**: comment on #109 / notify `.github` — manifest
   emitted at `.agents/skills/skill-manifest.json`; 15 digests match provisional
   exactly (clean promote), `fs-gg-sdd-troubleshooting` (`03c65640…`) is a new 16th
   row the registry must add (the 15→16 drift).

## Constitution Check

- **Structured artifacts are the machine contract** (II): the manifest is a machine
  contract, drift-guarded against the authored `SKILL.md` bytes and the seeded set —
  it can never silently contradict them. PASS.
- **Purity / layering** (V): pure model + hash in Contracts/Commands; JSON I/O in
  Artifacts; CLI edge in Cli. `FS.GG.Contracts` untouched (D2). PASS.
- **Claude/Codex parity**: no skill/command behavior change; `CLAUDE.md`/`AGENTS.md`
  kept byte-identical. PASS.
- **Real evidence**: verification is a real generate/`--check` round-trip and real
  digest recomputation from disk, plus the cross-repo digest cross-check — not
  synthetic. PASS.
- **No behavior drift / additive**: no lifecycle `outcome`/counter/JSON change; no
  persisted-schema or `skillManifestVersion` bump; `init`/`scaffold` seeding output
  unchanged; `validate`/golden contracts untouched. PASS.

## Risks

- **Divergent hash algorithm** → registry mismatch. Mitigated: reuse
  `Fsgg.SkillMirror.sha256` (the declared algorithm) and cross-check against the
  registry's provisional values (already verified for 2 skills; all 15 re-checked in
  step 5).
- **Wrong `materializes-when` grammar** (the Rendering#77 trap) → gate reads false.
  Mitigated: constant `always` + an explicit canonical-grammar assertion in the guard.
- **Non-deterministic serialization** → golden churn. Mitigated: sorted entries,
  fixed field order, LF, `Utf8JsonWriter` house style; determinism assertion in guard.
- **Path ambiguity** (producer vs union manifest at `.agents/skills/skill-manifest.json`)
  → resolved by documenting the SDD-repo file as process-only (all `scope: process`)
  and keeping scaffold/runtime union-manifest emission out of scope.
