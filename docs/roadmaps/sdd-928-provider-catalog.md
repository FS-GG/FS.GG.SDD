# SDD #928 provider/catalog delivery

Owner: [FS.GG.SDD#928](https://github.com/FS-GG/FS.GG.SDD/issues/928).
Campaign: `unified-roadmap-20261003`; Unified part: [§9.8 language-independent workspaces](https://github.com/FS-GG/.github/blob/main/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md).
Architecture: [ADR-0092 and linked implementation design](https://github.com/FS-GG/.github/blob/main/docs/adr/0092-descriptor-driven-polyglot-workspace-providers.md).
The original issue and accepted architecture retain authority; this plan adds the first executable window.

Reuse the existing schema-v1 Provider records/registry, generic scaffold invocation, default overrides,
legacy F# identifier route, scaffold provenance and reserved-tree/skill-union boundaries. Descriptor
protocol 2.0.0 remains knowledge admission; it is not the new catalog protocol.

- [ ] SDD928-C1 — Parse pinned typed catalogs and resolve validated explicit identities — route: routine.
  Depends on accepted ADR-0092; pure declarations need no downstream publication.
  Scope: sibling Contracts/Artifacts ProviderCatalog APIs, focused semantic and golden fixtures,
  text/reflection surfaces and [spec132](../../specs/132-polyglot-provider-catalog/spec.md).
  Acceptance: all five providers remain fixture data, exact versions and invalid defaults refuse,
  explicit identities retain raw bytes, declarations/references remain complete, deterministic
  prepared output, legacy source callers and unsupported-protocol no-effect controls pass.
  Local source preparation does not check this box; root native qualification and merge readback do.

Later outcome outline, under the same issue:

C2 connects generic inspection/scaffold to pinned catalogs, platform/tool admission, validated identity
routing, actual effective provenance and WorkspaceModel, using existing mutation and sole Quint lifecycle
authorities. Invocation requires executable failure/cleanup/no-partial-state/skill-union controls.

C3 delivers the provider-authoring SDK and package-only qualification harness from issue comment
5436166922: minimal provider/examples, deterministic pack/instantiate and actual build/test/package,
wrong-version/tool controls, provenance, cleanup and operational-skill acceptance. Pure fixtures do not
certify providers.

C4 publishes coherent producers and independently verifies exact installed artifacts before Governance
#423/Templates #441 adoption. Package/source/tag/feed byte identity and independent installed consumption
remain mandatory. #928 stays open across this C1 checkpoint.

C1 changes no generated workspace, installed scaffold or lifecycle default. C2 changes only selected new
catalog source behavior; installed adoption follows qualified publication and receiver adoption. Existing
providers retain schema-v1/legacy compatibility. Retained upgrades separately need preview, no-clobber,
original-byte and rollback evidence. This plan does not close V2-LANG or extend its frozen cohort.

Observation: root accepted bounded native08 on 2026-10-05: all nine phases exited naturally with
zero, 56 tests passed (25 Contracts, 30 Artifacts, one legacy scaffold refusal), and source/cache
pins, output census and cleanup passed in 78.867 seconds. Public surfaces retain all existing
members and add 95 Contracts members plus one Artifacts parser member. The exact compiled
baselines were imported and tested with updates disabled. Contracts 7.6.0 and shared SDD 2.2.0
follow their separate additive-version policies; only local project dependency lock edges change.
Catalog schema 2 and descriptor protocol 3.0.0 remain independent of package versions.

C1 source qualification is accepted; protected landing remains pending. C2–C4 and #928 stay open.
No installed provider, invocation, package publication or downstream adoption is certified. Earlier
failed attempts remain failed and retained; their missing process identities remain unknown.

Root telemetry begin failed duplicate facts and has no child token; model/native usage remains
unknown. No usage counter or publication is inferred.
