# Phase 0 Research: Accept 4-Segment Versions in the Registry Validator

Resolves the Technical Context unknowns and the deferred grammar-expression decision (spec
Assumptions). Each decision is recorded as Decision / Rationale / Alternatives.

## Current state (as found)

- **The grammar.** `src/FS.GG.Contracts/Registry.fs:226-227` defines the private real-schema version
  grammar:

  ```fsharp
  let private semVerRegex =
      System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$")
  ```

  with `bareIntegerRegex = ^\d+$` (line 229) for `1`/`2`, and a separate permissive
  `rangeRegex = ^[\d.xX*\s<>=~^|.-]+$` (line 232) for the `range` field. `isValidVersion`
  (line 234) = `semVerRegex || bareIntegerRegex`.
- **The path the canonical file uses.** `validateDocument` (line 242) validates contract `version`
  (line 311) and optional `package-version` (line 346) **only** through `isValidVersion`
  (→ `semVerRegex`), and `range` through `isValidRange`. It does **not** call the legacy
  `tryParseSemVer`. Verified by reading the `validateDocument` body (lines 242-357).
- **The legacy path.** `validate` (line 140) over the pre-042 `RegistryModel` uses `tryParseSemVer`
  (line 154) — a 3-field `SemVer` record parser also used by the legacy range comparators. It is a
  separate model, not the canonical-document path.
- **The Python authority.** `FS-GG/.github` `scripts/validate-registry.py:38`:

  ```python
  SEMVER_RE = re.compile(r"^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$")
  ```

  It **already** carries the optional 4th numeric segment `(?:\.\d+)?`. The typed grammar is the side
  that diverged (spec Assumption confirmed).
- **The concrete case.** `governance-reference-gate-set` is genuinely `1.2.1.1` (ADR-0007: four
  contained `schemaVersion`s `{gov}.{caps}.{policy}.{tooling}`), the real
  `FS.GG.Governance.ReferenceGateSet` package version. The local test fixture
  `tests/fixtures/registry/dependencies.yml` does **not** yet contain this contract.
- **Versioning.** `FS.GG.Contracts` carries its own `<Version>1.1.0</Version>`
  (`FS.GG.Contracts.fsproj:9`) kept in lockstep with `Fsgg.ContractVersion.value` (`ContractVersion.fs:5`,
  enforced by features 036/042). The `FS.GG.SDD.*` packages and the `fsgg-sdd` CLI share one product-line
  `<Version>0.2.0</Version>` from `Directory.Build.local.props:16`. Both publish through
  `.github/workflows/release.yml` (feature 044), each job packing its own resolved `<Version>` with
  `--skip-duplicate`, gated by an at-least-one-line tag guard. ApiCompat is a non-blocking
  package-validation job (`gate.yml` / `scripts/apicompat-check.sh`) that fails only on a `CP####`
  public-surface break.

## Decision 1 — Grammar expression: extend the existing regex with an optional 4th numeric segment

**Decision.** Change `semVerRegex` (and nothing else in the grammar) to:

```fsharp
let private semVerRegex =
    System.Text.RegularExpressions.Regex(@"^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$")
```

The added `(\.\d+)?` sits **before** the prerelease/build groups, so the optional 4th segment composes
with the existing `-prerelease`/`+build` grammar (FR edge case `1.2.1.1-preview.1` accepted) rather than
being a special case. `bareIntegerRegex` and `rangeRegex` are untouched.

**Rationale.** It is the minimal, idiomatic widening (Constitution IV), stays BCL-only with no new
dependency (FR Decision: the `Fsgg.Registry` no-new-dependency constraint holds), and is the only edit
needed because `validateDocument` routes both `version` and `package-version` through this one predicate.
It is numeric-only and bounded to exactly one extra segment, so `1.2.x.4` (non-numeric), `abc`
(non-version), and `1.2.3.4.5` (5 segments) all still fail (FR-004): the anchored `^...$` plus the single
optional `(\.\d+)?` admits a 4th numeric segment and no more.

