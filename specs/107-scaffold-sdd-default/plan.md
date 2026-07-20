# Implementation Plan: `sdd` as the Default Lifecycle at the SDD Scaffolder, and the Spec-Kit→SDD Migration Path

**Spec**: `specs/107-scaffold-sdd-default/spec.md` · **Item**: FS.GG.SDD#597 · **Epic**: `.github#1245`
(ADR-0056) · **Contract**: `scaffold-provider` (v1)

## Architecture

The load-bearing discovery is that **the scaffolder default already exists, value-agnostically**. The
provider-declared default is forwarded verbatim today:

```fsharp
// src/FS.GG.SDD.Commands/CommandWorkflow/HandlersScaffold.fs:96
let effectiveParameters (descriptor: ProviderDescriptor) (request: CommandRequest) =
    ...
    |> List.choose (fun spec -> spec.Default |> Option.map (fun value -> spec.Key, value))
    // ...then overlaid by author `--param` overrides (author wins), recorded as EffectiveParameters
```

So a `lifecycle` parameter the provider declares with `default: sdd` **already** flows into
`effectiveParameters` and `.fsgg/scaffold-provenance.json` with **no `sdd` literal anywhere in SDD**. The
behavior ADR-0056 wants at the `fsgg-sdd scaffold` surface is delivered by the provider flip (`.github
#1246`) *plus this already-present forwarding*. This feature therefore adds **no scaffold code path** — it
adds a **regression-proof witness** that the forwarding cannot silently break, and it authors the
migration guide.

| Layer | Project / path | Adds | Why here |
|---|---|---|---|
| Mechanism witness | `tests/…` (scaffold handler tests) | Synthetic-provider tests pinning `lifecycle` default forwarding + override precedence | The forwarding is the contract behavior ADR-0056 relies on; pin it so a future edit to `effectiveParameters` reddens (defect class #266 — a gate that cannot fail). |
| Migration guide | `docs/` (new `spec-kit`-lane→`sdd`-lane guide, sibling to `docs/migration-from-spec-kit.md`) | The `upgrade`-re-supply / re-scaffold move, additive + non-destructive | SDD owns the lifecycle-lane migration narrative; the tool primitive (`upgrade` `artifactReSeed`) already exists (`docs/reference/doctor-upgrade.md`). |

Nothing else moves: **no `.fsi` change** (the `ProviderDescriptor`/`effectiveParameters` surface is
unchanged), **no `scaffold-provenance` schema bump** (stays v1), **no `scaffold.providerMissing` relaxation**
(`--provider` stays required), and **no `sdd`/`spec-kit` literal enters generic SDD**.

### Why no SDD value embed — and why that is the design, not a gap (FR-001 / FR-004)

Adding an `sdd` default *inside* SDD would re-couple generic scaffold to a provider value, the exact
inversion `scaffold` FR-002 / SC-005 forbids. The contract's answer is that lifecycle is a
**provider-declared parameter**; the flip is authored once at the provider and forwarded. "Scaffolder
default" and "template default flip" are ADR-0056 naming the *same* provider default observed at two
surfaces (`fsgg-sdd scaffold` and raw `dotnet new`), not two independent value writes. This feature pins
the SDD half of that single mechanism.

### Sequencing — publish-before-flip (ADR-0037 over `scaffold-provider`)

```
.github#1246 (Templates)                 this item (SDD #597)
flip fs-gg-ui template.json              PR-1 (unblocked NOW): this spec/plan
lifecycle.defaultValue → sdd      ┐      + mechanism witness (AC-001..003, synthetic provider,
publish provider version          │        no real provider, no `sdd`-value dependency)
                                  │      + migration guide (AC-005)
                                  └────▶  PR-2 (gated on #1246 publish):
                                          end-to-end value witness AC-004
                                          (override-free `fs-gg-ui` scaffold → lifecycle: sdd)
```

The **value** witness (an override-free real-`fs-gg-ui` scaffold records `lifecycle: sdd`) is asserted
only *after* the flipped provider version publishes — otherwise SDD would carry an `sdd` expectation ahead
of the provider that owns it, violating publish-before-flip. Everything in PR-1 is value-agnostic (a
synthetic provider proving *forwarding*, whatever the value) and lands now; the CLI is not re-published
for PR-1 because no behavior changed, only test coverage and docs.

## Verification plan

The failure legs are the point — a gate that cannot fail is this repo's recurring defect class (#266).
Each row is **mutation-checked**: break the named mechanism, the test reddens.

| # | Test | Pins | Mutation that reddens it |
|---|---|---|---|
| 1 | Synthetic provider `lifecycle` default `sdd`, no override → `effectiveParameters` has `lifecycle=sdd` | AC-001 / FR-002 | drop the `spec.Default` fold in `effectiveParameters` |
| 2 | Same, default `spec-kit` → `lifecycle=spec-kit`; `--param lifecycle=none` → `lifecycle=none` | AC-002 / FR-002 / FR-003 | hard-code any lifecycle value (proves value is forwarded, not chosen); break override overlay |
| 3 | `--provider` omitted → `scaffold.providerMissing`, exit 1 (regression witness) | AC-003 / FR-004 | introduce a default provider |
| 4 | `scaffold-provenance.json` still `schemaVersion: v1` with the new run | FR-004 | any schema bump |
| 5 *(PR-2, gated)* | Override-free real `fs-gg-ui` scaffold → `lifecycle=sdd` | AC-004 | provider flip regressed, or forwarding broke |
| 6 | Doc-lint / link check over the new migration guide passes; grep asserts no provider literal | AC-005 / FR-006 | add a provider package/template/path/URL literal |

Driven, not just asserted: PR-1's body records the real `fsgg-sdd scaffold` run against a synthetic
provider showing `lifecycle` forwarded verbatim, and (once unblocked) PR-2's body records the real
`fs-gg-ui` end-to-end `lifecycle: sdd` verdict.

## Open questions for clarify

- **Doctor guard scope.** ADR-0056's "fail-closed readiness/doctor check on the `sdd` lane" is described
  for the raw-`dotnet new` standalone consumer (template-side notice). Whether `fsgg-sdd doctor` should
  *additionally* fail-closed on an `sdd`-lane tree lacking the SDD skeleton is deferred to clarify — an
  `fsgg-sdd scaffold`-produced `sdd`-lane tree is never lifecycle-less, so the guard may be entirely
  template-side. Resolve before plan is final.
- **Migration guide home.** New standalone doc vs. a lane-migration section folded into
  `docs/migration-from-spec-kit.md`. The scenarios differ (additive-adopt vs. lane-move), which argues
  for a sibling doc; confirm at clarify.

## Release

**PR-1 ships no CLI behavior change** — only tests and docs — so no `Directory.Build.local.props` version
bump and no publish. **PR-2**, if it adds a real behavior/test that must ship, and only if a publish is
actually warranted, is sequenced after `.github#1246`. The `spec-kit`-lane removal and its `Target` date
remain out of scope and downstream.
