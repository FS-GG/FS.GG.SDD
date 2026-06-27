# Contract: App-only provenance invariant

Pins what `.fsgg/scaffold-provenance.json` records after a successful `lifecycle=sdd`
scaffold. Documents existing behavior (`HandlersScaffold.fs:205-285`,
`ScaffoldProvenance.fs`); the schema (v1) is **unchanged**.

## Definitions

- `produced` = files the provider materialized into the target (the app-only tree).
- `skeleton` = files written by `initEffects` (`.fsgg/`, `work/`, `readiness/`, `AGENTS.md`,
  `CLAUDE.md`).
- `provenance.producedPaths` = the `path` set recorded in the provenance JSON.

## Invariants (asserted)

| ID | Invariant | How asserted | Req |
|---|---|---|---|
| P1 | `provenance.producedPaths == produced` (precision **and** recall = 100%) | diff target vs `skeleton`; compare to provenance set | FR-004 / SC-003 / US2.3 |
| P2 | every entry has `owner == "generatedProduct"` | parse provenance | FR-004 / SC-002 / US2.1 |
| P3 | `provenance.producedPaths ∩ skeleton == ∅` | set-disjoint check | FR-005 / SC-002 / US2.2 |
| P4 | `skeleton` files are **byte-identical** to a standalone `init` run | run `init` into a sibling temp dir; compare bytes | FR-005 |
| P5 | two identical runs → byte-identical provenance file | run twice into clean targets; compare bytes | FR-006 / SC-004 |
| P6 | producedPaths sorted; no clock, no absolute path in provenance | string assertions (no `root`, no `timestamp`) | FR-006 / SC-004 |
| P7 | `refresh` excludes the produced paths (externally owned) | run refresh; produced paths absent from refreshed/blocked view ids | FR-007 (030) re-asserted |

## Edge outcomes (provenance under FR-008)

| Scenario | Provenance behavior |
|---|---|
| empty product (`lifecycle=sdd`) | `outcome = providerSucceededEmpty`; `producedPaths = []` |
| SDD-tree intrusion | provider defect (exit 2); intruded SDD paths **never** recorded as app-only |
| required `lifecycle` omitted | blocked pre-invocation (exit 1); **no** provenance written |
