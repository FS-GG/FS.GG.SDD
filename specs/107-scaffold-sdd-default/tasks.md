# Tasks: `sdd` as the Default Lifecycle at the SDD Scaffolder, and the Spec-Kit→SDD Migration Path

**Spec**: `specs/107-scaffold-sdd-default/spec.md` · **Plan**: `specs/107-scaffold-sdd-default/plan.md` ·
**Item**: FS.GG.SDD#597 · **Epic**: `.github#1245`

## PR-1 — unblocked now (docs only; no CLI behavior change, no publish)

- [x] T001 Mechanism witness — **already discharged, no new test.** Feature-050 `T008`/`T009` in
      `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs` pin that a provider-declared parameter
      **default** (no override) is forwarded verbatim and recorded in `summary.EffectiveParameters`,
      `scaffold-provenance.json`, and the json/text projections (over a value-agnostic `variant`
      `default: alpha`) — this *is* the `effectiveParameters` fold at `HandlersScaffold.fs:96` (AC-001).
      Feature-031 `T010`–`T014` pin `lifecycle`-keyed forwarding, value-agnosticism (arbitrary nonce),
      and override/order-independence (AC-002, FR-002/FR-003). A `lifecycle`-keyed default clone would
      duplicate the same key-agnostic path and imply `lifecycle` is special (contra FR-001).
- [x] T002 Regression witnesses — **already covered.** `--provider`-omitted → `scaffold.providerMissing`
      exit 1 is pinned by `ScaffoldGuardTests`; provenance `schemaVersion` v1 by `ScaffoldProvenanceTests`.
      No `sdd`/`spec-kit`/`lifecycle` value literal is added to `src/` here (this feature ships no `src/`
      change), which the reflection `PublicSurface`/`surface --check` gates keep honest (FR-001/FR-004).
- [x] T003 Author `docs/migrate-spec-kit-lane-to-sdd.md` — the `spec-kit`-lane → `sdd`-lane move via
      `fsgg-sdd upgrade` (no-clobber re-supply of the missing SDD skeleton) and via clean re-scaffold on
      the `sdd` lane; additive / non-destructive / re-appliable guarantees; **no** provider-specific
      package id, template id, path, or docs URL (FR-005/FR-006, AC-005). Deprecation framing (leave the
      lane before the removal deadline), distinct from the additive-adopt guide.
- [x] T004 Cross-link both guides — pointer added from `docs/migration-from-spec-kit.md` to the new doc
      and back; new doc frontmatter is `category: SDD`, `categoryindex: 6`, `index: 16` (adjacent indices
      12–15 taken; cross-links carry the relationship without renumbering `adopting-governance.md`). All
      relative links and the ADR-0056 URL verified to resolve (AC-005).
- [ ] T005 Gates green — `dotnet test` (offline suite), `fantomas` clean, `PublicSurface` +
      `surface --check` untouched (no public surface moved). Drive it: PR-1 body records the real
      `fsgg-sdd scaffold` run against the synthetic provider showing `lifecycle` forwarded verbatim
      (plan §Verification — driven, not just asserted). **No** `Directory.Build.local.props` bump, **no**
      publish.

## PR-2 — gated on `.github#1246` publishing (publish-before-flip)

- [ ] T006 [after `.github#1246` publishes] End-to-end value witness — an override-free
      `fsgg-sdd scaffold --provider <fs-gg-ui>` against the **published, flipped** template records
      `lifecycle=sdd` (AC-004). Assert only once the flipped provider version is public; do **not** carry
      an `sdd` expectation ahead of the provider that owns it. Publish only if a real behavior/test that
      must ship warrants it.
