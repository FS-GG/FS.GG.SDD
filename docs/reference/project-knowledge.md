# Concise project knowledge

Project knowledge records reusable findings and evidence references under
`.fsgg/knowledge/`. Source code, raw logs, transcripts, document dumps, build
artifacts, generated indexes and backups remain outside that directory. Each
current record is UTF-8 JSON at `records/<id>.json`; `schema.json` identifies
`fsgg.knowledge/1`. The [shared API](../../src/FS.GG.SDD.Knowledge/Store.fsi)
serves the CLI, F# scripts, Markdown browse view and agent guidance.

Typed scaffold creates initial knowledge before reporting success. Successful
Typed SDD authoring or migration also initializes it. Generic `init` seeds
`.fsgg/knowledge-guide.md` without changing lifecycle selection or creating a
knowledge store. `fsgg-sdd knowledge initialize --root .` explicitly initializes
knowledge and preserves authored guidance and existing records. It appends ignore
exceptions for canonical records and excludes `.fsgg/cache/`. The caller's normal
initial `git add .` and commit include the canonical records; the tool never
starts a background commit or push.

## Capture and retrieve

Write a concise finding JSON, then use the common write path:

```sh
fsgg-sdd knowledge capture --root . --record finding.json
fsgg-sdd knowledge search --root . --text "failed experiment"
fsgg-sdd knowledge get --root . --id experiment-42
fsgg-sdd knowledge related --root . --id bug-42
fsgg-sdd knowledge browse --root .
fsgg-sdd knowledge check --root .
```

A record has `SchemaVersion: 1`, a stable `Id`, `Kind`, `Title`, `Summary`,
`Rationale`, `Limits`, `State`, `Basis`, `Author`, `Created`, `Updated`, `AsOf`, `Scope`,
`Applicability`, `Evidence` and `Relations`. Dates use `yyyy-MM-dd`. Evidence
references contain `Locator`, `Repository`, `Revision`, `Path`, `Digest` and
`Run`; a revision or run identity is required. Relations contain `Kind` and
`Target`, linking experiments to outcomes and bugs to fixes. Initial guidance
provides a [complete record example](../../src/FS.GG.SDD.Knowledge/Workspace.fs).

Kinds are architecture, decision, diagnostic, experiment, bug-fix,
qualification, operation, handoff and guidance. States are proposal,
open-question, reported, observed, accepted, superseded and retracted. Basis is
evidence, reported, inference or proposal. Observed and accepted records require
an explicit evidence basis. The schema cannot establish usefulness or truth;
review must assess evidence, rationale and applicability.

Capture returns the current byte digest and a size/growth report. To update a
record, retrieve it and pass `--expected <digest>` with the edited record.
Mismatched digests refuse; reconcile concurrent edits explicitly. Ordinary
commits preserve prior findings. Consolidate duplicates and superseded entries
through review while retaining useful conclusions and provenance. Source imports,
raw attachments, unknown record fields, code fences and oversized prose fields
are refused. Concise prose can still be misleading or leak information; review
remains responsible for content and access.

## Size and CI

The exact aggregate limit is 10,485,760 bytes for every current canonical file,
including schema metadata. Writes and restores report growth and refuse an
over-limit candidate. `knowledge check` validates disk content independently of
the write API, rejects unexpected files and unsafe links, and enforces the same
limit. Run it in the project's required CI after restoring its pinned CLI:

```sh
dotnet tool restore
dotnet tool run fsgg-sdd knowledge check --root .
```

Git history and ignored caches are outside the current-store measurement. Search
reads the records directly; deleting a cache does not affect retrieval. The
[source `.fsx` example](../examples/knowledge.fsx) uses the same public API.
A published package reference remains part of the separate publication milestone.

## Versions and recovery

```sh
fsgg-sdd knowledge history --root . --id experiment-42
fsgg-sdd knowledge get-version --root . --id experiment-42 --commit <exact-Git-SHA>
fsgg-sdd knowledge export --root . --ids experiment-42,bug-42 --archive /outside/findings.json
fsgg-sdd knowledge restore --root . --archive /outside/findings.json
```

Historical retrieval requires Git and an exact commit identity. Current export
preserves the selected records' exact UTF-8 bytes, metadata and inventory digests;
its `HistoryIncluded` is false. It does not export the repository or private
stores. Restore validates the complete candidate and combined byte budget before
writing; differing current records refuse rather than overwrite project edits.
An interruption can leave a subset of individually valid records; repeating the
same restore completes it without overwriting different authored content.

Full-history backup is a separate, explicit repository operation. Create a Git
bundle outside `.fsgg/knowledge`, clone that bundle into a fresh directory, and
check retrieval and history with `.fsgg/cache` absent before retiring old custody.
Commit the intended findings first; a Git bundle does not preserve uncommitted
edits. This operation exports the whole repository and requires the repository's
access boundary. Do not represent a selected-record export as a history backup.

Shared Git custody is the public access boundary. Configure a private store
explicitly with `--store /private/path` outside the shared workspace; never select
it for a shared export. Labels do not restrict access. Keep credentials and
restricted payloads outside canonical records, and ensure shared references do
not disclose private titles, paths or snippets. Tests use synthetic private
fixtures; no private project corpus is imported by initialization.
