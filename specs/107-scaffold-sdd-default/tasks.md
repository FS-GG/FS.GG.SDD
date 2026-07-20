# Tasks: `sdd` as the Default Lifecycle at the SDD Scaffolder, and the Spec-Kit→SDD Migration Path

**Spec**: `specs/107-scaffold-sdd-default/spec.md` · **Plan**: `specs/107-scaffold-sdd-default/plan.md` ·
**Item**: FS.GG.SDD#597 · **Epic**: `.github#1245`

## PR-1 — unblocked now (docs + tests only; no CLI behavior change, no publish)

- [ ] T001 Mechanism witness — scaffold-handler tests (e.g. `tests/FS.GG.SDD.Commands.Tests/ScaffoldCommandTests.fs`)
      pinning that `effectiveParameters` (`HandlersScaffold.fs:96`) forwards a provider-declared `lifecycle`
      default **verbatim**: a synthetic provider declaring `lifecycle` `default: sdd`, no `--param` override
      → `effectiveParameters` / `scaffold-provenance.json` record `lifecycle=sdd` (AC-001); `default:
      spec-kit` → `spec-kit`, and `--param lifecycle=none` → `none` (AC-002, FR-002/FR-003). Reuse the
      existing `tests/fixtures/scaffold-provider/lifecycle/` fixture where possible. Each row
      mutation-checked (drop the `spec.Default` fold, or hard-code a value → red).
- [ ] T002 Regression witnesses — `--provider` omitted still blocks `scaffold.providerMissing`, exit 1
      (AC-003, FR-004); `scaffold-provenance.json` stays `schemaVersion` v1 on the new run (FR-004). Assert
      no `sdd`/`spec-kit`/`lifecycle`-value literal was added to `src/` (grep guard, FR-001).
- [ ] T003 Author `docs/migrate-spec-kit-lane-to-sdd.md` — the `spec-kit`-lane → `sdd`-lane move via
      `fsgg-sdd upgrade` (no-clobber re-supply of the missing SDD skeleton) and via clean re-scaffold on
      the `sdd` lane; additive / non-destructive / re-appliable guarantees; **no** provider-specific
      package id, template id, path, or docs URL (FR-005/FR-006, AC-005). Deprecation framing (leave the
      lane before the removal deadline), distinct from the additive-adopt guide.
- [ ] T004 Cross-link both guides — add a pointer from `docs/migration-from-spec-kit.md` to the new doc
      and back; confirm doc index/frontmatter (`category`/`index`) is coherent with the docs site. Doc-lint
      / link check over both passes (AC-005). (`docs/migration-from-spec-kit.md` is the only edit outside
      `specs/`/the new doc; **widen the claim `Paths:`** to include the two `docs/` files before touching
      them.)
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
