# Implementation plan

Base: protected SDD46ca7ff71a87bc288587bf7adbd0fe79b0ae23be. This plan freezes the
source wire contracts and conservative hard ceilings before acquisition effects
are implemented. Compilation, native test/acquisition and publication are not
admitted by this plan.

## Wire versions and numeric ceilings

| Surface | Version / hard bound |
|---|---|
| Source set | `fsgg.provider-source-set/1`; UTF-8 JSON, at most256KiB and16 sources |
| Source manifest | `fsgg.provider-source/1`; UTF-8 JSON, at most256KiB |
| Acquisition policy | `fsgg.provider-source-policy/1`; UTF-8 JSON, at most64KiB; explicit caller-selected digest |
| Source receipt | `fsgg.provider-source-observation/1`; strict JSON, at most256KiB |
| Internal immutable cache metadata | `fsgg.provider-source-cache/1`; at most256KiB; historical acquisition facts only |
| Scaffold provenance | integer schema3, nested strict schema2 `catalog` plus `source`; total existing provenance ceiling still applies |
| JSON | depth16; duplicate/unknown properties refused; existing stricter artifact ceilings retained |
| Source envelope | ZIP, maximum256MiB compressed; no self-extracting/executable envelope support |
| Member closure | at most4096 members, maximum256MiB per member and512MiB aggregate expanded bytes across the complete selected source set |
| Catalog | maximum1MiB per catalog, also subject to existing catalog parser ceiling |
| Paths | at most1024 UTF-8 bytes,32 components; portable relative path grammar, ordinal and case-insensitive collision refusal |
| Text identities | source ID/revision at most128 characters; URI at most4096 characters |
| Redirects | no redirects by default; policy may select0..3 same-origin HTTPS redirects |
| Acquisition clock | caller chooses16..180 seconds total; includes15-second original cleanup reserve; no clock renewal per source/hop |
| Network | connect at most10 seconds and response-body inactivity at most15 seconds, each clipped to original useful-work end |
| Diagnostics | at most64 entries and64KiB UTF-8 total; truncation is explicit, first cause retained |
| Private staging/cache write population | at most1GiB for one operation including envelope, extracted closure and proposed cache copy; existing cache is not rewritten |

Wire digests are exactly `sha256:` plus64 lowercase hexadecimal characters.
Canonical writers sort source origins/IDs, manifest member paths and archive
associations ordinally without altering literal parameter or descriptor bytes.
Readers accept insignificant JSON whitespace but reject duplicate/unknown fields,
noncanonical integer encodings, invalid UTF-8/BOM and unsupported versions.

The caller may select smaller limits through the policy but cannot raise these
hard ceilings. Refuse infeasible time windows (total<=15 seconds) before effects.
All counter arithmetic is checked64-bit before allocation or accumulation. Sizes
declared in manifests/HTTP/ZIP metadata are checked against streamed observed
counts; declarations never replace observations. No advertised compression ratio
or Content-Length can bypass actual counters.

## Module and dependency plan

Reuse published `Fsgg.ProviderCatalog` types unchanged; there is no new
FS.GG.Contracts release prerequisite or source-project reference. Artifacts adds
`ProviderSource` typed source-set/manifest/policy/receipt contracts and strict
parsers, validation and deterministic selection; `SourceScaffoldProvenance`
joins schema3 to the existing schema2 record. Existing
`ScaffoldProvenanceDocument` adds a SourceCatalog case and ownership projection.
WorkItem's observed schema projection and other document consumers change together.

Commands adds a pure `ProviderSourceWorkflow` and a bounded data-only
`ProviderSourceEffects` edge. Prepared source handles are opaque and cannot be
constructed from a caller-asserted success Boolean. Their selected files and
receipt feed an additive `CatalogScaffoldEffects.prepareFromSource`; existing
`prepare` and request record constructors stay unchanged. The normal scaffold
edge still performs capability admission, tool observation, staging and target
commit. Schema3 is written at its existing provenance-write point before commit.
The effect request carries exact source-set and acquisition-policy bytes with
independently expected raw digests; parsed structures plus caller-asserted hashes
are insufficient. Acquisition observations bind that policy digest and actual
connected addresses/statuses for each network hop. Cache reads emit no new
network-hop observation. Source acquisition is fully settled before handing over a verified cache-backed
input; the scaffold operation reopens/captures and validates all relevant bytes.
No source-acquisition task obtains a new deadline from scaffold startup.

CLI adds explicit source-set/digest, policy/digest, cache root and offline options.
Its source route rejects mixing new source selection with direct catalog/archive
selectors. `catalog inspect` remains data preparation and never runs tools; the
new source preparation reports acquisition observations distinctly from execution.
The opaque prepared set exposes a pure `inspect` projection for no-provider CLI
listing. Its immutable source previews retain source identity, raw source-set and
policy digests and actual acquisition/hop facts, plus the existing CatalogPreview
with Selected=None. They contain no fabricated selected archive observation.
Order sources by origin then ordinal ID and providers by ordinal ID. Listing
neither rereads files nor resolves provider parameters or executes declarations;
the complete source set must pass collision validation before it is inspectable.
All projections preserve the same facts and refusal reasons. No implicit network
request is added to legacy catalog, doctor, refresh or upgrade.

## Data shape and custody

