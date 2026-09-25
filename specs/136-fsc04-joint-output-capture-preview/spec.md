# Joint Work-Model Output and Capture Preview

**Status:** source-only FSC-04 verification prerequisite, stacked on draft #1021. **Owner:** FS.GG.SDD.

## Contract

- FR-001: Bind the proposed work-model JSON to independently selected snapshots and the generator version through #1020 exact regeneration before physical capture.
- FR-002: Verify two #1017 pinned physical captures against that same prepared proposal and v2 candidate. A capture or verification refusal on either pass blocks the preview.
- FR-003: Compare raw bytes by path across successful captures. A raw change blocks the preview even if decoded text and candidate digests are equal.
- FR-004: Return only the proposed output digest/path and source paths. Do not return output bytes, captured bytes, or a write effect.

## Red-before controls

The #1021 temporal preview accepts stable sources without inspecting a stale proposed JSON body. The #1020 exact-output preview accepts a previously captured set even if a source changes afterward. The joined preview refuses the stale body before capture and refuses the observed later source change on its second pass. An independent BOM-only byte change refuses despite two individually valid text/digest captures; a stable physical set succeeds.

## Limit

Two sequential captures do not prove one simultaneous cross-root instant. ABA, a repeated mixed set, timestamp-hidden or post-check changes, and Windows concurrency remain unresolved. #1017 physical custody, #1018 producer verification-wave and staging/rollback decisions, installed parity, publication, receiver pinning, and GS2-10 freeze remain separate holds.
