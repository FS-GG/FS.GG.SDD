# Phase 0 Research: Scaffold lifecycle-parameter pass-through & app-only provenance

All "NEEDS CLARIFICATION" from the spec's Technical Context resolve here. The feature is
verification-only; the research questions are about *how to prove* the existing behavior
deterministically and *how to enforce* the no-leak invariant robustly — not about new
implementation choices.

## Decision 1 — How to prove "lifecycle is forwarded verbatim" without a mock

**Decision**: Prove it at **two levels**, both through the public scaffold surface.

- **Plan level (set & order)** — drive the MVU loop in `--dry-run` and inspect
  `model.PendingEffects` for the create process `RunProcess("dotnet", args, _)` where
  `List.contains "-o" args`. Assert the `-p:` segment equals exactly the
  `key=value` overlay (defaults ⊕ author `--param`) and nothing else. This is the same
  real-effect surface `ScaffoldCommandTests.fs:74-78` already inspects — it is *not* a mock
  of an internal stage; it is the actual planned child-process arg vector.
- **End-to-end (verbatim arrival)** — run for real against a **recording fixture** whose
  `dotnet new` template declares a `lifecycle` symbol with
  `"replaces": "LIFECYCLE_VALUE"` and produces `scaffold-manifest.txt` containing
  `lifecycle=LIFECYCLE_VALUE`. After `--param lifecycle=sdd`, read the produced manifest and
  assert it contains `lifecycle=sdd`. The value can only appear if SDD passed
  `-p:lifecycle=sdd` to the child verbatim.

**Rationale**: The plan-level check gives exact *set equality / no add-drop-rename /
order-independence* (FR-003, FR-008) deterministically and offline. The end-to-end echo
gives real-evidence proof (FR-009, constitution VI) that the value survives the real
process edge — closing the gap a plan-only assertion would leave (it would not catch an
edge-interpreter bug that mangles args).

**Alternatives considered**:
- *Echo-only*: insufficient for "set equality / no extra param" — a produced manifest
  proves `lifecycle` arrived but not that nothing else was added/dropped.
- *Plan-only*: insufficient for FR-009's real-process-edge evidence; would miss a real
  `runProcess` defect.
- *A custom recording wrapper replacing `dotnet`*: rejected — it would mock the edge and
  violate "real filesystem/process fixtures, not mocks of internal stages" (FR-009).

## Decision 2 — Order-independence is structural, but still asserted

**Decision**: Assert parameter-order independence (FR-008, SC-001) explicitly, while noting
it holds *structurally*: `effective` is a `Map<string,string>` (`HandlersScaffold.fs:84-96`)
and the create args are built via `Map.toList` (`:175-178`), which yields keys in canonical
sorted order regardless of author `--param` ordering. The test supplies `--param` in two
different orders and asserts the planned `-p:` vector is identical.

**Rationale**: Structure guarantees it today, but the assertion pins the guarantee against
a future refactor that might switch to an order-preserving list. Cheap insurance for SC-001.

**Alternatives considered**: Trusting the `Map` invariant silently — rejected; an
unasserted invariant is one refactor away from a silent regression.

## Decision 3 — App-only provenance proof: precision *and* recall

**Decision**: After a successful `lifecycle=sdd` run, assert three set relations:
1. `provenance.producedPaths ⊇`/`⊆` the files the fixture actually created (exact equality —
   precision and recall both 100%, SC-003), computed by diffing the target tree against the
   known SDD skeleton.
2. Every produced path carries `owner = generatedProduct` (SC-002).
3. `provenance.producedPaths ∩ skeletonPaths = ∅`, where `skeletonPaths` is the set written
   by `initEffects` (`.fsgg/`, `work/`, `readiness/`, `AGENTS.md`, `CLAUDE.md`) — using the
   existing `isSddOwned` notion (`HandlersScaffold.fs:52-60`).
4. **Init byte-identity** (FR-005): run a plain `init` into a separate temp dir and assert
   each skeleton file written by the `lifecycle=sdd` scaffold is byte-identical to init's.