A source-set entry declares ID, origin, locator and expected manifest digest.
HTTPS adds immutable revision and expected envelope digest. The manifest names
its ID/revision, catalog member and ordered member records `{path,bytes,sha256}`,
plus `{templateSource,path}` archive associations. The closed population is the
catalog and exactly those archives; product/skill files remain inside the opaque
template archive and are governed by existing scaffold transport checks.

Cache keys are computed only from the source-format version, manifest digest and
expected envelope digest when applicable, never from URL/version alone. Every
cache use revalidates the complete manifest/member closure. Cached acquisition
receipts retain their original observation; a cache read does not claim a fresh
network request. Local sources are captured using contained, no-follow held
objects, not a check-then-follow symlink path. Source discovery cannot select a
network policy or silently trust a workspace-authored policy.

HTTP uses a closed BCL data-fetch edge: automatic redirects/decompression/proxy,
cookies and credentials disabled; normal TLS hostname/chain validation retained.
An independently selected exact HTTPS origin allowlist and public-address scope
govern each actual DNS/connect endpoint. Ports other than443, IP-literal hosts,
private/loopback/link-local/multicast/unspecified/mapped-special addresses refuse
in this first profile. Test-only injected transport cannot appear in the public
CLI or production selection. No shell/curl/process fallback exists.

The whole source operation owns streams, tasks, cancellation and staging from
before effect start. Cancellation/timeout stops useful work; retirement uses only
the same original reserve. Unknown closure remains an owned failure and cannot
produce a source handle. Native path custody reuses the existing Linux mechanisms;
there is no claim of hostile containment or unqualified cross-platform parity.

## Compatibility and delivery

Source preparation receives the scaffold target before any cache effect (inspection
supplies no target). Refuse cache/source/staging overlap with that target in pure
preparation and recheck physical containment at acquisition. The selected cache
root must already exist; source acquisition never creates missing parent paths
that could accidentally create a target. Existing/appearing scaffold targets are
preserved, with the existing final no-replace guard retained. Request byte arrays
are length-checked before defensive copying and then validated; returned payload
arrays are defensive copies. Manifest and envelope populations reject ordinal
case-insensitive file/ancestor collisions, including the fixed `source.json`.
The4096 payload-member limit excludes that one manifest member, so an envelope
can contain4097 regular files while retaining all existing byte bounds.
After terminal cleanup Unknown, the CLI retains the original operation passively
without repeated release/settlement calls. The bounded acquisition and retirement
attempt does not promise bounded CLI process exit. Genuine CLI qualification must
use the existing finite external custodian with original PID/birth, descendant and
EOF observations; process death does not establish semantic resource cleanup.

The opaque selected source carries its original target binding. None is genuinely
inspection-only: it cannot later enter prepareFromSource. Sourced preparation
requires the same target, provider ID, ordinal literal override list, catalog
bytes/raw digest and archive path/raw digest as the selected handle, including
when two providers share one template/archive. Before cache or scaffold effects,
recheck physical source/cache/staging versus target ancestry in both directions;
different path strings or mount aliases do not establish disjoint objects.

Keep current catalog contract3.0.0 and schema2 catalog bytes untouched. Additive
source interfaces live in Artifacts/Commands with matching `.fsi` baselines and
consumer tests. Schema3 is explicitly incompatible with old provenance readers;
those readers already refuse unknown schema. New readers preserve1/2 exactly.
Select coherent package version changes through normal release policy after API
review, not by inventing an unavailable dependency pin during implementation.
The source candidate keeps the SDD coherent set at 2.4.0: the additive source APIs
call for a minor advance to 2.5.0, which is selected with the coherent release
preparation (compatibility matrix, readiness baseline, goldens) at publication,
not by a partial bump in this source delivery. Existing schema1/2 data and constructors remain supported; exhaustive
Document-union consumers must add SourceCatalog and old binaries refuse schema3.
The prospective version is not a publication or installed-compatibility claim.

Spec/.fsi and representative semantic/API tests form the first checkpoint.
After reviewed compiler preflight, exercise those surfaces before corresponding
implementation. Continue disjoint contract/test work meanwhile. Then implement
pure codecs, provenance readers, acquisition/staging/cache, original-edge join and
CLI; qualify affected semantic tests, required exact-head CI and independent
review. Root separately selects the real package-only third-party/native journey,
publication and public installed acceptance. Retain the same issue/owner/PR.

## Ordinary and native source tests

Exactly four `withInspectionSources` consumers perform real source acquisition
I/O. Reuse the existing `CatalogNamespaceFactAttribute` from shared test support
and mark them `kind=provider-source-native`. Ordinary unfiltered CI keeps the
remaining 49 source and three CLI declarations; the four native facts report
skipped/not executed unless explicitly selected. Declaration counts are not
execution results, and a skipped case is never acceptance.

Use the maintained `catalog-runtime-validation.py` native-runtime/pid-namespace
route for a separately admitted current-binary run selecting all four facts.
The opt-in flag and namespace provide neither a qualified carrier nor source
acquisition authority. That run still requires actual Linux-x64, .NET10.0.12,
exact glibc2.44 plus every required export, complete selected input custody, and
a finite external custodian. Four executed passes, zero skips, exact source/input
joins and retirement evidence are required. Home carrier qualification remains
a prerequisite; ordinary CI does not close #935 or its parent #928.

A small actual-source census in the existing Python runner test suite checks the
complete four-case set, shared attribute order, trait and CI wiring before SDK
setup. Causal removals/additions must fail. This static preflight introduces no
executor, runtime authority or weakened host gate.
