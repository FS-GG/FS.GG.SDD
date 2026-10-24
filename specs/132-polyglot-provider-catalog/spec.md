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

## SDD928-C2.2 — local candidate staged creation

Tier 1 additive explicit catalog scaffold route, under the existing owning plan. The selected
Config 0.3.0 archive is a locally qualified candidate awaiting the protected coherent release
join, not a published dependency or adoption pin. Legacy callers and schema-1 workspaces retain
their contracts. Quint remains the sole lifecycle/semantic acceptance authority.

- FR-008: Resolve the original complete capability declarations through the actual selected
  Config package, with independently selected policy, immediately before dispatch. Reject missing
  required bindings and unsupported exact evidence mappings; caller-supplied resolved sets confer
  no authority. Descriptor protocol 3.0.0 maps explicitly to neutral capability contract 1.0.0.
- FR-009: Observe selected platform, every selected-platform tool and exact transport version
  through bounded literal process invocations. Policy controls executable/version parsing, supported
  formats/environments and budget ceilings; declarations cannot widen them.
- FR-010: Consume the exact selected local template archive into isolated qualified transport
  state, without template updates, discovery or network fallback. Preserve ordinal effective argv
  values, including empty strings and metacharacters; reject transport-option collisions.
- FR-011: Require an absent target and existing parent, with no force. Compose the whole workspace
  in a sibling owned staging directory, validate physical containment and full ownership/skill union,
  then perform atomic no-replace commit. Failure writes no success envelope and preserves target.
- FR-012: Use separate monotonic whole-phase preflight/scaffold deadlines, cancellation, bounded
  output and explicit environment admission. Retire owned children and staging only. Unknown cleanup
  or commit outcomes stay unknown and block retry.
- FR-013: Emit complete schema-2 declaration/actual observation/ownership only after observed
  success. Canonical provenance uses root roles and relative paths, with no clocks, staging randomness
  or physical machine paths. Identical normalized declarations/observed outputs yield identical bytes.
- FR-014: Select and physically capture the recognized scaffold provenance source in both producer
  work-model selection and closed WorkModelSourceBundle validation. Tampering changes revision-bound
  source identity; provenance supplies no alternate lifecycle or semantic acceptance.
- FR-015: Route the real explicit scaffold CLI through the production MVU/edge. Dry run uses the
  same preparation with observations absent. No catalog flags without catalog silently select legacy.

Acceptance is one generic local package fixture through that real caller: exact archive/argv/cwd/tool
observations, awkward values, deterministic repeat, complete ownership/skills, and no-target-change
negatives for bad bindings/policy/maps/tools, drift, reserved writes, collisions, deadlines/cancellation
and cleanup. Pure effects or synthetic records cannot establish real execution. Runtime fixture
qualification is separately admitted by root. C2.3 Verify, C3 SDK and C4 release/adoption remain open.

## C2.2 first-host mechanism addendum

The explicit catalog route adds `--transport-executable <fully-qualified-file>`. The host supplies
LocalLinux selection independently of provider bytes and policy probe IDs. Unsupported host,
CI/Release requirements, missing ABI/exports or unqualified transport behavior refuse before launch.
The first profile uses Linux-x64/glibc, pidfd_spawn and held directory descriptors. Atomic
renameat2 NOREPLACE publishes into the held parent object and preserves an appearing target.
The supported cooperative namespace excludes adversarial pathname ABA, hostile mounts and escaping
descendants; pathname relocation can leave requested-path commit Unknown. No sandbox is claimed.

The caller holds an opaque Operation before any acquisition. Pure prepare performs no native
observation; run consumes it once. Every phase ceiling includes cancellation/drain/cleanup, with
1MiB per stream/2MiB whole-phase output and separately bounded8MiB raw transport state. Unsettled
launch, readers, children or commit remain owned and block retry/commit/deletion. CLI reports the
original failure/unknown once, then passively retains the owner; bounded reporting does not promise
bounded process exit. Late facts do not upgrade the report or renew filesystem cleanup authority.

Before managed body or SDK transport, qualify exact native ABI/exports/offsets with a finite
ABI-only C fixture, then separately qualify pidfd/fd-cwd/no-replace primitives with a real outer
fixture owner and managed coexistence. SDK procfd-root/cache/alias behavior is another gate; the
previous ordinary absolute-path fixture acceptance does not establish it. No new package/helper,
policy schema or alternate execution authority is introduced.

## SDD928-C3.1 — package-only authoring example

Tier 1 sample contract; installed CLI, public library APIs and schemas stay unchanged.
- FR-016: A nonpackable F# example uses only actual package APIs to author and seal a generic
  test-handoff catalog. Require an explicit exact SddPackageVersion; no default public pin.
- FR-017: Independently supplied policy, absolute fixture executable, raw test input and copied
  template data produce the existing CLI fixture manifest. No test assembly, reflection, sibling
  source, SDK probe promotion or copied canonicalization is permitted.
- FR-018: Emit ordinal archive entries with fixed ZIP timestamps, attributes and compression.
  Reject links, escaping/duplicate paths and existing output. Equal input bytes yield equal catalog
  and archive hashes despite input creation order or mtimes. Strict check uses the production codec.

Acceptance: immediate cold package-only typed compile, deterministic double emission, invalid policy
refusal and actual CLI/Governance journey through the maintained drivers. Retained SDD2.2 packages
are explicit early-compile inputs only; final local2.3 qualification awaits independently supplied
qualified archives. This bounded slice does not close C3.1, distribution or provider certification.


### SDD928-C3.1-03 — Governance Verify capture-lifetime join (#1131)