**Rationale**: The board item's second claim is "provenance records app-only paths." Recall
(no app path missing) and precision (no skeleton path laundered in) are independent failure
modes; both must be pinned. Init byte-identity is the cleanest proof the skeleton is
established "unchanged."

**Alternatives considered**: Asserting only that skeleton paths are absent (precision only)
— rejected; misses the recall failure where the provider's files are under-recorded.

## Decision 4 — Leak-invariant scan: identifiers by grep, value-semantics by behavior

**Decision**: Enforce US3 / FR-007 with three complementary mechanisms:

1. **Identifier deny-list grep** (extend `ScaffoldGuardTests.fs:12`): generic SDD `src` and
   the generic-contract tests contain none of the provider-specific tokens (`fs-gg-ui`,
   `FS.GG.Rendering`, plus any provider name / docs URL). Offenders are reported as
   `"{path}: {token}"` (location named — SC-005).
2. **Scoped `lifecycle`-literal scan**: assert the literal token `lifecycle` does **not**
   appear in the **scaffold source path** — the files that handle the parameter
   (`HandlersScaffold.fs`, and the scaffold branches of `CommandSerialization.fs` /
   `CommandRendering.fs` / `CommandReports.fs`). Those files own no lifecycle vocabulary, so
   a `lifecycle` literal there can only mean someone special-cased it. This scan is **not**
   applied repo-wide, because "lifecycle" is core SDD vocabulary elsewhere
   (`nextLifecycleCommand`, lifecycle-stage names, the constitution).
3. **Behavioral value-agnosticism test**: run the recording fixture with an *arbitrary*
   lifecycle value (e.g. `lifecycle=zzz-nonce`) and assert the forwarded arg vector, outcome,
   and provenance shape are identical (modulo the echoed value) to the `lifecycle=sdd` run —
   proving SDD branches on *no* lifecycle value, not just `sdd`.
4. **Automated planted-violation proof**: a unit test feeds the scan's offender-detection
   logic a synthetic in-memory source string containing a planted rendering identifier (and
   one containing a planted `lifecycle` literal) and asserts the scan returns a non-empty,
   located offender list — so SC-005's "demonstrably fails when planted" is itself automated,
   not a manual procedure.

**Rationale**: A repo-wide grep for lifecycle *values* (`sdd` / `spec-kit` / `none`) is
unreliable: `sdd` is a substring of `fsgg-sdd`/`FS.GG.SDD`, and `none` is ubiquitous
(`None` successor, `(none)` projection text). Scoping the *literal-key* scan to the scaffold
source — which legitimately has zero lifecycle vocabulary — makes it precise and robust,
while the behavioral test covers "any other lifecycle value" far more strongly than any grep
could. The planted-violation unit test converts SC-005's negative requirement into a
standing automated guarantee.

**Alternatives considered**:
- *Repo-wide value grep for `sdd`/`spec-kit`/`none`*: rejected — false positives against
  core SDD vocabulary make it un-shippable.
- *Manual "plant and observe" procedure*: rejected — not enforced on every build (FR-007
  says "enforced invariant scan … MUST fail the build").
- *AST/Roslyn analysis to detect value comparisons*: rejected — disproportionate complexity
  (constitution IV) for a guarantee the behavioral test already provides.

## Decision 5 — Recording fixture must declare the `lifecycle` symbol

**Decision**: Add a dedicated recording fixture template (`tests/fixtures/scaffold-provider/
lifecycle/`) that declares both `productName` and `lifecycle` as `dotnet new` symbols. Do
**not** reuse the existing `ok` fixture: it declares only `productName`, and passing
`-p:lifecycle=sdd` to a template that does not declare the symbol makes `dotnet new` fail or
warn — which would conflate "param not forwarded" with "template rejects unknown param".

