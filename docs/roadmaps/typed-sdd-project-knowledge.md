# Typed SDD project knowledge — TSDD-KNOWLEDGE-01

Owner: FS.GG.SDD; provider composition: FS.GG.Templates; content authority: each
project owner. Routine delivery. [Unified §9.8](https://github.com/FS-GG/.github/blob/main/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md#98-feature-parts-and-subroadmap-index)
and [selected requirements §9.9.1](https://github.com/FS-GG/.github/blob/main/docs/2026-09-07-154210-fs-gg-unified-development-roadmap.md#991-project-knowledge-from-typed-sdd-initialization).
This owning roadmap is the single milestone completion authority.

Every newly initialized Typed SDD project must eventually include concise,
evidence-backed Git text knowledge usable by humans, scripts and agents without
a service, cache or previous session. Architecture, decisions, causes/fixes,
positive and negative experiments, qualification and operating lessons share one
contract. Current canonical bytes, including metadata/guidance, are limited to
10,485,760 inclusive; raw source/logs/transcripts/cache/backups are excluded.
Repository visibility governs privacy; public Git has no per-record secrecy flag.

## Evidence and decisions

Planning inspected SDD protected `388e4e0dbb216de8a37687818af042030d4e26c5`
and Templates `908da309c9a52490cf914c3f2d5ce1eaf1188b2e`. SDD had no generic
knowledge API/CLI. Existing artifact parser/serialization conventions and standalone
CLI dispatch are reusable; lifecycle artifact shapes and private typed authority
transactions are not knowledge records. Reuse existing Artifacts assembly (public
2.0.3 identity is not permission to republish). No additional package dependency.

Use minimal JSON under `.fsgg/knowledge/`, one record per stable identity, manifest,
schema and concise capture guidance. No central topic index, SQLite, embeddings,
service or cache authority. Evidence URLs are never fetched or treated as verified.
Pure read operations need no Quint model. Controlled writes in milestone .2 need
an actual concurrent write/recovery model and replay against production F#.

Typed knowledge creation is selected by effective `typed-sdd`. Fable-game currently
uses typed omission; other providers use sdd omission. No generated initialization
or provider defaults change in .1. Source merge, publication, clean installed
creation and retained-project adoption are distinct acceptance boundaries.

## Milestones

- [ ] **TSDD-KNOWLEDGE-01.1 — Read/query canonical contract.** Ready independently.
  Deliver schema/design, public F# parse/load/search/get/related, read-only CLI
  check/search/get/related and thin fsx usage. Disclosed four-finding corpus proves
  identity/status/provenance parity, Unicode filters, related/superseded findings,
  cacheless/no-mutation reads, invalid schema/duplicates/paths/links and exact byte
  boundary. [Specification](../../specs/tsdd-knowledge-01/spec.md),
  [plan](../../specs/tsdd-knowledge-01/plan.md),
  [reference](../reference/project-knowledge.md). Source implementation and test
  execution must be qualified before checking this milestone.
- [ ] **TSDD-KNOWLEDGE-01.2 — Capture/reconcile/restore without loss.** Depends .1.
  Controlled add/update/supersede/import uses expected fingerprints and atomic
  budget checks; Git history and selected export distinguish current-record export
  from a clone/bundle preserving history. Missing/shallow history returns explicit
  Unknown/unavailable. Two writers cannot clobber; concurrent growth cannot exceed
  budget; interruptions retain old or complete new bytes. Owner changes refuse.
  A literate Quint protocol models real write/retry/recovery; bounded replay checks
  the production reducer. No automatic Git commit/push or private migration.
- [ ] **TSDD-KNOWLEDGE-01.3 — Fresh typed creation and capture checks.** Depends .2.
  Join actual typed init/scaffold boundaries; seed honest observable lifecycle
  facts, manifest and guidance idempotently without overwriting owner content.
  Include knowledge check in normal CI/check routes and capture guidance in agent
  surfaces. Ignore rules must preserve canonical Git inclusion and ignore optional
  caches. Prove provider-less typed init, rerun, dry-run/refusal, initial ordinary
  commit and later update commit. Ordinary sdd/none/legacy selection remains scoped
  to its selected contract; scaffold invents no background commits.
- [ ] **TSDD-KNOWLEDGE-01.4 — Every supported provider composes the contract.**
  Depends .3; Templates fixture preparation can follow .1. Re-read provider/wizard
  producers and actual descriptor matrix, including Python and Rendering game/app.
  Providers invoke the SDD initializer rather than vendoring schema/content. Exact
  candidates prove explicit typed and omitted fable-game parity, other selected
  omission behavior and actionable incompatible-version refusal.
- [ ] **TSDD-KNOWLEDGE-01.5 — Publish and qualify installed clean receivers.**
  Depends .4 and independent release gates. Assign versions only against current
  release state; publish coherent exact bytes, record digests/pins, and adopt the
  actual minimum in Templates/wizard/materializer. Every supported typed family
  captures four findings, commits/clones cachelessly, queries/history/exports/
  restores with provenance/privacy and exact boundary/refusal. Initial commit
  contains canonical files; cache/raw payloads absent. This boundary supports the
  first installed new-project availability claim.
- [ ] **TSDD-KNOWLEDGE-01.6 — Preserve retained-project knowledge during adoption.**
  Depends applicable .2–.5 artifacts. Re-read cohort, provide explicit preserving
  upgrade/extraction, dry-run/diff, conflict refusal, bounded extraction, missing
  source references and interrupted retry/restore with Git history. Original
  source/log corpora remain in their original custody. Sanitized fixtures prove
  extraction independently; private operational migration requires owner evidence.

## Current horizon and verification

Only .1 implementation is admitted. New source and semantic fixtures reuse existing
Artifacts/CLI projects; no package versions/pins, initialization/scaffold/providers,
publication, private data or accepted V2 activation change. Focused Artifacts and
CLI tests, signature baselines, existing dispatch/help checks and normal required
source gates qualify the actual candidate. Compute execution uses the programme's
finite grant; static review never implies restore/build/test success. Later work
requires fresh producer/receiver readback. Unified link/progress projection follows
authoritative source landing and remains asynchronous.
