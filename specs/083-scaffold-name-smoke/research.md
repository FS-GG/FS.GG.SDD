# Research — Composition Smoke: Hyphenated Scaffold Name

Phase 0 for `083-scaffold-name-smoke`. No `NEEDS CLARIFICATION` remained after the spec; the
mechanism is fully determined by the existing acceptance harness and feature 080. This file
records the load-bearing design decisions.

## D1 — How the hyphenated name reaches scaffold (provider-neutral)

**Decision**: Forward the name as a `--param` keyed by the **provider-declared name
parameter**, resolved at run time from the registry descriptor via
`Fsgg.Provider.resolveNameParameter descriptor`. Only the **value**
(`Roquelike-DungeonCrawler`) is written in generic SDD; the **key** comes from the registry.

**Rationale**: Feature 080 FR-004 activated `ProviderDescriptor.NameParameter` (the reference
provider declares `productName`). The scaffold handler's `deriveIdentifierParameter` reads the
name parameter and derives the valid identifier into the provider-declared
`IdentifierParameter` sink, leaving the raw name for string/path/`.fsproj` contexts. The
acceptance already copies the external registry into `.fsgg/providers.yml` and already has
`resolveProviderDescriptor root "rendering"`; resolving the name key from that descriptor keeps
generic SDD free of any `productName`/rendering literal (spec FR-006). `resolveNameParameter`
falls back to the contract default `"name"` when a provider declares none, so the builder is
safe against a provider without a declared name parameter.

**Alternatives considered**:
- *Rename the temp product root to `Roquelike-DungeonCrawler`.* Rejected — scaffold does not
  derive the product name from the directory basename (verified: 080 FR-004 uses the forwarded
  `nameParameter`), so this would not exercise the derivation.
- *Hardcode `"productName"` in the acceptance.* Rejected — embeds provider identity in generic
  SDD, violating FR-006 and the constitution's "no Rendering knowledge in generic SDD".

## D2 — Asserting `dotnet build` AND `dotnet test`

**Decision**: Reuse the existing bounded process-shell edge `runToCompletion "dotnet" [...]
root 300_000`. Build via the existing `buildProbe` (declared-or-default) path; add a
`testProbe` that runs `dotnet test` at the product root under the same 300 s cap. A `pass`
requires both exit 0; a non-zero build fails the fact naming the build diagnostic; a non-zero
test fails naming the test diagnostic.

**Rationale**: The existing lane already builds and *runs* (a headless start smoke) but never
runs the product's own tests, and never with an illegal-identifier name — the exact gap #150 /
080-FR-011 names. `runToCompletion` is the shared bounded edge the build probe already uses;
reusing it keeps the timeout-kill semantics and avoids a new abstraction (Principle IV). An
empty-but-green test run (exit 0, zero tests) satisfies FR-002 — the fact proves the produced
test project *compiles and runs*, not a minimum test count (spec Edge Cases).

**Alternatives considered**:
- *Reuse the `runProbe` grace-window semantics for `dotnet test`.* Rejected — `test` is a
  run-to-completion command with a meaningful exit code, not a long-lived process to start-and-
  kill; `runToCompletion` is the right edge.
- *Assert build only (drop test).* Rejected — 080-FR-011 and #150 explicitly require build
  **and** test.

## D3 — Gating and honest skip (reuse, do not re-invent)

**Decision**: Tag the new fact `[<Trait("kind","composition-acceptance")>]` +
`[<RequiresRegistryFact>]` and resolve its verdict through the existing verdict resolution
(`Pass` / `SkipUnavailable` / `Fail`). Do not add a new gating mechanism.

**Rationale**: `RequiresRegistryFact` already statically skips at discovery when
`FSGG_SDD_ACCEPTANCE_REGISTRY` is unset/empty, so the offline inner loop stays green and touches
no network (spec FR-004). The existing `unavailable provider resolves to skip-unavailable`
posture is exactly the behavior FR-005 requires; keying the new fact's verdict on the same
`(outcome, diagnostic)` resolution means a transient provider outage never fails SDD. The
`--filter "kind=composition-acceptance"` in `composition-acceptance.yml` selects any fact
carrying that trait, so the new fact runs on schedule/dispatch/manual with **no YAML change**
(spec FR-007) — a fact to verify, not a change to make.

## D4 — Regression demonstration (the guard actually fences C1)

**Decision**: The fact must go **red** if feature 080's derivation is reverted (raw hyphenated
name templated into identifier contexts) — proven by the build probe failing with the
`FS0010` hyphen-in-namespace class of error. Documented as a manual verification step in
quickstart (SC-002); not wired as an automated negative test (it would require mutating product
source).

**Rationale**: SC-002 is the whole point — a guard that cannot fail on the regression it
guards is theatre. The build-probe failure on a reverted derivation is the demonstrable fence.

## D5 — Neutrality invariant coverage

**Decision**: Add an always-on offline companion fact asserting the new request forwards the
descriptor-resolved name **key** with the hyphenated **value** and no rendering token; extend
the existing `acceptance project carries no Governance reference` scan set if the new fact's
request builder lands in a scanned file.

**Rationale**: Slice A rides on FR-006 (provider-neutrality). An always-on offline companion
makes that invariant a first-class, PR-visible assertion rather than something only observable
on the nightly lane. Mirrors the existing `the fixed composition request passes no explicit
starter parameter` offline companion pattern.