The fixture and its registry use only neutral identifiers (`fsgg-fixture-*`, `__FIXTURE__`
token) — never a Rendering package id, template id, or docs URL (FR-001, SC-005). It is the
rendering-agnostic stand-in for the real Rendering provider, exactly as the existing
fixtures stand in for it in 030.

**Rationale**: A clean, declared `lifecycle` symbol both (a) makes the happy path succeed and
(b) provides the substitution channel for the verbatim-echo proof. Keeping it separate from
`ok` avoids disturbing existing 030 scenarios.

**Alternatives considered**: Teaching `ok` an optional `lifecycle` symbol — rejected; it
would touch 030's golden expectations and blur which fixture proves what.

## Decision 6 — Edge-case fixtures: declare `lifecycle`, reuse the existing guards

**Decision**: For the FR-008 edges, bind them to `lifecycle=sdd`:
- **Required-but-missing**: `lifecycle-required.providers.yml` marks `lifecycle` `required:
  true`; omitting it must hit the existing `scaffold.providerParamMissing` (exit 1) *before*
  any provider invocation.
- **Empty product**: a `lifecycle-empty` fixture that declares `lifecycle` but produces no
  files → existing `scaffold.providerEmpty` / `ProviderSucceededEmpty` (exit 0), empty
  provenance produced set.
- **SDD-tree intrusion**: a `lifecycle-intrusion` fixture that declares `lifecycle` and
  writes into `.fsgg/`/`work/`/`readiness/` → existing `scaffold.providerWroteSddTree`
  (exit 2), and those paths are **not** laundered into provenance as app-only.

**Rationale**: The edges are existing guarded behaviors; the feature's job is to prove they
still fire correctly when `lifecycle=sdd` is in play (the org-roadmap shape). Each edge needs
a fixture that *declares* `lifecycle` so the param is accepted up to the point the guard
fires.

**Alternatives considered**: Reusing `empty`/`writes-into-fsgg` directly with a `lifecycle`
param — rejected for the same unknown-symbol reason as Decision 5. (If `dotnet new` is
confirmed to tolerate unknown `-p:` params for these, the variants collapse to new registries
only; resolved during implementation and noted in tasks.)

## Decision 7 — Production code is not expected to change

**Decision**: Treat this as verification-only. If — and only if — a scenario fails against
current behavior, the failure is a genuine defect; the fix stays inside the existing scaffold
contract (no new public surface), and this section is amended to record the defect, the
corrective change, and the `.fsi`/baseline/golden follow-through (none anticipated).

**Rationale**: The behaviors under test already exist and are exercised generically by 030's
suite; this feature pins them to the specific `lifecycle=sdd` shape and adds the leak guard.
FR-010 mandates that any corrective change remain within the existing contract and be called
out explicitly — this decision is that standing call-out.

## Decision 8 — Defect surfaced & corrected: parameter-forwarding arg form (`-p:k=v` → `--k v`)

**Status**: This amends Decision 7 — the anticipated-but-unlikely contingency fired.

**Defect**: The pre-change scaffold forwarded each effective `--param key=value` to the
provider as `dotnet new <id> -o . -p:key=value` (`HandlersScaffold.fs:175-178`,
`plannedCreateCommand`). In SDK 10 `dotnet new` has **no** MSBuild-style `-p:k=v` passthrough:
each declared template symbol is exposed as a `--<symbol>` option, and `-p` is merely the
*auto-generated short alias of the first parameter*. So `-p:productName=Acme` parsed as the
`-p` (productName) option with the literal value `productName=Acme` — substituting
`PRODUCT_NAME` with `productName=Acme` rather than `Acme` — and **two** `-p:` args
(`-p:lifecycle=… -p:productName=…`) failed outright (`Option '-p' expects a single argument
but 2 were provided`, exit 127). The behavior was never caught because the 030 `ok` fixture
tests assert only the *produced path set*, never the *substituted file content*; this
feature's verbatim-arrival assertion (T010, reading `scaffold-manifest.txt`) is the first test
to exercise real forwarding, and it exposed the defect.

