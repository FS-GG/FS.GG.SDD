namespace FS.GG.SDD.Knowledge

open System
open System.IO

module Workspace =
    let guidance = """# Project knowledge

Capture concise reusable findings with `fsgg-sdd knowledge capture --record finding.json`.
Use architecture, decision, diagnostic, experiment, bug-fix, qualification,
operation and handoff records. Summarize rationale and limits; link canonical
source/evidence with revision or run identity. Keep failed outcomes. Distinguish
proposal/open-question/reported/observed/accepted/superseded/retracted and an
explicit evidence/reported/inference/proposal basis. Review establishes truth.

Before work, use `knowledge search --text <question>`, `get --id <id>`,
`related --id <id>` and `history --id <id>`. Updates require --expected <digest>.
Concurrent edits require explicit reconciliation. Capture through this same API
from CLI, F# scripts or agents; ordinary commits and PRs retain past versions.
The current .fsgg/knowledge records and schema share a 10 MiB byte budget.
`knowledge check` enforces the schema, exclusions and budget in CI.
Consolidate duplicate/superseded findings while retaining useful conclusions and
Git history. No source copies, raw logs, transcripts, document dumps, build
artifacts, snapshots, backups or indexes belong in the canonical store.

`knowledge browse` renders canonical findings with provenance. Search reads
records directly; an optional cache belongs under ignored .fsgg/cache.
`knowledge export --ids id1,id2 --archive findings.json` exports selected current
records; `restore` refuses overwrites. It does not include history. Full-history
backup is an explicit git bundle outside .fsgg/knowledge, followed by a cacheless
clone and retrieval verification before retiring any old custody.

Shared records are Git-reviewed. Private findings use a separately configured
--store outside the shared repository. Classification labels do not restrict
access. Credentials and restricted payloads remain outside canonical records;
shared references must not leak private titles, paths or snippets.
"""
    let ignoreBlock = """
# FS.GG.SDD project knowledge: canonical records tracked; optional cache ignored.
!.fsgg/
!.fsgg/knowledge-guide.md
!.fsgg/knowledge/
!.fsgg/knowledge/schema.json
!.fsgg/knowledge/records/
!.fsgg/knowledge/records/*.json
.fsgg/cache/
.fsgg/.knowledge-writer.lock
"""
    let initialRecord = {
        SchemaVersion = 1; Id = "project-knowledge"; Kind = "guidance"; Title = "Project knowledge capture and retrieval"
        Summary = "Capture concise reusable findings, evidence references and limits through the shared knowledge API before development work."
        Rationale = "Portable current records and Git history make the same project context available to people, scripts and agents."
        Limits = "Structural checks do not establish usefulness or truth; evidence and review remain required."
        State = "accepted"; Basis = "evidence"; Author = "fsgg-sdd"; Created = "2026-10-02"; Updated = "2026-10-02"; AsOf = "2026-10-02"
        Scope = "project"; Applicability = "Typed SDD workspace initialization"
        Evidence = [| { Locator = ".fsgg/knowledge-guide.md"; Repository = ""; Revision = ""; Path = ".fsgg/knowledge-guide.md"; Digest = ""; Run = "typed-sdd-initialize/v1" } |]
        Relations = [||]
    }
    let initialFiles = [
        ".fsgg/knowledge/schema.json", Store.schemaText
        ".fsgg/knowledge/records/project-knowledge.json", System.Text.Json.JsonSerializer.Serialize(initialRecord, System.Text.Json.JsonSerializerOptions(WriteIndented = true))
    ]
    let initialize root =
        let store = Path.Combine(root, ".fsgg", "knowledge")
        Directory.CreateDirectory(Path.Combine(root, ".fsgg")) |> ignore
        let guide = Path.Combine(root, ".fsgg", "knowledge-guide.md")
        if not (File.Exists guide) then File.WriteAllText(guide, guidance)
        let ignore = Path.Combine(root, ".gitignore")
        let prior = if File.Exists ignore then File.ReadAllText ignore else ""
        if not (prior.EndsWith(ignoreBlock, StringComparison.Ordinal)) then File.AppendAllText(ignore, ignoreBlock)
        if File.Exists(Path.Combine(store, "records", "project-knowledge.json")) then Store.check store
        else Store.capture store None initialRecord |> snd
