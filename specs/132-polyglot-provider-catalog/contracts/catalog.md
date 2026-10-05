# Catalog schema 2 / descriptor protocol 3.0.0

All mapping keys are closed: unknown keys refuse rather than silently weakening declarations.
All fields below are required except `default`; every list must be explicit (empty is permitted
unless stated). IDs and metadata strings are nonblank. IDs are exact, case-sensitive literals.
Digests are `sha256:` followed by exactly 64 lowercase hex digits. They are declarations, not
proof of authenticity or content equality. Catalog IDs, provider IDs and descriptor IDs are
unique within their respective catalog namespaces.

Root: `schemaVersion`, `id`, `revision`, `digest`, `providers` (nonempty).
Provider: `id`, `displayName`, `help`, `language`, `productShape`, `descriptorId`,
`descriptorRevision`, `descriptorDigest`, `contractVersion`, `templateSource`, `templateId`,
`platforms` (nonempty unique scalar IDs), `parameters`, `identities`, `tools`, `capabilities`,
`evidence`, `skills` (unique nonblank scalar IDs).

Parameter: `key`, `kind` (`string`, `enum`, `exact-version`), `required` (true/false),
`prompt`, `help`, optional scalar `default`, `values` (nonempty for enum, empty otherwise),
`validation` mapping (`nonEmpty` boolean, `minLength` nonnegative integer,
`maxLength` integer >= minLength, `allowedValues` unique scalars).
Lengths count UTF-16 code units. Allowed values and enums compare ordinally. No arbitrary regex
or executable validator is supported. Invalid defaults refuse before override resolution.
Exact versions are 1–128 ASCII characters, start with a digit, contain only letters/digits/dot/
underscore/hyphen/plus, and contain no empty dot component. This deliberately bounded lexical
literal is not SemVer and is not a package resolver: `1.22`, `2026-10-05`, `1.2.3-preview.1`
are valid; ranges, whitespace, `latest`, `*`, `^1`, `>=1`, `1.x`, `1.X`, trailing dots refuse.
Accepted literals retain exact bytes. A literal component exactly `x` or `X` refuses.

Identities: `rawName`, `packageIdentity`, `codeIdentifier`, each a distinct declared required
parameter key. Values may coincide, but bindings must remain independently named. All identities
are explicit inputs/defaults; this window has no derivation engine.

Tool: `id`, `version` (exact-version literal), `platforms` (nonempty provider platform refs).
Evidence: `id`, `format`, `path`, `required` boolean. Paths are declared data, not reads/writes.
Capability: `id`, `required`, `platforms` (nonempty refs), `toolIds`, `evidenceIds`, `binding`.
Binding: `kind: semantic-only` alone, or `kind: command`, `executable`, `arguments` scalar argv,
`workingDirectory` nonblank scalar, `timeoutSeconds` positive integer, `costClass` nonblank scalar, `environmentIds` unique scalars.
Semantic-only means an explicitly command-free obligation. Required command/evidence references
must exist regardless of requiredness of the referenced declaration. Structural validity does not
establish Governance support: its neutral executable capabilities may refuse semantic-only.

Resolution validates the complete catalog, selects one exact provider ID, rejects duplicate and
unknown overrides, overlays defaults, and validates all effective values. Missing optional values
remain absent. The result carries catalog and descriptor pins plus the normalized declaration,
sorted effective parameters, and exact separate identity values. No observed tool/evidence facts
or successful scaffold receipt are synthesized. Malformed documents return a deterministic
`catalog.malformed` diagnostic with field path; semantic diagnostics use stable `catalog.*` codes.

## C2.1 integrity and inspection

Inspection requires an explicitly selected local catalog and `sha256:` followed by the exact
64 lowercase hexadecimal digits of its raw bytes. UTF-8 decoding is strict. C1 declaration
parsing is unchanged; the new integrity layer additionally verifies each descriptor and catalog
semantic digest before preparation. No registry lookup, fallback, executable probe or write occurs.

Canonical semantic bytes are compact UTF-8 JSON without BOM or trailing newline, using the
default System.Text.Json escaping. Field order follows the existing catalog schema. Descriptor
bytes omit only that descriptor's `descriptorDigest`; catalog bytes omit only the root `digest`
and include verified descriptor digests. Providers, parameters, tools, capabilities and evidence
sort by their ordinal identity keys; set-valued lists sort ordinally. Parameter defaults are
explicit strings or `null`. Ordered command argv and literal values retain their order and
content, including Unicode, control characters, whitespace and empty arguments. Raw byte
identity therefore changes with document formatting while semantic identity remains stable.

`fsgg-sdd catalog inspect --catalog <file> --catalog-sha256 <digest>` emits a deterministic
`fsgg.catalog-preview/v1` JSON projection by default. `--text` and `--rich` show the same facts
with existing output capability rules. Without `--provider`, all verified provider metadata and
parameter prompts are exposed. An explicit provider may use repeated `--param key=value`;
values are split only at the first equals sign and empty values are preserved. Parameters
without a provider, duplicate valued options and unsupported options refuse with located
diagnostics. Success reports `prepared` and `observations: null`, never provider success.
`scaffold --catalog` is unavailable and refuses before file or process effects.
