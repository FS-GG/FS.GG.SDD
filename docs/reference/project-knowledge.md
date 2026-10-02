# Git text project knowledge

The source API and read-only CLI use one canonical UTF-8 contract. This feature
reuses FS.GG.SDD.Artifacts; it does not change generated workspaces or claim an
installed release. [Owning roadmap](../roadmaps/typed-sdd-project-knowledge.md).

A project owner's store is `.fsgg/knowledge/manifest.json`, `schema.json`,
`README.md` and independently editable `records/<id>.json`. No central topic index
is required. Manifest content is `{"schemaVersion":1}`. Use `Knowledge.schemaJson`
for the matching JSON Schema. Parser semantic checks additionally enforce date
ordering, evidence provenance, stable links and supersession. The store is optional
until explicitly created; reads never initialize it. All regular canonical bytes,
including schema, manifest and guidance, must total at most **10,485,760**.

The [synthetic representative corpus](../../tests/fixtures/project-knowledge/.fsgg/knowledge/README.md)
contains a decision, operating lesson, failed experiment and cause/fix. Its findings
are illustrative, not verified production claims. Every record requires:

| Field | Contract |
| --- | --- |
| schemaVersion / id | Integer 1; stable lowercase ASCII slug, same as filename |
| kind | architecture, decision, cause-fix, experiment-positive, experiment-negative, qualification, operating-lesson |
| title / conclusion / limits | Nonempty concise human-readable strings |
| created / updated | ISO calendar dates; updated does not precede created |
| scope / attribution | Nonempty arrays of distinct nonempty strings |
| status | observed, accepted, proposed, open, superseded |
| evidence | Nonempty array; each entry has repository, revision, path, digest, run, url; unknown values are empty strings; at least one revision, digest or run is required |
| related / supersedes | Distinct stable IDs present in this store; no self-link; superseded target must explicitly have superseded status |

Evidence paths are repository-relative without `..`, absolute paths or drive
prefixes. URLs must be credential-free HTTP(S) references. They are never fetched:
a URL's existence is not verification. Attribution describes who owns the finding;
status is authored assertion, not an automated proof. Missing fields, unsupported
fields (including `private`), duplicate JSON keys, identities, broken links and
invalid UTF-8 refuse the whole store with stable diagnostic codes. Symbolic links,
reparse points and noncanonical files/directories refuse CLI reads. Source copies,
raw logs, dumps, transcripts, caches and embedded backups are unsupported.

```sh
fsgg-sdd knowledge check --root .
fsgg-sdd knowledge search --root . --query 'café' --kind operating-lesson --status observed --scope 日本語
fsgg-sdd knowledge get --root . --id lesson
fsgg-sdd knowledge related --root . --id architecture
```

JSON is the default (`--json`); `--text` and `--rich` project the same records,
provenance, diagnostics and byte contributors as portable plain text. Success is
exit 0; invalid input, missing records or unreadable stores return exit 1. `get` and
`related` require `--id`; `search` accepts optional `--query`, `--kind`, `--status`,
`--scope`. Unknown options and missing/repeated values refuse before reading.
Search uses ordinal case-insensitive Unicode substring matching over record prose,
identity, scope, attribution and evidence. Filters are exact strings. Results sort
by identity. Related results include incoming/outgoing related and supersession
links; superseded findings stay visible. Search is linear over canonical records.

F# callers supply exact bytes with store-relative paths to `Knowledge.load`, then
call `search`, `get` or `related`. See the [thin fsx example](examples/project-knowledge.fsx).
No optional `.fsgg/cache` sibling is read. The CLI's explicit `ReadStore` effect
only enumerates the store, never writes files, invokes a process or accesses the
network. A failed load provides diagnostics rather than partial query results.
Budget diagnostics disclose total, limit, remaining bytes and largest contributors.

Repository visibility is the access boundary: knowledge in public Git is public.
No record flag promises secrecy. Keep restricted payloads and credentials in their
existing custody and use safe non-secret references. Review owns truth, concision
and disclosure; structural validation cannot prove them. Direct Git text editing is
possible; controlled writes, Git history, export/restore, automatic typed creation,
provider composition and existing-project adoption remain later roadmap work.
