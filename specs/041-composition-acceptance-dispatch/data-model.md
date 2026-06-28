# Phase 1 Data Model: Composition-Acceptance Consumes the Dispatched Registry

This feature adds CI wiring and one resolver; it defines **no new persisted SDD schema**. The
entities below are the run-time values the resolver and workflow operate over. The authoritative
machine contract is the cross-repo dispatch payload — see
[contracts/registry-dispatch.md](./contracts/registry-dispatch.md).

## Entity: Dispatched registry payload

The `client_payload` of the `composition-registry-updated` `repository_dispatch` event, published by
the FS.GG.Templates sender. **Consumed, never authored, by SDD.**

| Field | Type | SDD use |
|---|---|---|
| `registry_content` | string (full registry YAML) | materialized **verbatim** to the ephemeral registry file (FR-002) |
| `registry_path` | string (`providers/rendering.providers.yml`) | informational; the canonical source path (not used to locate a file in SDD) |
| `registry_sha256_12` | string (12-char hex) | the **drift signal**; surfaced to the run, recomputed from the materialized bytes as an integrity check (FR-008) |
| `version` | string (= `registry_sha256_12`) | the content identity / drift signal alias |
| `source_repo` / `source_sha` / `source_ref` | string | sender-added provenance; surfaced for traceability, not required |

**Validation / rules**:
- A `repository_dispatch` whose `type` ≠ `composition-registry-updated` MUST NOT trigger (D1).
- Missing/empty `registry_content` on a dispatch run ⇒ **fail closed** (FR-005).
- No field value (especially any rendering id/path/url carried in `registry_content`) may be copied
  into SDD source or the result document (FR-003).

## Entity: Registry source (resolved per run)

Exactly one of three, chosen by deterministic precedence (FR-004, D2):

| Precedence | Source | Trigger | Selection condition |
|---|---|---|---|
| 1 (highest) | manual `registry_path` input | `workflow_dispatch` | input is non-empty |
| 2 | dispatched `registry_content` | `repository_dispatch` | event is `composition-registry-updated` |
| 3 | secret `FSGG_SDD_ACCEPTANCE_REGISTRY` | `schedule` (or fallback) | secret is non-empty |

**State → outcome** of the resolver:

| Resolved state | Resolver result |
|---|---|
| input path present | export that path; exit 0 |
| dispatch + non-empty content, sha matches advertised | materialize verbatim → ephemeral file; export its path; exit 0 |
| dispatch + non-empty content, sha ≠ advertised `registry_sha256_12` | `::error::` integrity-mismatch diagnostic; **exit ≠ 0** (fail closed, D5) |
| dispatch + missing/empty content | `::error::` diagnostic; **exit ≠ 0** (fail closed, FR-005) |
| secret non-empty | materialize verbatim → ephemeral file; export its path; exit 0 |
| none of the above | `::error::` diagnostic; exit ≠ 0 (unchanged from today) |

**Invariant**: the resolved value is always a filesystem path that the unchanged acceptance reads as
`FSGG_SDD_ACCEPTANCE_REGISTRY` — the **only** thing that varies across sources (FR-006). There is no
per-source behavioral fork downstream.

## Entity: Materialized registry file

An ephemeral file under `RUNNER_TEMP` (e.g. `${RUNNER_TEMP}/fsgg/providers.yml`).

**Rules**: written byte-for-byte from the chosen content (`printf '%s'`, D4); never committed; deleted
with the runner. Its sha256 (first 12 hex chars) MUST equal the advertised `registry_sha256_12` on a
dispatch run (integrity cross-check, D5); a **mismatch fails closed** (`::error::` + non-zero exit) —
a corrupted or mis-advertised payload is a wiring defect, never a green. The integrity check applies
only to the dispatch source (the secret/manual sources advertise no sha).

## Entity: Composition-acceptance result (unchanged)

The existing `composition-acceptance-result` v1 document (034). **This feature changes neither its
body nor its `sensed` block.** The registry it tested may now originate from the dispatch, but that
fact is surfaced at the **run** layer (Step Summary / log), not in the document (FR-008, D5).

## Non-entities (explicitly out of scope)

- No new `fsgg-sdd` command, lifecycle stage, or `nextLifecycleCommand` value.
- No new release-catalog (`release-readiness.json`) artifact.
- No change to `scaffold-provider`, `scaffold-provenance`, or `composition-acceptance-result` schemas.
