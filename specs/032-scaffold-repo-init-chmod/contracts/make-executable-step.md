# Contract: Make-executable post-instantiation step

Behavior contract over `fsgg-sdd scaffold` for setting executable bits on produced shell
scripts.

## Trigger

Same as repo-init: after a success create outcome, real path only (FR-008/009).

## Target discovery (generic)

Targets = `produced |> List.filter (fun p -> p.EndsWith(".sh"))`, where `produced` is the
existing app-only, non-SDD produced-path diff (`HandlersScaffold.fs:268-273`). Generic file
*shape*; no provider-specific script name (FR-006, Decision 4).

## Action

One `SetExecutable(path)` effect per target. The edge interprets it with
`File.SetUnixFileMode`, OR-ing the execute bits onto the current mode, wrapped in `try`
(Decision 2).

## Guarantees

- **G1 (FR-005/SC-003)**: every produced `.sh` is left executable; `ExecutableScriptCount`
  records the count, with **no** manual `chmod` by the author.
- **G2 (US2-AC2)**: provider produced no shell scripts ⇒ the step is a reported no-op
  (`ExecutableScriptCount = 0`, `ExecutableScriptsSkipped = 0`), succeeds.
- **G3 (US2-AC3/FR-010)**: a bit that cannot be applied (read-only FS, non-Unix host)
  increments `ExecutableScriptsSkipped`, raises advisory `scaffold.scriptsNotMadeExecutable`,
  and does **not** convert the scaffold's success into a defect.
- **G4 (FR-013)**: re-applying executable bits is idempotent and safe.
- **G5 (FR-006/SC-005)**: discovery uses only `.sh` shape over the scaffolded tree — no
  provider/template/rendering identifier.
- **G6 (FR-012)**: the executable bit is a filesystem attribute; it changes no provenance or
  report JSON byte (only the additive count fields reflect it).

## Non-goals

- No shebang sniffing in v1 (recorded as a possible future refinement in research Decision 4).
- SDD-owned/skeleton files are never targeted (only the produced app-only set).
</content>
