# Phase 1 Data Model: Collapse Diagnostic Builder + Unify JSON Serializers

This is an internal refactor, so the "entities" are the function shapes being
introduced or consolidated — not new domain types. No record/DU in the public
contract changes; `Diagnostic`, `SourceDigest`, `OutputDigest`, `SourceLocation`,
and `WorkModel` are all held byte-stable.

---

## E1 — Command diagnostic builder (Story 1, `CommandReports.fs`)

The single mechanism through which every `CommandReports` diagnostic is built.

| Element | Shape | Visibility | Notes |
|---------|-------|-----------|-------|
| `commandDiagnostic` | `id → severity → path:string option → message → correction → relatedIds → Diagnostic` | **public** (in `.fsi`, byte-stable) | Existing shared helper. Resolves `path` via `artifactForPath` and delegates to `Diagnostics.create`. Unchanged. |
| error-default builder | `id → path:string option → message → correction → relatedIds → Diagnostic` | internal (not in `.fsi`) | Fixes `severity = DiagnosticError`; removes the ~99 hand-spelled literals. Layered over `commandDiagnostic`. |
| warning-default variant | analogous, `severity = DiagnosticWarning` | internal | Used by the 14 warning constructors so none is promoted to error. |
| family helpers | per-family thin builders for `missing*` / `malformed*` / `duplicate*` / `unknown*` / `stale*` / `unsafe*`+`failed*` | internal | Capture each family's shared `id`/`path`/`message`/`correction` skeleton; the varying parts are parameters. |
| ~113 named functions | each retains its exact `.fsi` signature | **public** (byte-stable) | Become thin call sites over the helpers above. Names, ids, messages, corrections, related-ids, severities unchanged. |

**Invariants**
- Every named diagnostic constructor routes through `commandDiagnostic` (directly
  or via a default/family helper); none re-implements severity/path/sort inline
  (SC-001).
- Severity per function is exactly today's: 99 error, 14 warning (the 14 are
  enumerated from source and re-checked). No flip (FR-002, "severity is not
  uniform" edge case).
- No id, message, correction, or related-id string changes (FR-009).
- `CommandReports.fsi` is byte-identical to baseline (FR-007).

---

## E2 — Shared low-level JSON writer primitives (Story 2)

New module `FS.GG.SDD.Artifacts.Json.JsonWriters` (sub-namespace chosen in
research D1 to keep the guarded surface baseline byte-identical), consumed by both
`Serialization` (Artifacts) and `CommandSerialization` (Commands).

| Primitive | Shape | Replaces |
|-----------|-------|----------|
> Names match the authoritative `module JsonWriters` `.fsi` sketch in
> `contracts/shared-json-writers.fsi.md` (module-qualified, unprimed). The
> "Replaces" column lists the pre-change per-assembly bodies these consolidate.

| `StringListOrder` | `= SourceOrder \| Sorted` (DU) | the implicit sort/no-sort fork |
| `writeStringList` | `Utf8JsonWriter → StringListOrder → name → string list → unit` | Commands `writeStringList` (Sorted) + Artifacts `writeStringList` (SourceOrder) |
| `writeSourceDigest` | `Utf8JsonWriter → name → SourceDigest option → unit` | Commands `writeSourceDigest` + Artifacts `writeDigest` (called with `Some`) |
| `writeOutputDigest` | `Utf8JsonWriter → name → OutputDigest option → unit` | Commands `writeOutputDigest` + Artifacts `writeOutputDigest` (called with `Some`) |
| `writeLocation` | `Utf8JsonWriter → name → SourceLocation option → unit` | Commands `writeLocation` (name fixed `"location"`) + Artifacts `writeLocation`/`writeSourceLocation` (parameterized name) |
| `writeDiagnostic` | `Utf8JsonWriter → StringListOrder → Diagnostic → unit` | Commands `writeDiagnostic` (Sorted) + Artifacts `writeDiagnostic` (SourceOrder) |

**Field-shape invariants (byte-fixed)**
- Digest object: `{ "algorithm", "value" }`; `None`/absent → `writeNull name`.
- Location object: `{ "line", "column" }` each `number`-or-`null`; absent →
  `writeNull name`.
- Diagnostic object: `id`, `severity` (`severityValue`), `artifact` (path or
  null), `location`, `message`, `correction`, `relatedIds`, in **exactly** today's
  field order.

**Caller bindings (the only divergence points, now parameters)**

| Caller | string lists | diagnostic `relatedIds` | location name |
|--------|-------------|------------------------|---------------|
| Commands (`CommandSerialization`) | `Sorted` | `Sorted` | `"location"` (fixed today) |
| Artifacts (`Serialization`) | `SourceOrder` | `SourceOrder` | per-call name (`"location"`, `"sourceLocation"`) |

**Invariants**
- Each previously-duplicated writer body exists in exactly one place; both
  serializers consume it (SC-002).
- `Serialization.fsi` and `CommandSerialization.fsi` byte-identical to baseline;
  `serializeReport` / `serializeWorkModel` unchanged (FR-007).
- One-way `Artifacts → Commands` layering preserved; no new `Artifacts`→`Commands`
  dependency (FR-008).

---

## Cross-cutting invariant (both stories)

`--json` output of every command and the serialized work-model JSON are
**byte-identical** to the pre-change baseline — ordering, null-handling, digests,
locations, and every diagnostic string (FR-006, SC-004). Net `src` line count
decreases (SC-006). Release build green, no new warning category (SC-007).