- FR-C3-01: One Governance Verify provider invocation owns one capture session; the Verify host
  releases it (Governance#465/#468). The maintained runner is the outer admitted runtime for the
  SDD package-only qualification: it is the only supplier of the host's original work and cleanup
  ends, requires the whole capture window plus a startup allowance to fit inside its own work window
  (so it never signals a host inside that window), refuses the declaration otherwise, and ends the
  whole pid-namespace worker afterwards. No Governance contract change is required.
- FR-C3-02: Teardown is observed, not assumed: descendants record their outer pid and kernel start
  time, and a per-run environment marker is scanned for non-cooperatively; none may survive the
  worker. A settlement is only a host exit by itself with a pass or blocked exit (0/1), no
  unsettled report and inside the admitted window (exit 3 claims none); a host that stays resident is a non-passing
  `worker-ended-by-outer-runtime`.
- Out of scope: an authentic blocked regular-file read and an independently observed uncertain
  close (Governance#423 prerequisite b); hostile containment; provider certification; distribution.

### SDD928-C3 — provider qualification harness and certification report (#1134)

- FR-C3-03: A maintained harness (`scripts/provider-certification.py`) qualifies a PACKED provider artifact
  through the installed SDD CLI and installed Governance CLI and emits a closed
  `fsgg.provider-certification/v1` report: lifecycle, provenance, skill-union, no-partial-state,
  unsupported-tool, malformed/wrong-version provider controls, a supported-host check and the artifact's one
  declared required test capability's evidence through Governance Verify/GateExecution under the FR-C3-01/02
  capture-lifetime join.
- FR-C3-04: A leg that cannot run is `skipped` with a reason and the verdict is not certified; an
  admission-side validator (`check-report`) rejects a report with a missing, duplicated, unevidenced or
  inconsistent check, a verdict that overstates the checks, or a machine-specific path.
- FR-C3-05: When the artifact also declares `build:build` and/or `package:package` (typed example profile
  `build-test-package`), the harness certifies their ACTUAL outputs: the build output equals the declared
  transformation of its source and its handoff cites those exact bytes; the packed tar's members are exactly the
  declared members, equal the files they claim to pack, and the handoff cites the artifact's exact digest. A
  provider whose build or package evidence is self-consistent but wrong (accepted by Governance) fails exactly its
  own `capability.*` check.
- Out of scope: distribution (C4), the schema-v1 window, concrete language providers, hosted or hostile
  profiles, and Governance#423 prerequisite (b).

## Selected-template data transport follow-up

This bounded internal transport extension implements the accepted Templates441/SDD928
join under FR-010. Public signatures, schema2/descriptor3, independent admission and
FR-011–FR-015 remain unchanged; no publication or default activation is implied.

- FR-019: Select exactly one explicit descriptor alias from a multi-template archive;
  refuse selected alias/identity ambiguity, including legacy alias arrays and nested
  content roots. Unselected templates do not gain execution authority.
- FR-020: Preserve captured classifications, parameter descriptions/defaults, literal
  text/string and single-valued choices, and boolean requiredness. Check effective
  values and descriptor restrictions against the actual selected declaration.
- FR-021: Permit one static source mapping with omitted or exactly ./ source/target;
  only closed directory and extension exclusions are admitted, with zero-directory **
  semantics. Exclusions determine the complete expected payload inventory.
- FR-022: Permit only a direct declared-parameter, single literal hyphen-to-underscore
  generated substitution and fixed nonempty token. Generated symbols are not CLI
  parameters. Other generators, conditions, path renames and dependency cycles refuse.
- FR-023: Installed SDK cache identity/configuration/alias and parameter metadata must
  match this same actual declaration. Keep archive integrity, exact mount inventory,
  literal long-option checks and complete output-byte comparison.

Acceptance separates synthetic actual-parser/cache tests from the separately selected
real Node vertical using its owner's exact multi-template archive. Other-language
evidence, general identifier grammars and portable tool carriers remain explicit gaps.


## SDD1115: literal template parameter transport

Tier 1 external transport correction; existing public `.fsi` signatures, report records,
provenance schemas and descriptor contracts remain unchanged. The semantic API exercise
uses the existing `CommandWorkflow.init/update`/RunProcess surface and
`CatalogScaffoldEffects.prepare/run/observe/release`; no new public API is introduced.

Both production template callers preserve the effective parameter map literally. A
nonempty value is one `--key=value` argument; an empty value is the two arguments
`--key` and an explicit empty string. No shell, response-file expansion, language
transform, extra quoting in argv, or provider-specific exception is introduced.
Defaults are overlaid by author values and keys retain deterministic ordinal order.
Existing invalid-key/built-in-option collision refusals remain before invocation.

Planned legacy commands and reported legacy failure invocations derive from the
same original RunProcess argv. Display quotes make empty values, whitespace and
metacharacters unambiguous; display text is never reparsed for execution. Catalog
provenance records the actual ordered argument vector, including explicit empty
entries; older provenance remains readable without migration or schema changes.

Qualification requires real SDK10.0.401/template-engine controls through both
production callers for an existing response-file sentinel, a missing response-file
name, spaces, empty, embedded `=`, option-shaped `--` values, Unicode, defaults,
overrides and order. Generated bytes, invocation/provenance and sentinel preservation
are checked, and the same maintained caller must fail causally with original split
encoding. Effects-only assertions, routing doubles, skipped native facts and engine
discovery are not actual qualification. Existing bounded runner/profile and exact
source/input/cleanup custody apply; each runtime requires separate admission.

This source slice does not select any package version, publication or installed
receiver effect. Retained c8/2.3 artifacts and SDD1113 occupancy Unknown remain
unchanged. Parent `.github#4642` owns the Wizard bindingTarget repair, new coherent
publication and installed bindings journey.