**Why it is a genuine defect (not a test artifact)**: FR-002/US1 require that
`--param lifecycle=sdd` reach the provider **verbatim**. Under `-p:k=v` it did not reach the
provider at all for multi-param providers, and reached it mangled for the single-param case.
The verification is correct; the production forwarding was wrong.

**Corrective change (within the existing scaffold contract)**: forward each effective param as
a verbatim `--<key> <value>` pair (`HandlersScaffold.fs` `parameterArgs` and the
`plannedCreateCommand` hint). No new public surface, no `.fsi`, no schema, no provenance, no
projection, no diagnostics change. The `NextActionHint` hint string changes from
`… -p:productName=Acme` to `… --productName Acme` (still contains `dotnet new <templateId>`).
No `PublicSurface.baseline` or golden file is affected (verified: scaffold has no golden
snapshot file; the four baselines are unchanged — T026). All 030 scaffold tests stay green.

**Contract docs updated to match reality**: `contracts/forwarding-invariant.md` (the forwarded
create-arg vector) and the inventory rows in `plan.md` are amended from `-p:k=v` to `--k v`.
The *invariant* (forwarded set == `effective`, verbatim, order-independent, value-agnostic) is
unchanged — only the wire form of each pair.

## Decision 9 — C2 scoped scan refined: literal-`lifecycle` → collision-free value token `spec-kit`

**Status**: Refines Decision 4 / `contracts/leak-invariant-scan.md` C2 after implementation
found the original scan premise false.

**Finding**: C2 as drafted scanned the curated scaffold-source union for the literal token
`lifecycle`, on the premise "those files own no lifecycle vocabulary." That premise is
empirically false on the shipped, clean tree:
- `HandlersScaffold.fs` uses `lifecycle` as ordinary SDD vocabulary — the staged MVU driver
  `nextLifecycleEffects` and the success hint `"… begin the lifecycle at charter."`
- `CommandSerialization.fs` / `CommandRendering.fs` serialize `lifecycleStageReadiness` (the
  **ship** command's field), and `CommandReports.fs` is dense with lifecycle-stage prose.

A case-insensitive literal-`lifecycle` scan therefore false-positives on clean code; it cannot
be made green honestly. The lifecycle **values** also collide exactly as Decision 4 predicted:
`"sdd"` collides with an ownership value (`Ownership = "sdd"`, CommandReports.fs) and `"none"`
with None-rendering (`| None -> "none"`, Cli/Rendering.fs). The **only** collision-free
lifecycle-value token is `spec-kit`.

**Refinement**: C2 scans the curated scaffold-source union for the collision-free
lifecycle-value token **`spec-kit`** — the canonical "other" value whose presence would prove a
value was special-cased — rather than the literal `lifecycle`. The comprehensive guarantee
against branching on **any** lifecycle value remains **C4** (behavioral value-agnosticism,
T014), which a static grep cannot match; C2 is the cheap static backstop, and C3 (planted-proof)
now plants a `spec-kit` literal to prove the scan bites. C1 (identifiers) and the curated scope
are unchanged. This keeps FR-007 / US3 fully discharged (C1 + C2 + C3 + C4).

**Contract updated**: `contracts/leak-invariant-scan.md` C2/C3 rows amended accordingly.

## Resolved unknowns

| Spec/Template unknown | Resolution |
|---|---|
| Testing framework/edge for the fixture | xUnit + real local `dotnet new` template via the existing process edge (`CommandEffects.fs:68-110`); Decision 1, 5. |
| How "verbatim" is asserted | Two-level: planned create-arg vector (set/order) + echoed manifest (arrival). Decision 1. |
| How "no lifecycle-value special-casing" is enforced | Scoped literal scan + behavioral value-agnosticism + planted-violation unit test. Decision 4. |
| Whether new schema/artifact/surface is needed | None (FR-010 / SC-007). Verification + fixtures only. |
| Whether agent surfaces change | No — no workflow change. Constitution VII satisfied trivially. |
| Whether production code changes | Not anticipated; Decision 7 governs the contingency. |
