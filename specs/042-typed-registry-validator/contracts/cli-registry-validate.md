# Contract: file→verdict edge entrypoint (`fsgg-sdd registry validate`)

The gate-callable entrypoint that turns the on-disk file into a verdict. This is what
FS-GG/.github#18 invokes instead of `scripts/validate-registry.py`. It composes the YAML
**load** edge (YamlDotNet, in `FS.GG.SDD.Artifacts`) with the pure `validateDocument`
(in `FS.GG.Contracts`). Exact command name/wiring is finalized in `/speckit-tasks`; the
*contract* below is fixed.

## Load edge (in `FS.GG.SDD.Artifacts`)

```fsharp
/// Parse the on-disk registry YAML into the typed model.
/// I/O lives here (not in the BCL-only Contracts leaf) — Constitution V.
val load: path: string -> Result<Fsgg.Registry.RegistryDocument, RegistryLoadError>
```

- A missing/unreadable file or unparseable YAML returns `Error` (never throws/crashes) —
  surfaced downstream as a single `MalformedDocument`-class diagnostic, distinct from
  content diagnostics (Constitution VIII; FR-001, US1-S3, edge cases).
- The mapping preserves document order (determinism, R5) and tolerates unknown/extra keys
  (additive registry evolution must not break the loader — edge case).

## Command behavior

`fsgg-sdd registry validate <path>` (cross-cutting; not a lifecycle stage):

1. `load <path>` → on `Error`, emit a single `MalformedDocument`-class load/parse diagnostic.
2. On `Ok document` → `Registry.validateDocument document`.
3. Project the outcome as a **deterministic registry-validation report** in all three
   formats (`--rich` > `--text` > `--json` > default), per the repo's CLI output contract.
   The default/`--json` projection is the deterministic automation contract the gate consumes.
4. **Exit code**: `0` when `Valid`; `1` when `Invalid` or load failed. (Matches the Python
   stand-in's "exit 1 on any diagnostic".)

**Wiring decision (as implemented).** `registry validate` is realized as a **CLI-level
cross-cutting command** in `FS.GG.SDD.Cli` (module `RegistryValidate`), dispatched in
`Program.fs` *before* `parseCommand` — exactly the established pattern of the existing
`validate` harness command. It carries its **own** deterministic report
(`RegistryValidateReport`: `{ path, valid, diagnostics[] }`) with `serialize` (JSON) /
`renderText` / a Spectre rich projection, rather than the lifecycle `CommandReport`. This
keeps the work-item-shaped `CommandReport` / `parseCommand` / per-command contracts (and
their apicompat surface) untouched for a command that has no work item, no stage, and no
`nextLifecycleCommand` — the same reason `validate` is a peer rather than an `SddCommand`.
The user-facing contract above (path-in → verdict-out, three projections, exit code) is
unchanged by this choice. `--rich` is presentation-only and degrades to plain text when
non-interactive or color-disabled.

## What it is NOT

- It does **not** perform the `fsgg-contracts`-pin == actual-package-version coupling
  assertion (the Python `--expect` flag). That assertion is already live in the gate and
  stays where it is; this feature replaces only the **schema-validation** half.
- It does not write or mutate the registry; read-only.

## Success criteria coupling

| Behavior | Spec ref |
|---|---|
| path in → verdict out, no stand-in script | FR-001, SC-003, US1 |
| canonical file → exit 0, zero diagnostics | FR-008, SC-001 |
| deterministic JSON for a CI `--exit-code` gate | FR-007, SC-004 |
| broken file → correct diagnostic + exit 1 | FR-006, SC-002 |
| safe load failure, not a crash | FR-001, US1-S3, Constitution VIII |
| swap-in parity with the Python authority | FR-010, SC-005 |
