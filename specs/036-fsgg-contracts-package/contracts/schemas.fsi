// CONTRACT SKETCH — Phase 1 design artifact, not compiled source.
// The implementation lives at src/FS.GG.Contracts/Schemas.fsi (+ .fs).
// Namespace `Fsgg` per the item's mandated module breakdown (Fsgg.Schemas).
// BCL-only: FSharp.Core types exclusively; no serialization, no I/O.

namespace Fsgg

/// One typed source of truth for every `.fsgg` schema shape and its version
/// constant (FR-004/005). SDD-owned version constants equal the value SDD emits
/// today; Governance-owned schemas are declared to their published reference.
module Schemas =

    /// Which repo owns (emits) a schema. SDD-owned versions must equal today's
    /// emitted values; Governance-owned shapes are declared, not emitted by SDD.
    type SchemaOwner =
        | Sdd
        | Governance

    /// The unit of "one fact in one place": a schema's contract name paired with
    /// its version constant(s) and owner.
    type SchemaContractEntry =
        { Name: string
          SchemaVersion: int
          ContractVersion: string option
          Owner: SchemaOwner }

    // --- Named version constants (FR-005). One authoritative value each. ---
    val providersVersion: int                       // = 1
    val projectVersion: int                          // = 1
    val sddVersion: int                              // = 1
    val agentsVersion: int                           // = 1
    val scaffoldProvenanceVersion: int               // = 1
    val governanceHandoffVersion: int                // = 1
    val governanceHandoffContractVersion: string     // = "1.0.0"
    val governanceVersion: int                       // = 1 (published reference)
    val policyVersion: int                            // = 1 (published reference)
    val capabilitiesVersion: int                      // = 1 (published reference)
    val toolingVersion: int                           // = 1 (published reference)

    /// All 10 named schemas, for the "every schema represented?" check (SC-001).
    val entries: SchemaContractEntry list

    // --- Typed shape per schema (fields mirror today's SDD records; BCL-only). ---
    // Shapes are sketched here by name; full field lists mirror the corresponding
    // src/FS.GG.SDD.Artifacts records (project/sdd/agents/providers configs,
    // ScaffoldProvenanceRecord, GovernanceHandoff) and the Governance published refs.
    type ProvidersSchema            // mirror of `.fsgg/providers.yml` registry shape
    type ProjectSchema              // mirror of `.fsgg/project.yml`
    type SddSchema                  // mirror of `.fsgg/sdd.yml`
    type AgentsSchema               // mirror of `.fsgg/agents.yml`
    type ScaffoldProvenanceSchema   // mirror of `.fsgg/scaffold-provenance.json`
    type GovernanceHandoffSchema    // mirror of `governance-handoff.json`
    type GovernanceSchema           // declared to Governance published reference
    type PolicySchema               // declared to Governance published reference
    type CapabilitiesSchema         // declared to Governance published reference
    type ToolingSchema              // declared to Governance published reference
