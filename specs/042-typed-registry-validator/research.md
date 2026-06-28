# Phase 0 Research: Typed Registry Validator

All NEEDS CLARIFICATION from Technical Context are resolved below. Each decision is
grounded in the real canonical file (`FS-GG/.github` → `registry/dependencies.yml`) and
the Python authority it must match (`FS-GG/.github` → `scripts/validate-registry.py`).

## R1 — Converge the model, or add a parallel real-schema model?

**Decision**: **Additive** — *add* a new `RegistryDocument` model + pure `validateDocument`
to `Fsgg.Registry`, and **keep** the existing `RegistryModel`/`validate` (legacy abstract
model) untouched.

**Rationale**:
- The originating issue explicitly expects an additive change ("No registry version bump
  expected if additive"). Additive keeps the apicompat/PublicApiAnalyzers gate
  (`apicompat-publicapi-gate`) green and forces no SemVer major.
- The only consumer of the legacy `RegistryModel`/`validate` is its own test file
  (`RegistryValidatorTests.fs`); nothing else in any FS-GG repo references it. So keeping
  it costs nothing and avoids churn.
- The real schema (repos vs contracts vs repo→repo edges) cannot be faithfully expressed
  by the legacy `Components/Edges` shape, so a *distinct* type is the honest model anyway.

**Alternatives considered**:
- *Replace/converge (breaking → SemVer major 2.0.0).* Cleaner single-source long-term and
  removes a now-vestigial type, but breaks the published type for no current consumer
  benefit and forces a major bump + registry-pin churn. **Rejected now**; fold the
  legacy-type removal into a future major.
- *Projection onto the existing model* (suggested as an option in the issue). Rejected:
  the real validation is richer than the legacy model can carry (owner∈repos,
  consumers∈repos, duplicate-id, schemaVersion-is-int), so a projection would either lose
  rules or distort the model.

## R2 — Where does the YAML `load` live? (the BCL-only constraint)

**Decision**: The **pure** `validateDocument` lives in `FS.GG.Contracts`; the **YAML
`load`** (file read + parse → `RegistryDocument`) lives at an **edge** in
`FS.GG.SDD.Artifacts`, which already references **YamlDotNet 16.3.0**. A thin
`fsgg-sdd registry validate <path>` command composes them.

**Rationale**:
- `FS.GG.Contracts` is a published **BCL-only leaf** (FSharp.Core-only closure, no
  third-party package) — this is part of its advertised contract surface and is guarded by
  the apicompat gate. Adding YamlDotNet there would violate the contract and the
  constitution's Engineering Constraints.
- Constitution Principle V puts file/parse **I/O at an edge interpreter**, not in a pure
  leaf. `load` is I/O; `validateDocument` is pure. This is the natural split.
- YamlDotNet is already in the repo (`FS.GG.SDD.Artifacts`); reusing it means **no new
  package** anywhere and no hand-rolled YAML subset parser (Principle IV — idiomatic
  simplicity).

**Alternatives considered**:
- *Hand-roll a BCL-only YAML reader inside Contracts.* The file uses block + flow mappings,
  block sequences, folded scalars (`summary: >`), quoted scalars, and inline comments — a
  correct subset parser is real, error-prone work. Rejected in favor of reusing YamlDotNet
  at the edge.
- *A standalone console tool.* Workable, but a `fsgg-sdd` subcommand reuses the existing
  CommandReport projections and the one-contract-for-agents/humans/CI principle (VII).

## R3 — Version grammar (what counts as a valid version?)

**Decision**: Mirror the Python authority exactly. A `version`/`package-version` value is
valid if it matches **either**:
- SemVer-ish: `^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$` — i.e. `major.minor.patch`
  with optional `-prerelease` and `+build` (covers `1.0.0` **and** `0.1.52-preview.1`); **or**
- bare integer: `^\d+$` (covers governance schema versions `1`, `2`).

The optional `range` field (on `governance-handoff`, and any future contract) is validated
permissively (`^[\d.xX*\s<>=~^|.-]+$`) — it legitimately carries `1.x`.
`schemaVersion` must be an **integer**.

**Rationale**: This is exactly what `scripts/validate-registry.py` enforces
(`SEMVER_RE` / `INT_VERSION_RE` / `RANGE_RE`); matching it guarantees no behavioral
disagreement on the canonical file (SC-005). It directly fixes the spec's false-positive
classes: bare-integer (`1`,`2`), `1.x` ranges, **and** the prerelease form
`0.1.52-preview.1` (which the legacy strict-triple parser also rejects — surfaced during
planning, beyond the three the issue named).

**Note**: The legacy `validate`'s comparator/`IncompatibleVersion` range-vs-declared check
is **not** part of document validation — the real file's edges are repo→repo and `via` is
prose, so there is no version-range satisfaction check to run. The `IncompatibleVersion`
rule remains available but is unused by `validateDocument` on the canonical file.

## R4 — Validation rules (parity with the Python authority)

**Decision**: `validateDocument` enforces, per the Python stand-in:

- **Root**: `schemaVersion` present and integer; `repos` a non-empty mapping; each repo
  has non-blank `name` and `role`. (`MissingField` / `MalformedVersion` / `MalformedDocument`.)
- **Contracts** (non-empty list): each has non-blank `id` (unique → else
  `DuplicateComponent`), a valid `version` (R3), non-blank `owner`/`surface`/`consumers`;
  `owner` ∈ repos ∪ {`github`}; every `consumers[]` entry ∈ repos (→ `UnknownComponent`);
  optional `package-version` valid; optional `range` well-formed.
- **Dependencies**: each edge's `from` and `to` are present and ∈ repo keys
  (→ `MissingField` / `UnknownComponent`). **`via` is free-text and is NOT
  contract-checked** — matching the authority.
- **Coherence**: each entry has a non-blank `id` and a boolean `coherent`.

**Rule set**: extend `RegistryRule` additively with `DuplicateComponent` and
`MalformedDocument` (added DU cases; additive under R1's posture).

**Resolved scope correction**: spec User Story 2, scenario 3 ("genuine undefined-contract
reference via a `via` linkage still reported") is **relaxed** — the authority does not
parse `via`, and matching it (SC-005) takes precedence. Genuine *contract-reference*
defects are instead caught structurally via `owner`/`consumers`/duplicate-id/version rules.
This is recorded so `validateDocument` does not over-validate prose and diverge from the
gate it is replacing.

**Open confirmation for /tasks**: the tail of `scripts/validate-registry.py` (coherence
handling + `--expect <id>=<version>` pin assertion) was only partially read during
planning. A task MUST read the remainder and pin coherence-entry + `--expect` parity
exactly. The typed `fsgg-contracts`-pin-coupling assertion is already live in the gate and
is **out of scope** here (it stays where it is); this feature replaces only the
*schema-validation* half.

## R5 — Determinism

**Decision**: `validateDocument` walks root → repos → contracts (file order) →
dependencies (file order) → coherence (file order), appending diagnostics in that fixed
order; given identical input it returns an identical diagnostic list. The edge maps
YamlDotNet nodes preserving document order. No clock, no hashing, no nondeterministic
iteration (no unordered dictionary enumeration in the diagnostic path).

**Rationale**: SC-004 — the verdict must back a reproducible CI `--exit-code` gate.

## R6 — Test fixture strategy (real evidence)

**Decision**: Vendor the **actual** `registry/dependencies.yml` into the test tree as a
fixture and assert it validates clean (SC-001). Derive broken variants from it (drop a
required field, add an undefined consumer, duplicate an id, malform a version, corrupt the
YAML) — each asserting the exact rule kind, plus a paired "still passes the good case"
assertion (SC-002, no-regression-in-detection). Constitution Principle VI: real fixtures
over mocks.

**Drift note**: the vendored fixture can drift from the upstream file. A task adds a
README/source-comment pointing at the canonical path; long-term, FS-GG/.github#18 running
the typed validator against the *live* file is the real anti-drift guarantee.
