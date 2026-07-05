# Phase 0 Research: name → valid F# identifier derivation at scaffold

All decisions below resolve the Technical Context and the spec's plan-deferred choices. No
`NEEDS CLARIFICATION` remain.

## D1 — Where the derivation source/sink are declared (contract shape)

**Decision**: Reuse the existing (dead) `ProviderDescriptor.NameParameter` as the **source**
key (the forwarded param carrying the raw product name), and add an additive
`IdentifierParameter: string option` as the **sink** key (the forwarded param that receives the
derived F# identifier). When `IdentifierParameter = None`, scaffold behaves exactly as today
(no derivation) — full backward compatibility.

**Rationale**: The provider template needs **two** symbols — one for identifier contexts
(namespace/module) and one for string/path contexts (the raw name). The contract must name
both. `NameParameter` already exists and defaults to `"name"` but is never consulted; this
activates it as the derivation source. A new optional sink field keeps every existing provider
(which declares neither the sink nor a template symbol for it) working unchanged. The key is
**provider-declared**, so generic SDD embeds no provider-specific value (`030` FR-002).

**Alternatives rejected**:
- *Derive the sink key by suffix convention* (`<nameParameter>Identifier`): couples the sink
  name to the source name and hard-codes a convention SDD invents; a provider-declared field is
  cleaner and self-documenting.
- *Overwrite the name param in place* (spec option "SDD sanitizes in place"): loses the raw name
  in string/path/`.fsproj` contexts — violates #149 and US2. Rejected.
- *Provider owns it via `dotnet new` `sourceName`* (spec option "Provider owns it"): makes the
  guarantee depend on each provider's template sophistication and leaves SDD's #149 deliverable
  empty. Requester chose SDD-derives-and-forwards.

## D2 — The derivation policy (what "valid F# identifier" means here)

**Decision**: A pure `deriveNamespace : string -> Result<string, DerivationError>` in a new
`FS.GG.SDD.Artifacts/FsharpIdentifier` module. Policy, applied **per dot-separated segment**
(dots denote namespace-segment boundaries and are preserved):

1. Drop characters that are not valid in an F# identifier (keep Unicode letters, digits, and
   `_`); this removes hyphens, spaces, and punctuation.
2. If a segment would begin with a digit after step 1, prefix `_`.
3. If a segment collides with an F# reserved keyword, suffix `_`.
4. Empty result for a segment (or the whole name) after step 1 ⇒ **unrepresentable** (D5).

Derivation is a **no-op** on inputs already valid as F# namespaces (idempotent, deterministic,
culture-invariant).

**Rationale**: These are the minimal, language-level rules that make the identifier compile.
Dropping (rather than replacing hyphen with `_`) yields the natural `RoquelikeDungeonCrawler`
form shown to and approved by the requester, and matches how a human would rename. Per-segment
handling preserves legitimate namespaces like `Acme.Foo`.

**Alternatives rejected**: replace-invalid-with-`_` (yields `Roquelike_DungeonCrawler` — valid
but less idiomatic; not what was approved); PascalCase re-casing (over-reach — changes names
that are already valid, breaking D3 no-op).

## D3 — Determinism & no-op guarantee

**Decision**: Pure function, `StringComparison.Ordinal`, no `CultureInfo`-sensitive casing, no
clock/random. Golden test table pins representative inputs → outputs. A separate test asserts
`deriveNamespace x = Ok x` for a set of already-valid names.

**Rationale**: Constitution VI (golden coverage for tool-facing contracts) and spec SC-005.

## D4 — Precedence when the author overrides the sink param (FR-008)

**Decision**: If the author passes `--param <identifierParameter>=<value>` explicitly, the
**author value wins** (verbatim, no derivation). Otherwise SDD computes the derived value and
injects it. Detection: the sink key is present in `request.Parameters` (author-supplied) — if so,
do not derive. This mirrors the existing "author `--param` overrides provider default" rule
(`050`), extended to "author `--param` overrides SDD derivation."

