# Phase 1 Data Model: CLI Version Coherence in Scaffold Provenance

Feature: `052-cli-version-coherence` · Date: 2026-07-01

Only **additive** changes to existing entities plus one small shared value type. No entity is
removed or reshaped; every change is backward-compatible.

---

## E1 — `ScaffoldProvenanceRecord` (extended, additive)

`src/FS.GG.SDD.Artifacts/ScaffoldProvenance.fs(.fsi)` — schema **stays v1**.

| Field | Type | Change | Notes |
|-------|------|--------|-------|
| `SchemaVersion` | `int` | unchanged | literal `1` |
| `Generator` | `GeneratorVersion` | unchanged | **authoritative "CLI version used"** (D1); `{ Id; Version }` |
| `RequiredMinimumCliVersion` | `string option` | **NEW (additive)** | provider-declared minimum coherent `fsgg-sdd` version; `None` when the provider declares none or declares a malformed one (D2/D6) |
| `ProviderName` | `string` | unchanged | |
| `ProviderContractVersion` | `string` | unchanged | |
| `TemplateRef` | `string` | unchanged | |
| `Outcome` | `string` | unchanged | |
| `ProducedPaths` | `ScaffoldProducedPath list` | unchanged | |
| `EffectiveParameters` | `(string * string) list` | unchanged | |

**Serialization** (`serialize`): emit `requiredMinimumCliVersion` **immediately after** the
`generator` object, always present as **string-or-null** (`Some v` → string; `None` →
`WriteNull`). This places the two US1 facts side by side and keeps the JSON shape stable.
Deterministic ordering and sorting of other fields unchanged.

**Parse** (`tryParse`): read `requiredMinimumCliVersion` as an optional string; **absent or
JSON null → `None`** (back-compat for records written before this field, mirroring the
`effectiveParameters` default at `ScaffoldProvenance.fs:102-110`). No new failure mode — a
missing field never makes `tryParse` return `None` for the whole record.

**Validation rules**:
- `None` is a legitimate recorded value (provider declared no/malformed minimum) — never
  fabricated into a fake version (Edge Cases, D2/D6).
- The field is never written with a value that failed `Fsgg.Version.tryParse` (D6).

---

## E2 — `Fsgg.Provider.ProviderDescriptor` (extended, additive)

`src/FS.GG.Contracts/Provider.fs(.fsi)` — the "additive superset" descriptor gains one field.

| Field | Type | Change |
|-------|------|--------|
| existing 10 fields | — | unchanged |
| `MinimumCliVersion` | `string option` | **NEW (additive)** — the raw, value-agnostic `minimumCliVersion` scalar from the registry entry; `None` when absent |

**Parse** (`Config.parseProviderRegistry`, `Config.fs:194-205`): add
`MinimumCliVersion = tryScalarAt [ "minimumCliVersion" ] mapping`. Absent key → `None`. The
four currently-required scalars (`name`/`contractVersion`/`templateId`/`source`) are unchanged;
the new field is optional and never affects whether an entry is dropped.

**Validation rules**: SDD stores the raw string verbatim (never interprets or normalizes it
here); parsing/validity is determined only at comparison time by `Fsgg.Version` (D3/D6).

---

## E3 — `Fsgg.Version` (NEW shared value type + module)

`src/FS.GG.Contracts/Version.fs(.fsi)` — one repo-wide version grammar (D3).

```fsharp
type Version = { Major: int; Minor: int; Patch: int }

val tryParse: text: string -> Version option        // major.minor.patch, same grammar as Registry
val compare: left: string -> right: string -> int option
//   Some -1 | Some 0 | Some 1  when both parse; None when EITHER side is unparseable
```

- Grammar identical to the existing private `Registry` `SemVer` engine (`Registry.fs:73-89`);
  `Registry`'s private helpers are refactored to delegate here so there is one grammar.
- `compare` returns `None` (not an exception, not a false ordering) when a side is unparseable
  — the honest-degradation contract (D6/D7).

---

## E4 — `ScaffoldSummary` (extended, additive)

`src/FS.GG.SDD.Commands/CommandTypes.fs(.fsi)` — report projection of the new fact (D9).

| Field | Type | Change |
|-------|------|--------|
| existing fields | — | unchanged |
| `RequiredMinimumCliVersion` | `string option` | **NEW** — mirrors E1 so all three projections can surface it |

The installed CLI version is already available on the report via existing machinery
(`generator`), so no new field is needed for it here.

---

## E5 — Diagnostics (NEW codes)

`src/FS.GG.SDD.Artifacts/Diagnostics.fs` — two new `scaffold.*` constructors, following the
existing scaffold-diagnostic shape (`scaffoldRef` artifact, `create` helper).

| Code | Severity | When | Blocking? | Exit impact |
|------|----------|------|-----------|-------------|
| `scaffold.cliBehindMinimum` | `DiagnosticInfo` | installed `< min` (`compare = Some -1`), D4/D5 | No | None |
| `scaffold.providerMinimumMalformed` | `DiagnosticWarning` | `minimumCliVersion` present but unparseable, D6 | No | None |

Both are non-blocking (`hasBlocking` keys only on `DiagnosticError`), so FR-005 / SC-004 hold
by construction. `scaffold.cliBehindMinimum`'s `Message` states installed + minimum + gap;
its `Correction` names the re-seed remedy (D8).

---

## E6 — `NextAction` branch (NEW, reuses existing type)

`src/FS.GG.SDD.Commands/CommandReports.fs` — no type change; a new resolver branch keyed on
`scaffold.cliBehindMinimum` (after the blocking branch, alongside the `earlyStageGuidance`
branch pattern at `CommandReports.fs:1191-1213`):

```
ActionId            = "reseedSeededSkills"
Command             = Some Init          // upgrade CLI, then re-run init to re-seed (D8)
WorkId              = request.WorkId
Reason              = "<installed> is behind <min>; upgrade fsgg-sdd and re-run
                       `fsgg-sdd init` to re-seed the fs-gg-sdd-* skills and
                       .fsgg/early-stage-guidance.md (refresh does not re-seed)."
RequiredArtifacts   = [ ".claude/skills/…"; ".codex/skills/…"; ".fsgg/early-stage-guidance.md" ]
BlockingDiagnosticIds = []               // empty ⇒ non-blocking pointer
```

---

## Relationships & data flow (all pure until the single existing write)

```
.fsgg/providers.yml ──parse──▶ ProviderDescriptor.MinimumCliVersion (E2, raw string option)
request.GeneratorVersion.Version ─────────────────────────────────┐  (installed CLI, D1/D7)
                                                                   ▼
                         cliCoherenceDiagnostics (pure, D11) uses Fsgg.Version.compare (E3)
                                   │                    │
                     Diagnostic list (E5)      resolved requiredMinimumCliVersion : string option
                                   │                    │
             merged into model.Diagnostics       threaded into provenanceWriteEffect
                    │                                    │
        NextAction branch (E6) + all 3 projections   ScaffoldProvenanceRecord.RequiredMinimumCliVersion (E1)
                                                         │
                                          WriteFile(.fsgg/scaffold-provenance.json)  [existing effect]
```

No new `Effect` / edge I/O is introduced (D11).
