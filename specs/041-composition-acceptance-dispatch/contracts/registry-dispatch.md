# Contract: `composition-registry-updated` Registry Dispatch (consumer side, v1)

The cross-repo contract by which **FS.GG.Templates** (producer) pushes its canonical provider
registry content to **FS.GG.SDD** (consumer) so SDD's composition-acceptance tests the live registry,
not a stale secret copy. This document is SDD's **consumer-side** view of that contract.

**Ownership**: jointly owned with FS.GG.Templates; the canonical registry
(`providers/rendering.providers.yml`) and the reusable dispatch **sender** live in FS.GG.Templates /
FS-GG.github. **A change to this contract is a coordinated, two-sided change** — drive it through the
**cross-repo-coordination** protocol (file/track on the org Coordination board; update the
contract/compatibility registry). SDD MUST consume the published shape without inventing rendering
identity (034 FR-009 / SC-003).

**Schema version**: v1. **Status**: consumed by SDD as of feature 041.

## Transport

- GitHub `repository_dispatch` event sent to the SDD repository.
- Event type (`event_type`): **`composition-registry-updated`** — SDD triggers `composition-acceptance.yml`
  **only** on this type (`on: repository_dispatch: types: [composition-registry-updated]`). Any other
  type MUST NOT trigger the acceptance.

## `client_payload` fields

| Field | Type | Required | Meaning |
|---|---|---|---|
| `registry_content` | string | **yes** | the full canonical registry YAML, verbatim |
| `registry_path` | string | yes | canonical source path, `providers/rendering.providers.yml` (informational) |
| `registry_sha256_12` | string (12 hex) | yes | sha256 of `registry_content`, first 12 chars — the drift signal |
| `version` | string | yes | content identity; equals `registry_sha256_12` |
| `source_repo` | string | added by sender | producer repo (provenance) |
| `source_sha` | string | added by sender | producer commit (provenance) |
| `source_ref` | string | added by sender | producer ref (provenance) |

## SDD consumer obligations

1. **Materialize verbatim (FR-002)**. Write `registry_content` byte-for-byte to an ephemeral runner
   file and point `FSGG_SDD_ACCEPTANCE_REGISTRY` at it. Multi-line YAML and special characters are
   preserved (pass content via an env var, write with `printf '%s'`).
2. **Fail closed on empty (FR-005)**. A `composition-registry-updated` event with missing/empty
   `registry_content` MUST exit non-zero with a clear diagnostic — never pass, never silently skip.
3. **No leak / no commit (FR-003)**. The content (and any rendering id/template/path/docs URL it
   carries) MUST NOT be committed, and MUST NOT appear in SDD source, the resolver, or the
   `composition-acceptance-result` document. The materialized file is ephemeral run state.
4. **Surface the drift signal (FR-008)**. Record `registry_sha256_12` / `version` to the run
   (GitHub Step Summary / log) so the run is traceable to the exact content tested. Recompute the
   sha256 of the materialized bytes and verify it matches the advertised value (integrity check); a
   **mismatch fails closed** (non-zero exit, `::error::`) — a corrupted or mis-advertised payload is a
   wiring defect, never a green. The `composition-acceptance-result` v1 document is **not** modified.
5. **No behavioral fork (FR-006)**. The dispatched source feeds the identical acceptance facts,
   gating, outcome→verdict mapping, and result contract as the secret/manual sources. Only the
   registry *source* differs.

## What SDD does NOT consume

- SDD does not parse, validate, or interpret the `registry_content` beyond materializing it — the
  provider resolution is the unchanged scaffold path (034
  [acceptance-protocol](../../034-scaffold-composition-acceptance/contracts/acceptance-protocol.md)).
- SDD invents no field, no encoding, and no rendering-specific identity.

## Versioning & compatibility

- v1 is additive over the existing secret/manual sources (US3): both continue to work unchanged.
- Adding a new optional `client_payload` field is a backward-compatible producer change SDD may
  ignore. Renaming/removing a required field, or changing the event type, is a breaking v2 change
  requiring a coordinated update on both repos and a compatibility-registry entry.
- The sender may be **dormant** (org App/secrets not yet provisioned) — until a dispatch arrives, the
  consumer wiring is simply not triggered and the secret/manual paths keep working (spec Assumptions).