**Parity.** F#'s capturing `(\.\d+)?`, `(-…)?`, `(\+…)?` are equivalent to Python's non-capturing
`(?:…)?` for `IsMatch`/`re.match` (capturing vs. non-capturing changes only group extraction, not the
matched language). The resulting accepted language is identical to `validate-registry.py:38` — parity
restored exactly, not further diverged (FR-006, SC-005). See `contracts/version-grammar.md`.

**Alternatives considered.**
- *A hand-written numeric-segment parser instead of regex* — rejected: more code, a new shape to keep in
  parity with a regex authority, no benefit; violates idiomatic simplicity.
- *Reusing `System.Version.TryParse`* — rejected: its accepted language differs (rejects bare integers
  and prereleases, accepts 1–4 segments with different rules), so it would **break** parity with the
  Python authority and regress existing accepted forms.
- *Unbounded `(\.\d+)*`* — rejected: would accept `1.2.3.4.5` and beyond, violating FR-004/SC-002.

## Decision 2 — Scope the widening to the real-schema `semVerRegex`; leave the legacy `tryParseSemVer` unchanged

**Decision.** Widen only `semVerRegex`. Do **not** change `tryParseSemVer` / the `SemVer` record / the
legacy `validate`.

**Rationale.** Three independent reasons: (1) the canonical `registry/dependencies.yml` is validated by
`validateDocument`, which never calls `tryParseSemVer` (verified) — so the legacy parser is not on the
path this feature must fix; (2) the "mirrors `validate-registry.py`" invariant the feature restores is a
property of the *real-schema* grammar only (the comment at `Registry.fs:221-222` scopes parity to this
grammar), so touching the legacy parser is orthogonal to FR-006/SC-005; (3) widening `tryParseSemVer`
would force a 4th `Revision` field onto the `SemVer` record and ripple into `compareSemVer`/range
comparison semantics — scope and risk unjustified by any requirement. The legacy `validate` over
`RegistryModel` keeps its current behavior; if a future need to validate 4-segment versions through that
model arises, it is a separate, deliberate change.

**Alternatives considered.**
- *Widen both for internal consistency* — rejected now as out-of-scope gold-plating (Constitution IV);
  no requirement or caller needs it, and it enlarges the `SemVer`/compare surface.

## Decision 3 — Bump strategy: coordinated `FS.GG.Contracts` patch (1.1.1) + SDD-line patch (0.2.1)

**Decision.** Bump **`FS.GG.Contracts` `1.1.0 → 1.1.1`** (fsproj `<Version>` + `ContractVersion.value`
in lockstep) **and** the **SDD product line `0.2.0 → 0.2.1`** (`Directory.Build.local.props`, with the
`release-readiness.json` / `versioning-policy.md` projections). Publish both in one `release.yml` run.

**Rationale.**
- *Why Contracts must bump.* The change alters `FS.GG.Contracts` **source behavior** (a verdict flips).
  The coherence invariant `source == feed(newest) == registry` (bump checklist) forbids shipping changed
  behavior under the existing `1.1.0` — that would put two different behaviors behind one feed version.
  A bump is mandatory; the question is only the size.
- *Why patch, not major/minor.* Per the versioning-policy change-class table, a public-contract change is
  a public schema/output-shape/CLI-surface change. This touches none: no type, no `.fsi`, no `--json`
  output shape, no command/flag/exit-code (the *verdict* changes for one input class, but the contract
  *shape* does not). It is a defect fix restoring parity → **patch** (`1.1.1`). ApiCompat (`CP####`)
  cannot trip (no public-surface delta), so no major is forced — FR-008 satisfied with margin. (Even read
  as "additive — admits a new input shape," the pre-1.0/SemVer rules keep it non-breaking; patch is the
  conservative, defensible call.)
