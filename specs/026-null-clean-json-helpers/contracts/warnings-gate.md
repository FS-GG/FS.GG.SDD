# Contract: Warnings-as-Errors Gate + Null-Handling Convention

**Feature**: 026-null-clean-json-helpers | **Date**: 2026-06-26 | **Phase**: 1

This feature exposes no public API. Its "contracts" are (1) the build-configuration
change and (2) the internal null-handling convention every touched site must follow.
Both are internal/build-facing; no `.fsi`, schema, generated view, or command
contract changes.

## C1 — Build configuration (`Directory.Build.props`)

**Change** (lands in Story 2, only after the FS3261 count is 0):

```xml
<!-- before -->
<TreatWarningsAsErrors>false</TreatWarningsAsErrors>

<!-- after: keep the false default, add a scoped escalation -->
<TreatWarningsAsErrors>false</TreatWarningsAsErrors>
<WarningsAsErrors>FS3261;FS0025</WarningsAsErrors>
```

**Guarantees**:
- A new FS3261 (nullness) or FS0025 (incomplete-match) warning anywhere in `src` or
  tests **fails the build**, reported as an error with file/line.
- No other warning category is promoted to an error (FS-anything-else stays a
  warning under the unchanged `TreatWarningsAsErrors=false`).
- Inherited by every `src` and test project via `Directory.Build.props` import — no
  per-`.fsproj` duplication.

**Non-goals**: this property does **not** turn on global warnings-as-errors and does
**not** change `<Nullable>enable</Nullable>` (already set).

## C2 — Null-handling convention (internal, every touched site)

Resolve nullability with a built-in F# idiom; never paper over it with a suppression
unless the site is genuinely intractable (then enumerate it — FR-009).

| Situation | Idiom | Note |
|---|---|---|
| `JsonElement.GetString()` → `string option` | `Option.ofObj (e.GetString())` | preferred at the `Internal` JSON boundary |
| `GetString()` → `string` with default | `Option.ofObj (e.GetString()) \|> Option.defaultValue ""` | **must** equal the prior `if isNull v then "" else v` (INV-1) |
| param that may receive null | annotate `(x: string \| null)` | makes an existing `isNull x` well-typed |
| `isNull` on a non-nullable string | replace with `String.IsNullOrEmpty x` / `String.IsNullOrWhiteSpace x` | BCL signature accepts `string \| null` |
| arbitrary nullable reference | `match x with \| null -> … \| v -> …` | for `Process`/`DirectoryInfo`/one-offs |

**Convention guarantees**:
- **Behavior-preserving**: the chosen idiom returns the same value the old code did
  for both the null and non-null inputs. This is the determinism contract (SC-004):
  every coalesced default must be byte-identical to today's output.
- **Internal only**: helpers stay `[<AutoOpen>] module internal` with no `.fsi`; no
  public surface or baseline changes (INV-3).
- **No silent suppression**: `#nowarn "3261"` / `#nowarn "25"` at file or project
  scope is prohibited (it would blind the gate). Per-site suppression is allowed only
  for an enumerated intractable site.

## C3 — Verification contract (what "done" means)

| ID | Check | Pass condition |
|---|---|---|
| V-1 | `dotnet build -c Release --no-incremental` (before adding C1) | 0 FS3261 and 0 FS0025 emitted |
| V-2 | full test suite | all 438 tests pass |
| V-3 | `--json` output for charter/analyze/refresh on a fixture | byte-identical to pre-change baseline |
| V-4 | add C1, rebuild clean | build succeeds (still 0) |
| V-5 | inject one nullness defect, rebuild | build **fails** with FS3261 as error; revert ⇒ green |
| V-6 | grep promoted categories | no warning category other than FS3261/FS0025 fails the build |

These map to SC-001…SC-006 in the spec and INV-1…INV-5 in `data-model.md`.
