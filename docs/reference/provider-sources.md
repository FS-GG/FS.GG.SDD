# External provider sources

Provider sources let a consumer select an independently authored catalog and its
template archives. The producer supplies a pinned `source.json` manifest and the
exact catalog/archive population described by the [source wire contract](../../specs/134-consumer-provider-sources/contracts/source-wire.md).
The catalog and descriptor formats remain unchanged.

This implementation is under qualification. API-shape compilation has passed;
production source, installed third-party creation, build/test, offline recreation
and coherent publication still require their acceptance runs.

## Select and inspect

Create an empty private cache directory and select it explicitly. The source set
contains local directories or pinned HTTPS ZIP envelopes. Each reference carries
the complete raw manifest SHA256; HTTPS also requires a revision and complete raw
envelope SHA256. The independently selected acquisition policy limits allowed
HTTPS origins, redirects, transfer/expansion and the original acquisition window.

```sh
fsgg-sdd catalog inspect \
  --source-set /consumer/sources.json --source-set-sha256 sha256:<raw-digest> \
  --acquisition-policy /consumer/acquisition.json --acquisition-policy-sha256 sha256:<raw-digest> \
  --source-cache /consumer/source-cache --json
```

With no `--provider`, this lists every verified provider, ordered by source origin,
source ID and provider ID. Duplicate source/provider identities refuse the whole
set before cache publication or selection. `--provider <id>` selects one existing
provider; repeated `--param key=value` preserves literal values and their order.
Inspection may populate the selected cache, but it does not install a template,
probe a provider tool or create a target.

## Create from a selected source

Use `scaffold` with the same source/acquisition/cache options, plus the existing
independent execution selections:

```sh
fsgg-sdd scaffold \
  --source-set /consumer/sources.json --source-set-sha256 sha256:<raw-digest> \
  --acquisition-policy /consumer/acquisition.json --acquisition-policy-sha256 sha256:<raw-digest> \
  --source-cache /consumer/source-cache \
  --provider third-party-provider --param rawName=Example \
  --root /consumer/new-workspace \
  --admission-policy /consumer/execution.json --admission-policy-sha256 sha256:<raw-digest> \
  --platform linux-x64 --transport-executable /qualified/sdk/dotnet \
  --preflight-timeout-seconds 30 --scaffold-timeout-seconds 60 --json
```

Parameter names and the execution policy come from the selected provider contract.
The example budgets must fit that policy. A source declaration or acquisition
policy grants no permission to execute provider commands. Direct-catalog and
source options cannot be mixed. The target is bound before source/cache effects;
an inspection-only handle cannot later authorize an arbitrary target.

The existing catalog executor validates the selected nupkg identity, TemplateId,
tool/capability policy and supported host. Its current host contract remains Linux
x64, .NET10.0.12 and exact glibc2.44 with the required native exports. The source
edge uses that same physical-custody contract. No origin label, digest or fixture
substitutes for host or genuine installed-provider qualification.

## Offline use and provenance

Add `--offline` to either command to consume an already verified complete cache.
This performs no DNS, HTTP, implicit installation or automatic cache repair. A
missing or changed entry refuses. Original local source directories need not
remain present once the cache is complete. Current cache reads have empty network
observations; retained acquisition metadata records the original operation.

Source scaffolding writes schema3 provenance inside the existing staging
transaction before publishing the target. It contains the source observation and
the unchanged strict schema2 catalog execution/ownership record. New readers
accept schemas1/2/3; older readers correctly refuse schema3. The public
`ScaffoldProvenanceDocument.Document` union adds `SourceCatalog`, so exhaustive F#
consumers must handle that case. Existing request record constructors and return
types are preserved.

Doctor reports malformed provenance as observed and non-coherent. Refresh and
upgrade refuse mutations based on malformed records. Owner backfill cannot
rewrite schema2/3 original produced-path observations or flatten their wrapper;
a backfill requiring a changed ownership record therefore refuses before writes.
Legacy schema1 backfill remains supported.

An unresolved acquisition, publication or cleanup retains its original owner.
Passive observation does not retry a request, resend publication or renew a
cleanup clock. CLI reporting preserves the original failure before reporting
unresolved release. Once the original runner is terminal Unknown, the CLI holds
the same owner passively without polling release or restarting cleanup.

The acquisition and retirement attempt has a finite budget; independent CLI
process exit after Unknown is not promised. Actual CLI qualification must run
under the existing finite external custodian, which owns the original process
birth/PIDfd, descendants and output EOF through its included deadline. Terminating
that process never converts unknown resource or pathname cleanup into success.
That native qualification remains outstanding.
