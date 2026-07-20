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
- [x] T005 Gates green — offline `dotnet test` green (incl. the driven `ScaffoldCommandTests`
      default-applied + value-agnostic `lifecycle` cases, which run a real `dotnet new` against a
      synthetic provider and show a provider-declared default forwarded verbatim — the AC-001/002
      mechanism, driven not just asserted), `fantomas` clean, `PublicSurface` + `surface --check`
      untouched (this feature ships **no** `src/` change, so no public surface moved). **No**
      `Directory.Build.local.props` bump, **no** publish.

## PR-2 — carved to follow-up `FS.GG.SDD#601` (publish-before-flip)

Item #597 completes as an independent **producer** the moment PR-1 lands: the scaffolder-default
*mechanism* (AC-001–003, discharged by the existing value-agnostic tests) and the *migration path*
(AC-005, #600) are done, and #597 is the producer that unblocks the flip `.github#1246`. AC-004 is
the one criterion that **cannot** be satisfied before that flip publishes, and #1246 is itself blocked
by #597 — so AC-004 stays in #597 only by re-forming the deadlock `/check-board` broke. It is therefore
tracked as a follow-up, **blocked by `.github#1246`**, popping when the flip publishes:

- [ ] T006 → **`FS.GG.SDD#601`** (blocked by `.github#1246`). End-to-end value witness — an
      override-free `fsgg-sdd scaffold --provider <fs-gg-ui>` against the **published, flipped** template
      records `lifecycle=sdd` (AC-004). Realize via the network-gated composition-acceptance suite so it
      drives the **real** published provider; do **not** carry an `sdd` expectation ahead of the provider
      that owns it (FR-001). Publish only if a real behavior/test that must ship warrants it.
