namespace Fsgg

/// Typed cross-repo dependency-registry model + pure validator (FR-008/009).
/// BCL-only: SemVer parse/compare/range is an internal helper, no third-party package.
module Registry =

    /// A cross-repo component (repo/package) and its declared version.
    type RegistryComponent = { Id: string; Version: string }

    /// A dependency edge: `Consumer` depends on `Provider`, declaring the range of
    /// provider versions it is compatible with.
    type DependencyEdge =
        { Consumer: string
          Provider: string
          CompatibleRange: string }

    /// The typed model of `registry/dependencies.yml`.
    type RegistryModel =
        { Components: RegistryComponent list
          Edges: DependencyEdge list }

    /// The coherence/completeness rule a diagnostic reports as violated (FR-009).
    /// `DuplicateComponent` and `MalformedDocument` were added (additively, feature
    /// 042) for the real-schema `validateDocument`; the legacy `validate` emits only
    /// the original four. `MalformedField` was added (additively, feature 104) for
    /// `validateSkillRegistry`: a field that is PRESENT but unparseable is a distinct
    /// fault from one that is MISSING, and collapsing the two is what lets an
    /// unreadable value pass as an unset one.
    type RegistryRule =
        | MissingField of fieldName: string
        | UnknownComponent
        | IncompatibleVersion
        | MalformedVersion
        | DuplicateComponent
        | MalformedDocument
        | MalformedField of fieldName: string

    /// A single actionable diagnostic naming the offending entry and the rule.
    type RegistryDiagnostic =
        { Entry: string
          Rule: RegistryRule
          Message: string }

    /// Validation outcome: success has no diagnostics (SC-007).
    type ValidationResult =
        | Valid
        | Invalid of RegistryDiagnostic list

    /// Pure validator over the typed model. Reports missing required fields,
    /// edges referencing unknown components, version ranges that exclude the
    /// referenced version, and malformed version strings (FR-008/009).
    val validate: model: RegistryModel -> ValidationResult

    // --- Real-schema document model + pure validator (feature 042, additive). ---
    // Models the actual `registry/dependencies.yml` shape (schemaVersion / repos /
    // contracts[] / dependencies[] / coherence[]). The legacy RegistryModel/validate
    // above are retained unchanged. The YAML `load` edge lives in FS.GG.SDD.Artifacts
    // (Constitution V — I/O at the edge, not in this BCL-only leaf).

    /// A repo participating in the registry (the `repos:` map). `Id` is the map key.
    type RegistryRepo =
        { Id: string
          Name: string
          Role: string }

    /// One owner's answer to *"which repos depend on this contract?"* — an obligation the
    /// owner DECLARES, not a fact derived from the tree.
    ///
    /// THREE states, and the middle one is the point. `absent` and `[]` are DIFFERENT
    /// CLAIMS — `[]` says the owner considered this contract and asserts that nothing
    /// consumes it; absent says the question was never answered — and a bare `string list`
    /// cannot tell them apart, because the YAML edge maps a missing key onto the same
    /// empty list a present `[]` produces.
    ///
    /// That collapse is why a real package could not be registered. `FS.GG.NewSddWorkspace`
    /// is a `dotnet new` template humans install: no repo restores it, so `[]` is its only
    /// honest value — and a validator that reads absent and `[]` alike must reject both,
    /// leaving only two moves, both of which corrupt the registry. Invent a consuming repo,
    /// and the lie gets ENFORCED (`fsgg-surface-impact` reads `consumers` to decide who a
    /// shipped-surface mutation must flag, so a fictional edge mails a real consumer-impact
    /// issue to a repo that does not consume it). Or drop the row, which is how the org's
    /// package inventory went off by two in the first place. See FS.GG.SDD#508 / ADR-0039 §5.
    ///
    /// The empty case stays SAFE only because absent is still refused. `consumers: []` is a
    /// fail-open field — `fsgg-surface-impact` files ZERO consumer-impact issues for a
    /// breaking change and prints "(none declared)" without complaint — which is correct for
    /// a genuinely consumerless package and silent misrouting for a row that merely forgot.
    /// Distinguishing the two is what keeps "nothing consumes this" an assertion somebody
    /// made rather than a question nobody answered.
    ///
    /// Modelled as a union so the collapse is UNREPRESENTABLE rather than merely
    /// discouraged, and deliberately NOT as the `string list option` the request suggested:
    /// that has two states and nowhere to put a present-but-unparseable value
    /// (`consumers: sdd`, `consumers:`), which would then have to collapse into `None` —
    /// silently re-reading a malformed declaration as "unanswered", the same bug one level
    /// down. Sibling of [`MirrorDeclaration`](#MirrorDeclaration), decided the same way and
    /// for the same reason.
    type ConsumerDeclaration =
        /// No `consumers:` key at all — the question has NOT been answered for this
        /// contract. This is not `[]`, and must never be reported as one.
        | ConsumersUnspecified
        /// The owner answered. An EMPTY list is a real answer: "nothing consumes this."
        | ConsumersDeclared of consumers: string list
        /// Present but not a sequence (`consumers: sdd`, `consumers:` with no value…) —
        /// an unparseable declaration. Carried with its raw text rather than dropped, so
        /// `validateDocument` can REPORT it instead of silently re-reading it as
        /// `ConsumersUnspecified` (which would be a phantom "you forgot") or as
        /// `ConsumersDeclared []` (which, now that empty is legal, would be a phantom
        /// "nothing consumes this" — a typo passing as a deliberate assertion).
        | ConsumersMalformed of raw: string

    /// A versioned cross-repo contract (`contracts[]`). `PackageVersion`/`Range` are
    /// present only on some entries.
    ///
    /// `Consumers` is three-state as of 3.0.0 (FS.GG.SDD#508) — see `ConsumerDeclaration`.
    /// Retyping a field on a public F# record is a BINARY BREAK, hence the major; there is
    /// no additive spelling of this change (docs/release/contracts-version-bump-checklist.md).
    type ContractEntry =
        { Id: string
          Version: string
          Owner: string
          Surface: string
          Consumers: ConsumerDeclaration
          PackageVersion: string option
          Range: string option }

    /// A hard dependency edge over repos (`dependencies[]`). `From`/`To` are repo
    /// ids; `Via` is free-text and is NOT contract-checked (parity with the Python
    /// authority — research R4).
    type DependencyEdge2 =
        { From: string
          To: string
          Via: string }

    /// A coherence state entry (`coherence[]`).
    type CoherenceEntry = { Id: string; Coherent: bool }

    /// The typed model of the real `registry/dependencies.yml`.
    type RegistryDocument =
        { SchemaVersion: int
          Repos: RegistryRepo list
          Contracts: ContractEntry list
          Dependencies: DependencyEdge2 list
          Coherence: CoherenceEntry list }

    /// Pure validator over the real-schema document. Mirrors the rule *kinds* of
    /// scripts/validate-registry.py so the two cannot disagree on the canonical file
    /// (SC-005). Deterministic: diagnostics in document order
    /// (root → repos → contracts → dependencies → coherence). No I/O.
    val validateDocument: document: RegistryDocument -> ValidationResult

    // --- Skill-registry document model + pure validator (feature 104, additive). ---
    // Models `FS-GG/.github` `registry/skills.yml`, the org's authoritative skill
    // catalog (ADR-0017). Sibling of the dependency registry above, and a SEPARATE
    // document: the two share this module and nothing else. The YAML `load` edge
    // lives in FS.GG.SDD.Artifacts (Constitution V — I/O at the edge, not in this
    // BCL-only leaf).

    /// One owner's answer to ADR-0022 §6's frozen-mirror question for one skill body:
    /// *"is a designated consumer repo required to ship a byte-identical copy of this
    /// body?"* — an obligation the owner DECLARES, not an observation that a same-named
    /// file happens to exist in two trees. WHICH repo carries the obligation is the org
    /// registry's business and is deliberately not named here: generic SDD embeds no
    /// provider identity (CLAUDE.md; the `ScaffoldGuardTests` deny-list enforces it).
    ///
    /// THREE states, and the third is the point. `absent` and `false` are DIFFERENT
    /// CLAIMS — `false` says the owner considered this body and asserts no obligation;
    /// absent says the question was never answered — and a two-state `bool` with a
    /// default cannot tell them apart. That collapse is a live fail-open, not a
    /// tidiness concern: `select(.mirrored == true)` reads an absent key as false
    /// (`null == true` is false), so a catalog predating the field answers "not
    /// mirrored" for EVERY body, confidently, and every real mirror goes unguarded —
    /// the exact hole `.github#658` was opened to close. Modelled as a union so the
    /// collapse is UNREPRESENTABLE rather than merely discouraged: there is no `false`
    /// for an absent value to become.
    ///
    /// (`Fsgg` deliberately does NOT reuse the `bool option` the request suggested:
    /// it has two states and nowhere to put a present-but-unparseable value, which
    /// would then have to collapse into `None` — silently re-reading a malformed
    /// verdict as "unanswered", which is the same bug one level down.)
    type MirrorDeclaration =
        /// No `mirrored:` key at all — the question has NOT been answered for this
        /// body. This is not `false`, and must never be reported as one.
        | MirrorUnspecified
        /// The owner answered: `true` asserts the mirror obligation, `false` denies it.
        | MirrorDeclared of mirrored: bool
        /// Present but not a boolean (`yes`, `""`, a list…) — an unparseable verdict.
        /// Carried with its raw text rather than dropped, so `validateSkillRegistry`
        /// can REPORT it instead of silently re-reading it as `MirrorUnspecified`.
        | MirrorMalformed of raw: string

    /// One row of `registry/skills.yml`. `MaterializesWhen` is the ADR-0017
    /// condition predicate; absent ⇒ `always` (the catalog's own rule), so it stays
    /// an option rather than being defaulted here.
    type SkillRegistryEntry =
        { Id: string
          Scope: string
          Owner: string
          Source: string
          Sha256: string
          Mirrored: MirrorDeclaration
          MaterializesWhen: string option }

    /// The typed model of the org skill catalog (`registry/skills.yml`).
    type SkillRegistryDocument =
        { SchemaVersion: int
          Parameters: string list
          Skills: SkillRegistryEntry list }

    /// Pure validator over the skill-registry document — the DOCUMENT-SCHEMA tier, the
    /// same tier `validateDocument` occupies for `dependencies.yml`. Deterministic:
    /// diagnostics in document order (root → skills). No I/O.
    ///
    /// It deliberately does NOT attempt what the org registry's own reconciling check does —
    /// hashing producer bodies, walking the consumer repo's tree to test the mirror
    /// obligation, reconciling rows from producer manifests. Those need the filesystem of
    /// three repos and are structurally out of reach of a pure BCL leaf. The two are
    /// complementary; this one claims no authority over content the other verifies.
    ///
    /// Nor does it evaluate `materializes-when` predicates: the predicate DSL is the org
    /// registry's, evaluated by its union gate against a scaffold's effective parameters.
    /// This validator checks the predicate is PRESENT and non-blank, never what it MEANS.
    val validateSkillRegistry: document: SkillRegistryDocument -> ValidationResult
