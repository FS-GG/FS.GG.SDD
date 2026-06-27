# Contract: Leak-invariant scan

The build-enforced guard that keeps Rendering knowledge and `lifecycle`-value semantics out
of generic SDD (FR-007, US3, SC-005). Extends `ScaffoldGuardTests.fs`.

## Scope & inputs

| Component | Scanned surface | Fails when |
|---|---|---|
| **C1 — identifier deny-list** | `src/**/*.{fs,fsi}` **and** the generic-contract test files (`ScaffoldCommandTests.fs`, `ScaffoldProvenanceTests.fs`, `ScaffoldParityTests.fs`) | any provider-specific token (`fs-gg-ui`, `FS.GG.Rendering`, provider name, docs URL) appears, case-insensitive |
| **C2 — scoped lifecycle-value scan** | the **scaffold source path** only — the curated union: `HandlersScaffold.fs` + `CommandSerialization.fs` / `CommandRendering.fs` / `CommandReports.fs` / `Cli/Rendering.fs` | the collision-free lifecycle-**value** token `spec-kit` appears — its presence means a lifecycle value was special-cased (refined from the literal `lifecycle`, which is generic SDD vocabulary on the clean tree; research Decision 9) |
| **C3 — planted-violation proof** | a synthetic in-memory source string | the offender-detector returns an **empty** list for a string that contains a planted rendering identifier, or one containing a planted `spec-kit` lifecycle-value literal (i.e. the scan would *miss* a real violation) |

> C2 is deliberately **not** repo-wide and scans for one **collision-free** value token only.
> `lifecycle` is core SDD vocabulary even inside the scaffold source (`nextLifecycleEffects`,
> the "begin the lifecycle at charter" hint, `lifecycleStageReadiness`), so the literal-token
> scan is infeasible (research Decision 9). Of the lifecycle *values* `sdd`/`spec-kit`/`none`,
> only `spec-kit` avoids collision — `"sdd"` collides with `Ownership = "sdd"` and the
> `FS.GG.SDD` identifier, `"none"` with `None`/`(none)` projection text. The comprehensive
> "no branching on **any** value" guarantee is the behavioral **C4** (T014), not this grep.
> See research Decision 4 & 9.

## Output

On failure, each component reports the offending location, e.g. `"{path}: {token}"`
(existing `ScaffoldGuardTests.fs:42` shape) — satisfying SC-005 "names the offending
location".

## Companion behavioral guard (not part of the scan)

**C4 — value-agnosticism** (in `ScaffoldCommandTests.fs`): running the recording fixture with
an arbitrary `lifecycle` value yields an identical forwarded arg vector, outcome, and
provenance shape (modulo the echoed value) to the `lifecycle=sdd` run. This proves "no
branching on *any* lifecycle value" — a guarantee a source grep cannot give — and together
with C1–C3 fully discharges FR-007 / US3.

## Acceptance mapping

| Scenario | Component |
|---|---|
| US3.1 — no rendering identifier in generic SDD | C1 |
| US3.2 — no special-casing of `sdd` or any lifecycle value | C2 + C4 |
| US3.3 / SC-005 — scan fails and names location when a violation is planted | C3 |