**Rationale**: Least-surprise and consistent with the established parameter-precedence contract.
Recorded in `effectiveParameters`, so the effective value is auditable either way.

## D5 — Unrepresentable name (FR-009)

**Decision**: When `deriveNamespace` returns `Error`, scaffold blocks in `resolveScaffold`
(before any provider invocation) with a new `scaffold.nameUnrepresentable` diagnostic
(`DiagnosticError`, ref `.fsgg/providers.yml` / the offending name), a `NextAction` telling the
author to choose a name containing at least one identifier character, exit class **1**
(user-input), and a not-run summary — no partial provenance claiming success.

**Rationale**: Constitution VIII (distinguish malformed user input from tool defect; fail fast on
the critical path). Only fires when derivation is requested (provider declares
`IdentifierParameter`) and the name is genuinely empty of identifier characters.

## D6 — Recording (provenance/report) stays schema v1 (FR-007)

**Decision**: Both the raw-name param and the derived-identifier param are ordinary entries in
the `effectiveParameters` map, already recorded in `.fsgg/scaffold-provenance.json`
(`EffectiveParameters`, schema v1) and in all three report projections. **No schema bump.** The
injected sink entry appears exactly like any other effective parameter, deterministically sorted.

**Rationale**: Reuses `050`'s effective-parameters surface; keeps provenance additive and v1
(matches the CLAUDE.md guarantee that scaffold provenance stays v1). The derived value is
therefore auditable and reproducible with zero new persisted schema.

## D7 — CI smoke placement (FR-011)

**Decision**: Extend the existing network-gated composition-acceptance test
(`CompositionAcceptanceTests.fs`, `kind=composition-acceptance`, gated on
`FSGG_SDD_ACCEPTANCE_REGISTRY`) to scaffold with a **hyphenated/misspelled** name param against
the real `rendering` provider and assert the existing build probe (`dotnet build`) **and** a test
probe (`dotnet test`) are green. Add a `dotnet test` probe alongside the current build+run probes.

**Rationale**: This lane already builds a scaffolded product against the real provider; it is the
correct home for an end-to-end "compiles + tests" assertion. It self-skips offline, so the
deterministic gate and inner loop stay green and fast (SC-006). It is provider-aware test code
(already `--provider rendering`), so naming the real hyphenated param there does not violate the
generic-SDD constraint (which governs product code, not the provider-specific acceptance test).

**Alternative rejected**: a new always-on CI job that scaffolds — would pull a provider-specific
dependency into the deterministic offline gate. Rejected.

## D8 — Cross-repo sequencing (FR-010)

**Decision**: SDD lands its side independently: it forwards the derived sink param whenever the
provider declares `IdentifierParameter`. A provider that has not yet added the sink template
symbol simply ignores an extra `--param` (harmless). The reference provider **FS.GG.Rendering**
must then (a) add a namespace/identifier symbol to its template used in identifier contexts and
the raw-name symbol in string/path contexts, and (b) declare `nameParameter` + `identifierParameter`
in its published descriptor. Filed as a `cross-repo:request` against FS.GG.Rendering, a
**versioned additive** provider-contract change recorded in the org registry
(`registry/dependencies.yml` + `docs/registry/compatibility.md`), sequenced on the Coordination
board. The acceptance smoke (D7) goes green only after Rendering adopts — which is the concrete
signal that closes the cross-repo loop.

**Rationale**: publish-before-flip / additive-contract discipline from the cross-repo protocol;
SDD stays independently buildable and useful (constitution) while the guarantee completes once the
provider adopts.

## D9 — Agent surface parity (FR-012 / Principle VII)

**Decision**: Update the `fs-gg-sdd-getting-started` scaffold guidance (and any Claude/Codex
command/skill text describing scaffold parameters) equivalently across `.claude`, `.codex`, and
the neutral `.agents` roots wherever the `nameParameter`/`identifierParameter` semantics surface.
No behavior lives only in an agent prompt.

**Rationale**: Constitution VII; CLAUDE.md dual-surface rule and the byte-identical tri-root skill
guarantee.