- *Why the SDD line must also bump.* The gate consumer (#49) runs the published **`fsgg-sdd` tool**, which
  bundles Contracts by project reference. Re-publishing the tool at the existing `0.2.0` is a no-op under
  `nuget push --skip-duplicate`, so the fix would never reach consumers. Bumping to `0.2.1` ships a new
  tool version carrying the fixed Contracts (FR-009, SC-004). It is a patch: no CLI surface change.
- *One run publishes both.* `release.yml` resolves each project's `<Version>` independently and pushes
  each with `--skip-duplicate`; the at-least-one-line tag guard (feature 044) passes when the dispatch
  `version`/tag matches **either** line. Dispatching `version=1.1.1` satisfies the guard and both jobs
  publish their resolved versions (`1.1.1` and `0.2.1`).

**Alternatives considered.**
- *Bump only the CLI line, leave Contracts at 1.1.0* — rejected: breaks the Contracts source==feed
  coherence invariant (changed behavior under an unchanged version).
- *Minor or major Contracts bump* — rejected: overstates the change; no public-contract shape change and
  no apicompat break, so neither is warranted (and major would falsely signal a breaking change,
  violating FR-008's intent).

## Decision 4 — Test & fixture strategy: corpus cases + a real canonical-file end-to-end check

**Decision.** In `tests/FS.GG.Contracts.Tests/RegistryDocumentTests.fs`: add an accepted-case test for
`version: "1.2.1.1"` and `package-version: "1.2.1.1"` (and the edge `1.2.1.1-preview.1`); extend the
existing malformed `[<Theory>]` (currently `1.2.x.4`, `abc`) with `1.2.3.4.5`; and add an end-to-end
assertion that `validateDocument` returns `Valid` over the real fixture
`tests/fixtures/registry/dependencies.yml` **after** that fixture is refreshed to mirror the canonical
`FS-GG/.github` registry, adding the `governance-reference-gate-set@1.2.1.1` contract (with its real
owner/surface/consumers so the fixture stays internally valid).

**Rationale.** FR-007 requires the accepted 4-segment case and a still-rejected over-long/non-numeric
case pinned by tests; SC-002 requires the paired accept/reject demonstration; FR-005/SC-001 require a
"valid over the unmodified canonical file" check, which is only real if the fixture actually carries the
4-segment contract. Refreshing the local fixture from the live `.github` registry (spec Assumption: the
live registry is the source of truth and is consulted at plan time) keeps the in-repo fixture frozen to
the canonical shape (feature 043 fixture-freeze posture). The test is failing-before/passing-after
(Constitution VI): before the regex widening, the fixture yields the two `MalformedVersion` diagnostics
from the spec; after, `Valid`.

**Alternatives considered.**
- *Inline-only fixtures, no real-file refresh* — rejected: would not satisfy FR-005/SC-001's
  "over the canonical `dependencies.yml`" requirement with a real artifact, and would let the local
  fixture drift from the canonical registry it is meant to mirror.

## Decision 5 — Parity is asserted, not assumed

**Decision.** Record the widened grammar and the byte-level equivalence to `validate-registry.py:38` in
`contracts/version-grammar.md`, and (in `tasks.md`) verify on the actual canonical file that the typed
CLI and the Python script return the **same** verdict (both "valid").

**Rationale.** SC-005 / FR-010 require demonstrated agreement on the canonical file so FS-GG/.github#49
can retire the stand-in with no behavioral disagreement. The spec edge case "Parity drift" requires any
residual divergence be surfaced and reconciled rather than silently left; cross-repo, this rides the
`cross-repo-coordination` protocol. No further divergence is expected (Decision 1), but it is verified,
not assumed.

## Resolved unknowns

| Unknown | Resolution |
|---|---|
| Exact grammar expression | Add `(\.\d+)?` to `semVerRegex` (Decision 1) |
| Does the legacy parser need changing? | No — out of the canonical-file path and the parity surface (Decision 2) |
| Does `FS.GG.Contracts` need a version bump? | Yes — patch `1.1.1` (source-behavior change → coherence) (Decision 3) |
| Does the CLI/SDD line need a bump? | Yes — patch `0.2.1` (else `--skip-duplicate` no-ops the republish) (Decision 3) |
| Is a major bump / apicompat break incurred? | No — private regex, no public surface delta (Decision 3) |
| How are both packages published in one run? | `release.yml`, independent per-project versions, at-least-one-line tag guard (Decision 3) |
| Where is the 4-segment case tested? | `RegistryDocumentTests.fs` corpus + refreshed real fixture (Decision 4) |
| How is Python parity guaranteed? | Identical accepted language + verified same verdict on the canonical file (Decisions 1, 5) |
