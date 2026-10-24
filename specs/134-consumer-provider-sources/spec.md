# Consumer-defined provider sources

Feature134 · SDD#935 · Tier1 · source implementation selected2026-10-24.
Owner: review_coherent113, through the existing Delivery's full acceptance.
Immediate native parent: SDD#928; programme ancestor: .github#3008.

## Outcome

A consumer selects independently maintained provider definitions from a local
directory or digest-pinned HTTPS envelope. The existing catalog/scaffold caller
validates the complete source before target mutation, uses its existing trusted
capability policy and executor, records exact source provenance, and can repeat
creation offline only from verified exact cached bytes. Discovery never runs a
provider command, template hook, skill, tool probe or package installer.

All original #935 Acceptance and Verification remain required. Source delivery is
not installed acceptance. The genuine final journey uses published tools and a
third-party provider outside FS-GG, creates and actually builds/tests a clean
product, then repeats offline with the same verified source. Publication precedes
Wizard#3010 adoption. Templates#441's built-in packs are independent of this feature.

## Requirements

### User scenarios

1. P1: An external maintainer supplies a closed local provider source. A consumer
   selects its manifest digest, inspects it without tool execution, then separately
   admits the existing scaffold caller and obtains a buildable/tested product.
   Changed or escaping local members refuse with the original target preserved.
2. P1: A consumer selects an immutable HTTPS source with exact envelope/manifest
   digests. Acquisition records the actual permitted endpoint/hops, verifies and
   caches the complete closure, and creates the product through the same caller.
   With network unavailable, the exact cached source reproduces the product;
   absent or corrupted cache refuses without a network attempt.
3. P1: A workspace and an explicit source advertise a colliding provider ID.
   Resolution refuses the whole selected set before acquisition-dependent
   execution or target mutation, even when the descriptor bytes appear equal.

### Functional requirements

- PS001: Preserve existing catalog/scaffold invocation when no new source option
  is supplied. Existing descriptor identities, literal parameters, policy and
  schema1/schema2 provenance retain their meaning.
- PS002: Accept one explicit source-set JSON file/digest. Its entries have stable
  IDs, a BuiltIn/Workspace/Explicit origin and either an absolute local directory
  with expected manifest digest or an HTTPS locator/revision with expected
  envelope and manifest digests. A workspace-owned source set is explicitly
  selected; there is no new automatic configuration search.
- PS003: Normalize all sources to the same strict source manifest and member
  closure. Existing catalog/descriptor semantics remain in published Contracts7.6;
  the source wrapper does not copy them or add language-specific behavior.
- PS004: Reject duplicate source/provider IDs, including identical duplicates.
  Order origins BuiltIn, Workspace, Explicit and IDs ordinally for deterministic
  listing/diagnostics. Never silently shadow or merge a descriptor. Explicit
  selection limits which sources are admitted, not their trust or ownership.
- PS005: Validate raw/semantic catalog identity, exact member population, archive
  association, schemas, tools/platform declarations and skill manifests before
  the existing execution admission. A digest identifies bytes; independent policy
  remains the authority for permitted acquisition and executable capabilities.
- PS006: HTTPS is data transport only. Require the exact selected origin and a
  public-address-only first profile, normal TLS validation, no inherited proxy,
  cookies, credentials or automatic redirects. Independently selected policy can
  permit at most three same-origin HTTPS redirects. Bind every resolved IP and
  connected endpoint; do not resolve again implicitly after validation. Refuse
  userinfo/fragments, forbidden address families/ranges, downgrade, changed origin,
  moving content, wrong digest, unavailable/incomplete transfer and exhausted bounds.
- PS007: Inventory a ZIP envelope before extraction. Only regular members are
  allowed; reject directories as payload, links, devices, traversal, absolute
  paths, duplicate/case-colliding names, missing/extra members and exceeded bounds.
  Local directory sources apply equivalent containment/link/closed-population
  checks and detect changes. The manifest itself is externally hashed, not
  self-hashed. Its members exclude the manifest and include exactly one catalog
  plus the declared opaque template archives.
- PS008: Acquire into fresh owned staging outside the target. Publish only a
  complete verified cache entry by no-replace commit. Rehash on every read; do not
  select partial/mixed entries or overwrite conflicting cache bytes. The source
  cache is separate from template-engine and package-manager caches.
- PS009: Offline mode performs no DNS/HTTP/install/update. Missing/corrupt cache
  refuses. Cache presence conveys neither executable trust nor host qualification.
  Retained network observations are historical when reading a cached source.
- PS010: Pass selected verified catalog/archive bytes and source evidence to the
  existing CatalogScaffoldEffects operation. Revalidate the exact source evidence
  against held inputs before execution. Write schema3 source provenance inside
  staging before target commit, never as a post-success repair.
- PS011: Preserve first failure, target preimage and actual cleanup uncertainty.
  Original source acquisition owns its tasks/streams until terminal and EOF;
  cleanup Unknown cannot yield a prepared source or trigger an automatic retry.
- PS012: Schema3 nests the unchanged strict schema2 catalog record plus a typed
  source observation. All lifecycle ownership readers accept1/2/3 through the
  same ownership projection; unknown/malformed schemas never fall back to legacy.
  Old tools must refuse3 rather than reinterpret it. Source provenance is not an
  execution verdict or a replacement lifecycle authority.

## Verification

Semantic/API tests precede implementation. Cover source equivalence, deterministic
ordering, every collision and malformed field, envelope/path/link/budget controls,
redirect/digest/address policy, no pre-validation execution or target mutation,
offline no-attempt behavior, cache corruption/partial state, provenance joins and
legacy ownership, first failure and cleanup uncertainty. Use actual filesystem,
ZIP and HTTPS fixtures at separately admitted test stages; synthetic declarations
must identify themselves. Pure tests cannot qualify the installed carrier.

The real installed journey must preserve full original package/tool/source and
process custody, build/test the third-party product and reproduce offline. The
current source runtime route remains Linux-x64, exact glibc2.44 plus30 exports,
.NET10.0.12 and the SDK10.0.401 transport closure. Governance's separate>=2.40
guard remains legitimate. Published Commands binary/source and actual host proof
are separate; no guard weakening is part of this feature.
