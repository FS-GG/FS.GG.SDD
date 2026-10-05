# Retained public findings fixture

These six manually curated proposals summarize public SDD documentation at
`9df47707ab22b2486400fb359f381e08c4de90df`. The original documents remain in
their existing locations. `manifest.json` records their public locators and
SHA-256 digests. The fixture contains no source dump, raw log or private corpus.

| Finding | Source | Retained limit |
|---|---|---|
| Typed authority boundary | `docs/typed-sdd-lifecycle.md` | Documentation claim; no installed/provider acceptance |
| Non-saturating damage control | Q1 damage slice and qualification report | Reported failing mutation; no new execution |
| Q1 producer-only acceptance | Q1 qualification report | Consumer replay and production authority remain separate |
| Liveness defect, cause and fix | Q1 qualification report | Reported finite-model result and failed first repair |

Proposal records retain the source's last-change date as their historical
created/updated/as-of date: 2026-08-26 for Q1 and 2026-10-03 for the lifecycle
document. These are fixture attribution dates, not inferred experiment-event
dates. No import timestamp promotes a record to observed or accepted.

Review the source references, conclusions, limits, dates and relations before
selecting a proposal. The focused tests explicitly apply selected records with
`Store.capture`; reading a proposal does not initialize or mutate a store.
An initial record uses no expected revision. A changed record requires the
owner-selected digest returned by `Store.get` after explicit reconciliation.
Identical re-application preserves record bytes and dates.

`changes/owner-edited.json` and `changes/changed-conclusion.json` are disclosed
synthetic follow-up inputs. Their checked-in unified diff makes the changed
wording, author, date and semantic state visible. The later proposal retains
the historical as-of date and remains a proposal. It cannot overwrite the
owner's edit using the original digest or an unqualified restore. The test's
explicit reconciliation retains the owner's limit before applying against the
current digest; that test action is not a production import service.

Selected-record export restores current record bytes without Git history. The
Git fixture separately makes two ordinary commits, creates a bundle outside
the canonical directory, and recovers both versions in a fresh clone without
cache. It retires no real store and grants no authority over private BAR data.
