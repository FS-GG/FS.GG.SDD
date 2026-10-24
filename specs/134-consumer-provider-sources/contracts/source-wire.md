# External provider source wire contract

This is the version1 source format implemented by feature134. It does not change
the existing catalog schema2/descriptor contract3.0.0. All objects reject unknown
and duplicate properties; all fields below are required, including explicit nulls.
Digests are `sha256:` followed by64 lowercase hexadecimal digits. Text is strict
UTF-8 without BOM; numeric fields use canonical nonnegative decimal integer JSON
tokens, not floats/exponents. Counts, lengths and depths obey plan.md hard limits.
No embedded command, executable hook, script or skill becomes authority by parsing.

## Source set

Root fields: `schema`, `sources`. `schema` is exactly
`fsgg.provider-source-set/1`. `sources` is a nonempty list of at most16 references.
A reference has exactly `id`, `origin`, `locator`, `manifestSha256`.

`origin` is `built-in`, `workspace` or `explicit`. IDs/revisions are nonempty text
of at most128 characters without controls; their values are opaque and never
concatenated into filesystem paths. IDs are unique across the complete set.
Stable order is origin rank then ID using ordinal comparison. Source origin never
grants execution permission or authorizes silent provider shadowing.

Local locator: `{ "kind":"local-directory", "path":"/absolute/path" }`.
HTTPS locator: `{ "kind":"https-envelope", "uri":"https://host/source.zip",
"revision":"immutable-author-revision", "envelopeSha256":"sha256:..." }`.
Reject a missing, extra or wrong-kind field. Local paths are absolute caller
selections; containment and actual physical identity are checked by the data edge,
not inferred by this parser. HTTPS origin/address permissions come only from the
independently selected acquisition policy.

## Source manifest and envelope

The manifest is `source.json` at the source root. Its fields are exactly:

| Field | Value |
|---|---|
| `schema` | `fsgg.provider-source/1` |
| `id` | Equal to the selected source reference ID |
| `revision` | Nonempty immutable author revision; HTTPS selection must match it |
| `catalog` | Relative path to the one existing-format catalog |
| `members` | Ordered records containing exactly `path`, `bytes`, `sha256` |
| `archives` | Ordered records containing exactly `templateSource`, `path` |

The member population is exactly one catalog and the uniquely associated opaque
template archives. A member cannot serve two roles. Each archive association
matches an existing selected descriptor's opaque TemplateSource; no package ID or
version is guessed from a URL or path. The existing scaffold policy still validates
the actual nupkg identity/version and selected TemplateId. The manifest excludes
itself, preventing a self-hash cycle; the caller supplies its complete raw digest.

Paths use `/`, contain no empty, `.` or `..` component, backslash, control, colon,
leading/trailing slash, or platform-ambiguous trailing space/dot. At most32
components/1024 UTF-8 bytes. Reject portable reserved device basenames and both
ordinal and case-insensitive collisions. Source manifests and envelope payloads
are ordinary files only. No symlink/hardlink/device or executable-file metadata
confers effect authority. File-mode requirements inside an opaque template archive
remain that existing transport's contract.

The HTTPS envelope is a ZIP containing exactly `source.json` and those declared
members. Reject missing/extra entries, directory entries, encrypted entries,
unsupported link/special-file attributes, duplicate/ambiguous member names and
all expanded/compressed/count limits before selection. Check streamed observed
bytes even if central-directory lengths or HTTP Content-Length claim less.
Do not extract nested archive contents in source acquisition; the nupkg is an
opaque member subsequently validated by the existing template transport.

Local directory sources normalize to that same population. Capture its complete
observed member census with no-follow containment and revalidate the selected
directory and files; an incomplete/changed census refuses. The source root itself
may not be used as the target or source cache/staging directory.

Scaffold mode supplies its target before source/cache effects. Inspection mode's
absent target is not a future wildcard. Preserve the original target binding in
the opaque selected source; the existing executor refuses an inspection-only
handle, a different target/provider/literal override list or different catalog/
archive bytes. Physical ancestor/descendant aliases between target and source/
cache/staging refuse before effects, even when path spellings differ.

## Acquisition policy

Root fields: `schema`, `allowedOrigins`, `maximumRedirects`, `acquisitionSeconds`,
`maximumTransferBytes`, `maximumExpandedBytes`. Schema is
`fsgg.provider-source-policy/1`. Allowed origins are unique canonical HTTPS
authorities, at most16; they have no path/query/fragment/userinfo. Empty is valid
for a local-only source set. Network locators must match the selected allowlist.
Bounds can only narrow the hard ceilings in plan.md.

No proxy inheritance, cookies, credentials, TLS bypass, HTTP fallback or automatic
redirects. The initial profile accepts DNS hostnames on port443, not IP-literal
hosts. Resolve once under the original deadline; reject the complete answer set
if any address is outside the public unicast profile, then connect to a validated
address directly without a second implicit DNS lookup. Record the actual connected
endpoint. Reject private/loopback/link-local/unspecified/multicast/documentation/
benchmark/reserved ranges, including IPv4-mapped versions. This is a supported
closed acquisition profile, not a claim that any HTTPS service is trusted.

Requests use HTTP/1.1 without protocol fallback and with automatic decompression
disabled. No source request supplies headers or a body. Redirect targets must be
HTTPS on the same selected origin, pass the same checks and fit the independently
selected0..3-hop bound; no authentication is forwarded. Any non-success other
than an admitted redirect refuses. The owner records one original request/hop
sequence; source retry is never an automatic recovery action.

## Source observation

Fields are exactly `schema`, `sourceSetSha256`, `acquisitionPolicySha256`, `source`,
`revision`, `acquisition`, `resolvedUri`, `redirects`, `networkHops`,
`catalogSha256`, `templateSource`, `archiveSha256`. Schema is
`fsgg.provider-source-observation/1`; `source` is the complete selected reference.

Acquisition is `local-copy`, `https-fetch` or `cache-read`. Local and cache reads
have `resolvedUri:null`, empty redirects and no current network hops. HTTPS fetch
retains the final URI plus every followed redirect and connected response hop;
each hop is `{ "uri":..., "address":..., "statusCode":... }`, maximum4. Their
origins/ordering/statuses must agree, the last status is200, and the final URI is
the last hop. A cached historical receipt is retained separately; it cannot be
projected as a fresh network observation. The verified manifest/source revision
and actual catalog/archive digests remain determining on every acquisition kind.

## Schema3 scaffold provenance

Root is exactly `{ "schemaVersion":3, "source":<observation>,
"catalog":<unchanged strict schema2 record> }`. The nested catalog must be schema2
and its raw catalog digest, descriptor TemplateSource and observed/declaration
archive digests must agree with the source observation. The existing schema2 codec
continues to check parameters, skills, generated paths, tool/policy/evidence and
ownership correspondence. Do not duplicate those semantics in the source codec.

The schema3 writer executes inside existing scaffold staging before no-replace
target commit and reads back exact bytes. Document readers select1/2/3 explicitly;
ownership projects through the nested schema2 ownership record. Malformed source
provenance cannot select a nested legacy record as a fallback. Structural parsing
never proves genuine acquisition, installed execution or source authenticity.
