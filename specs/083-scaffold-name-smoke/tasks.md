# Tasks: Composition Smoke — Hyphenated Scaffold Name Builds and Tests Green

**Input**: Design documents in `specs/083-scaffold-name-smoke/` (plan.md, spec.md, research.md,
data-model.md, contracts/composition-smoke-fact.md, quickstart.md)

**Feature tier**: Tier 2 (test/CI-coverage addition; no product source, schema, or agent
surface change). Implements feature 080 **FR-011** / closes FS.GG.SDD#150.

**Tests**: This feature *is* test evidence — the "tasks" are the new xUnit facts and their
supporting harness helpers. All changes live in `tests/FS.GG.SDD.Acceptance.Tests`.

> ## ⚠ Finding (2026-07-05): the guard immediately caught a real upstream defect
> The gated fact was run end-to-end against the **real published provider** and correctly went
> **RED**: a scaffolded `Roquelike-DungeonCrawler` fails `dotnet build` at
> `EvidenceCommands.fs(623,18)` — the **template** (`FS.GG.UI.Template`, both `0.1.66-preview.1`
> and `0.1.68-preview.1`) imprints a hyphen-bearing product-name slug into a `let` **binding
> identifier** (`let roquelike-dungeoncrawlerDefectMessage = …`) it never wired to the derived
> namespace. SDD forwards `productName` (raw) + a valid derived `rootNamespace` correctly. Filed
> **FS-GG/FS.GG.Rendering#149**; **#150 re-blocked** on it. The SDD-side guard (this feature) is
> **complete and correct** — the gated fact is committed and will go green (closing #150) once
> Rendering fixes the template and Templates re-pins. A knowingly-red gated fact on the
> nightly/dispatch composition-acceptance lane is the intended fence, not a PR blocker.

## Format: `[ID] [P?] [Story] Description`

- `[P]` — parallel-safe · `[US1]`/`[US2]` — owning story
- Status: `[ ]` pending · `[X]` done with real evidence · `[-]` skipped (rationale on the line)

---

## Phase 1: Setup (confirm existing infrastructure)

- [X] T001 Baseline: `dotnet test tests/FS.GG.SDD.Acceptance.Tests` green on branch base — 33
  passed, 3 gated Skipped. Fail-before/pass-after baseline established.
- [X] T002 [P] Confirmed reuse points in `AcceptanceSupport.fs`
  (`resolveProviderDescriptor`, `runToCompletion`, `buildProbe`, `scaffoldRequest`,
  `RequiresRegistryFactAttribute`) and `CompositionAcceptanceTests.fs` (`composeOnce`,
  `resolveVerdict`/`noFacts`, the offline starter-param companion).

---

## Phase 2: Foundational (shared harness helpers)

- [X] T003 [US1] Added `namedScaffoldRequest root name` to `AcceptanceSupport.fs`: resolves the
  name key via `resolveProviderDescriptor root "rendering" |> Option.map resolveNameParameter`
  (fallback `defaultNameParameter`), appends `(nameKey, name)` to the `lifecycle=sdd` request.
  No hardcoded `productName`/rendering token.
- [X] T004 [US1] Added `testProbe root` to `AcceptanceSupport.fs`: `runToCompletion "dotnet"
  [ "test" ] root 300_000`. Same bounded edge as `buildProbe`.

---

## Phase 3: US1 (P1) — the guard fact *(MVP)*

- [X] T005 [US1] Added gated fact `` `hyphenated scaffold name builds and tests green` ``
  (`[<Trait("kind","composition-acceptance")>]` + `[<RequiresRegistryFact>]`) driving
  `namedScaffoldRequest root "Roquelike-DungeonCrawler"`. **Exercised end-to-end against the
  real provider** (registry = Templates `rendering.providers.yml`, `FS.GG.UI.Template::0.1.66`).
- [X] T006 [US1] Build + test assertions on `providerSucceeded` (declared-or-default build
  probe, then `testProbe`), failing named. **Verified real: build correctly fails on the
  hyphen-in-identifier defect** (Rendering#149) — the assertion fires exactly as designed.
- [X] T007 [US1] Non-success maps through `resolveVerdict` → `SkipUnavailable` tolerated, any
  other non-success fails. (Offline determinism of the mapping already covered by existing
  verdict-resolution facts.)

---

## Phase 4: US2 (P2) — gating + provider-neutrality proof

- [X] T008 [P] [US2] Added offline `[<Fact>]` `` `the hyphenated smoke request forwards the
  descriptor-resolved name key` `` over a synthetic registry (neutral `nameParameter:
  exampleProductName`); asserts `[ "lifecycle","sdd"; "exampleProductName", name ]`. **Passes
  offline.**
- [X] T009 [US2] Satisfied by the existing `` `composition-acceptance project contains no
  provider-specific identifiers` `` guard (ScaffoldGuardTests, recursive over all acceptance
  `.fs`, deny-list `fs-gg-ui`/`FS.GG.Rendering`) — **verified 10/10**; my additions use only the
  generic `"rendering"` provider name. No new scan needed.
- [X] T010 [P] [US2] Verified `dotnet test --filter "kind=composition-acceptance"` selects the
  new fact (4 gated facts selected). **No YAML change** to `composition-acceptance.yml`.

---

## Phase 5: Polish & validation

- [X] T011 Offline suite green with the additions: 34 passed / 4 gated Skipped; `result schema
  golden` + determinism byte-green (no schema drift, FR-008).
- [X] T012 [P] Guard-fences-the-regression **proven for real** (stronger than the planned
  reverted-080 manual check): the gated fact caught a genuine pre-existing compile defect
  (Rendering#149) on its first real-provider run. SC-002 demonstrated end-to-end.
- [X] T013 [P] quickstart.md steps validated against the delivered code (fact name, filter,
  gating); step 2's expected `pass` is currently a real-provider `fail` pending Rendering#149 —
  noted in the Finding above.

---

## Task counts & scope

- **US1 (P1, MVP)**: T003–T007 — the gated guard fact (implemented + exercised against the real
  provider). **US2 (P2)**: T008–T010. **Setup/Polish**: T001, T002, T011–T013. Total 13, all done.
- **Deliverable state**: SDD-side guard complete and correct; #150 stays open, re-blocked on
  **FS-GG/FS.GG.Rendering#149** (template `let {slug}DefectMessage` identifier defect). Closes
  when Rendering ships the fix + Templates re-pins and the smoke goes green.
