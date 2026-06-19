# FS.GG.Governance

Optional rule, evidence, and route-explanation tooling for the
[FS-GG](https://github.com/FS-GG) projects, built as a normal F# tool product with
standard [Spec Kit](https://github.com/github/spec-kit).

**In one sentence:** governance is a *pure inference kernel* over typed facts and
rules, where every rule declares **who is competent to decide it** (machine, agent,
or human), every rule's check is **reified data** that can be evaluated, rendered,
hashed, and explained from one source, and enforcement is **light and advisory by
default** with a loud, local-only escape hatch.

The kernel is domain-neutral: what changes between governing F# code, an essay, or a
research project is the *fact vocabulary* — the inference, arbitration, evidence, and
rule language stay the same. See the [design overview](docs/governance-design/index.md).

## Operating rule

> Governance tooling may *inspect* rendering; rendering must never *require*
> governance tooling to build, test, document, package, or release.

Generic code here must not assume any consumer's package IDs, template names, target
names, or directory layout. Rendering is one external customer, not this tool's shape.

## Architecture

```text
FS.GG.Governance.Kernel   pure, BCL-only — the inference core (M1, done)
  ├─ facts · rules · fixed-point · provenance              (F01)
  ├─ verdicts + Kleene three-valued logic                  (F02)
  ├─ reified Check algebra + eval/render/hash/explain       (F03)
  ├─ CheckTier arbitration + rule bridge + review cache key  (F04)
  ├─ evidence model + synthetic-taint over a DAG            (F05)
  ├─ JSON explanation + evidence-freshness                  (F06)
  └─ routing: Stakes / Severity / RunMode / Route           (F07)

FS.GG.Governance.Host     effects shell (I/O) — sense → plan → act (F08, done)
FS.GG.Governance.Adapters.Spi      adapter SPI + lift/compose      (F09, done)
FS.GG.Governance.Adapters.SpecKit  first concrete adapter — Spec Kit as data (F10, done)
FS.GG.Governance.Adapters.DesignSystem  second adapter — a design language as data (F11, done)
FS.GG.Governance.Cli     optional route/explain/contract/evidence tool (F12, done)
FS.GG.Governance.Adapters.*             external validation          (F13, planned)

Capability platform continuation (F14-F27, planned):
  .fsgg schemas, capability catalog, git/CI facts, gate registry,
  ship/verify/release JSON contracts, native SDD bootstrap, normalized work model,
  generated-view refresh, product/package/docs/skills/design checks,
  cost/freshness cache, command provenance, and human report projections.
```

The kernel is a pure, **zero-dependency** forward-chaining (Datalog-style,
stratified-monotonic) reasoner: `FixedPoint.evaluate identify rules supplied` returns
the least fixed point of the facts under the rules, with provenance for every derived
fact. All I/O lives at the edge in `Host` (functional core / imperative shell).

> **Kernel precondition (documented, not runtime-enforced).** Rules must be
> **monotonic** (add-only); negated or aggregated facts are *supplied* from a lower
> stratum, never derived in the same fixed point. See [the kernel](docs/governance-design/kernel.md).

## Status

| Milestone | Scope | State |
|---|---|---|
| **M1** | Pure kernel + evidence + explanation (F01–F06) | ✅ Reached |
| **M2** | Light routing + effects edge (F07–F08) | ✅ Reached |
| **M3** | Adapter SPI + two domains (F09–F11) | ✅ Reached — F09 (SPI) + F10 (Spec Kit) + F11 (design system) done |
| **M4** | CLI + external validation (F12–F13) | In progress — F12 CLI done |
| **M5** | Capability catalog + protected ship skeleton (F14–F17) | Planned |
| **M6** | Policy truth tables + native SDD model (F18–F20) | Planned |
| **M7** | Readiness + generated-view currency (F21–F22) | Planned |
| **M8** | Generated-product and surface-domain checks (F23–F24) | Planned |
| **M9** | Cost/cache/provenance + release gates (F25–F26) | Planned |
| **M10** | Human projections over stable reports (F27) | Planned |

F01–F12 are implemented (CLI tests included). The kernel and CLI pack to
`~/.local/share/nuget-local/`; the `Host` effects edge depends on it (zero new dependency).
F10 is the **first concrete production adapter** — it governs this repository's own Spec Kit
workflow as data, supplying only its five SPI components and reusing 100% of the kernel
(pure: no I/O, no new dependency; depends on the F09 SPI, never the reverse). F11 is the
**second** — it governs adherence to a **design language** (Ant Design as the worked example)
from a fixture token tree, adopting the kernel **by difference**: it shares none of F10's shape
(no phase, no `whenPhase`, no merge fence, no dial), proving the SPI sits at the right altitude.
Its faithful lift is proven by composing it alongside the **real** F10 adapter at one root.
F12 adds the optional `fsgg-governance` .NET tool. It exposes `route`, `explain`,
`contract`, and `evidence`, keeps command orchestration behind a CLI MVU boundary,
and inspects governed roots read-only. Fresh agent reviews are cache-only by default;
nonzero `--review-budget` records an attempted dispatch but no fake passing verdict.
The 2026-06-18 capability-design report is now incorporated into the implementation
plan as F14-F27, with checkbox progress tracking for the protected ship gate,
native SDD flow, generated views, surface checks, release gates, and provenance work.

## CLI

Build and run from source:

```bash
dotnet build src/FS.GG.Governance.Cli
dotnet run --project src/FS.GG.Governance.Cli -- route --root . --mode inner
dotnet run --project src/FS.GG.Governance.Cli -- evidence --root . --json --review-budget 0
```

Install from the local feed:

```bash
dotnet pack src/FS.GG.Governance.Cli -c Release -o ~/.local/share/nuget-local
dotnet tool install FS.GG.Governance.Cli --tool-path .tmp/f12-tool --add-source ~/.local/share/nuget-local
.tmp/f12-tool/fsgg-governance route --root . --mode inner
```

## Design lineage

The checker paradigm follows [Cedar](https://cedarpolicy.com/en) (and OPA/Rego):
**policy as analyzable data**, **forbid-trumps-permit** order-independent precedence
(the F07 routing layer), and decisions that are **explainable by construction**.
Planning and optimization are deliberately *not* native — the kernel checks a
planner's outputs at the edge rather than being one. It is **not** Cedar and does not
depend on it; Cedar is a reference for the evaluation semantics. See
[theory & composition](docs/governance-design/theory-and-composition.md) and
[scope: planning & optimization](docs/governance-design/planning-and-optimization.md).

## Design & plans

- [Design overview](docs/governance-design/index.md) — start here; the comprehensive design
  - [The theory of the rule engine](docs/governance-design/rule-engine-theory.md) — the connected, textbook story
  - [Goals & principles](docs/governance-design/principles.md) · [the kernel](docs/governance-design/kernel.md) · [the rule eDSL](docs/governance-design/rule-edsl.md)
  - [Routing, severity & run modes](docs/governance-design/routing-and-modes.md) · [domain adapters](docs/governance-design/adapters.md) · [Spec-driven development in the system](docs/governance-design/speckit-in-the-system.md)
- [Implementation plan (Spec Kit, F01–F27)](docs/2026-06-18-governance-kernel-speckit-implementation-plan.md) — the design and capability report decomposed into ordered features with progress checkboxes
- [Capability design report](docs/reports/2026-06-18-233718-fsgg-governance-capability-design.md) — product-neutral capability envelope and protected-boundary roadmap
- [Feature specs](specs/) · [decision records](docs/decisions/)

## Workflow

Standard Spec Kit with the
[`fsharp-opinionated`](https://github.com/EHotwagner/speckit-fsharp-tooling) preset:
specify → plan → tasks → implement, via the `/speckit-*` skills. Visibility lives in
`.fsi` signatures with per-module surface-area baselines; there is no evidence-audit
or DAG-validation machinery — see the
[constitution](.specify/memory/constitution.md).

## License

[MIT](LICENSE)
