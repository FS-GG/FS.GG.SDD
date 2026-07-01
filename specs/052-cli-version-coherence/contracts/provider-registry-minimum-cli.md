# Contract: provider registry — `minimumFsggSdd.version` (additive, value-agnostic)

Artifact: `.fsgg/providers.yml` (author-/provider-owned) → parsed to
`Fsgg.Provider.ProviderDescriptor`. Serves FR-002/FR-009.

> **Cross-repo reconciliation (T032):** the sibling epic-#85 work (ADR-0008 / #86,
> registry #87, `FS.GG.Templates#43`) was already **merged** using a **nested
> `minimumFsggSdd:` mapping** whose `version` scalar carries the value — **not** the flat
> `minimumCliVersion` scalar this contract originally proposed. SDD is aligned to the merged
> upstream shape (this is what "confirm the key against #85" resolved to). SDD's internal
> field name stays `ProviderDescriptor.MinimumCliVersion` and the provenance field stays
> `requiredMinimumCliVersion` — only the YAML key SDD reads changed.

## Change

Each provider entry MAY declare an optional `minimumFsggSdd` mapping. SDD reads its `version`
scalar **value-agnostically** and never embeds a concrete value (FR-009 / SC-005). Sibling
metadata (`requires`/`adr`/`registry`/`tracking`) is provider-owned and ignored by SDD.

```yaml
schemaVersion: 1
providers:
  - name: rendering
    contractVersion: "1.0.0"
    templateId: "…"          # provider-owned; never in generic SDD
    source: "…"              # provider-owned; never in generic SDD
    minimumFsggSdd:          # NEW (optional): coherent-set orchestrator axis (ADR-0008)
      version: "0.3.0"       # minimum coherent fsgg-sdd version; null ⇒ pending publish
      requires: "…"          # provider-owned metadata, ignored by SDD
    parameters: [ … ]
```

## Rules

- **Optional**: absent mapping, or `version` absent/null → `ProviderDescriptor.MinimumCliVersion
  = None`. Never affects whether an entry is dropped (the four required scalars are unchanged).
- **Raw**: `version` stored verbatim as `string option`; not parsed/normalized at read time.
  Validity is decided only at comparison (`Fsgg.Version`, see `version-compare.md`).
- **Value-agnostic**: no provider-specific package id, template id, path, or version literal
  enters generic SDD as a result of this feature (SC-005, grep-verifiable).

## Cross-repo coordination (epic FS-GG/.github#85)

The key `minimumFsggSdd.version` and its schema placement are a shared provider-contract
detail owned upstream. Per `cross-repo-coordination`, SDD matches what Templates writes into
`providers/rendering.providers.yml` (sibling sub-issues #86/#87, FS.GG.Templates#43). This
feature is **independently shippable**: today the real provider declares `version: null`
(PENDING PUBLISH — no released fsgg-sdd yet seeds Features 049+051), so SDD degrades to
"record CLI version, no comparison, no advisory" until a concrete version is pinned.

## Fixtures

- `tests/**` provider-registry fixtures gain a variant **with** `minimumCliVersion` and a
  variant **without**, to exercise both the compare path and the degradation path.
