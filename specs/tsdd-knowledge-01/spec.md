# Git text project knowledge — TSDD-KNOWLEDGE-01.1

Tier 1: new public API and read-only CLI contract. Canonical machine authority is
UTF-8 JSON under `.fsgg/knowledge`: `manifest.json`, `schema.json`, `README.md`,
and `records/<stable-id>.json`. No central topic index is required. Every regular
canonical byte counts toward the inclusive 10,485,760-byte ceiling. Unknown files,
invalid UTF-8, duplicate JSON keys/identities, links outside the store and symbolic
links refuse the store with explicit diagnostics. Evidence paths are relative to
the repository; safe URLs are references only and are never fetched or verified.

Records carry schemaVersion, id, kind, title, conclusion, limits, created, updated,
scope, status, attribution, evidence, related and supersedes. Evidence carries
repository, revision, path, digest, run and url; unavailable values are empty
strings, not fabricated provenance. At least one revision/digest/run is required.
Kinds: architecture, decision, cause-fix, experiment-positive, experiment-negative,
qualification, operating-lesson. Status: observed, accepted, proposed, open,
superseded. Supersession requires the prior record to be explicitly superseded.

Reads return identities, status and provenance through one public F# contract.
Search is Unicode ordinal case-insensitive substring matching with optional kind,
status and scope filters. Related navigation includes incoming and outgoing links.
Malformed stores never return partial query results. No writes, cache reads or
network operations occur. Repository visibility controls privacy; public Git has
no per-record secrecy flag. Raw logs/source/attachments/backups are unsupported.

Acceptance: disclosed four-finding corpus; API/CLI equivalence; Unicode filters,
related/superseded results; duplicate/schema/path diagnostics; ignored sibling
caches; immutable read snapshots; exact aggregate budget boundary. History/export,
controlled writes, generated initialization, providers and publication are later
milestones. Pure parsing has no state protocol warranting a Quint model.
