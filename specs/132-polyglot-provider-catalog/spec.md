# Provider catalog contract — SDD928-C1

Tier 1 additive public API and document contract. Owner: FS.GG.SDD#928; campaign:
`unified-roadmap-20261003`. Authority: [ADR-0092](https://github.com/FS-GG/.github/blob/main/docs/adr/0092-descriptor-driven-polyglot-workspace-providers.md)
and its linked design. This is prepared contract data, not provider certification.

## Requirements

- FR-001: Parse only catalog schema 2 and descriptor protocol exactly 3.0.0, independently
  of package versions and legacy schema/descriptor contracts.
- FR-002: Expose provider metadata, pinned catalog/descriptor identities and SHA256 digests,
  opaque template transport, platforms, typed prompts and declarative validation.
- FR-003: Resolve defaults then explicit overrides, rejecting invalid defaults even when hidden,
  duplicate keys, undeclared inputs and missing required inputs before returning any value.
- FR-004: Retain raw product name, package/module identity and code identifier separately through
  explicit parameter references; never derive names using host ecosystem logic.
- FR-005: Retain tools, capability commands/semantic obligations, evidence and product skills as
  declarations. Validate references and shape without certifying semantics or executing commands.
- FR-006: Preserve all legacy records, parsers and handlers. New descriptors remain non-invocable
  until C2 admission, observation, cleanup and provenance controls are implemented.
- FR-007: Deterministic diagnostics and prepared output; keyed sets sort ordinally, argv and values
  retain exact order/bytes. No process, file, package, network or lifecycle effect is planned.

## Acceptance

Four selected ecosystem fixtures and a fifth arbitrary provider resolve via identical generic code.
Awkward names remain distinct. Missing explicit module identity refuses. Hidden invalid defaults,
unknown kind/field/schema/protocol, malformed exact versions, duplicate IDs and dangling references
refuse. Existing legacy construction and scaffold version refusal controls remain valid.

Root accepted the bounded native08 source qualification: 56 focused tests passed, including both
committed reflection surfaces, package-version coherence, legacy parser compatibility and scaffold
refusal before mutation. Protected landing, package qualification and installed adoption remain open.

See [wire contract](contracts/catalog.md), [plan](plan.md), and [tasks](tasks.md).


## SDD928-C2.1 — read-only catalog preparation

Add explicit digest-checked `catalog inspect`, canonical semantic digest projections and closed schema-2 provenance reading/ownership projection. Preserve C1 parser/resolve and every legacy constructor. No runtime Governance reference or invocation is introduced. Inspection reports prepared declarations only, reads the explicitly selected local file and never writes or probes tools. `scaffold --catalog` refuses as unavailable before effects.

Verify raw/semantic tamper, sorted declarations versus literal argv, Unicode/control byte goldens, explicit parameter identities/defaults, schema-1 parity, complete synthetic schema-2 ownership correspondence, and malformed/unsupported provenance blocking refresh/lifecycle mutation. Real executable provider qualification remains C2.2 under root admission.
