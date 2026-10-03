namespace FS.GG.SDD.Knowledge

open System
open System.IO
open System.Text
open System.Text.Json

[<CLIMutable>]
type Evidence = { Locator: string; Repository: string; Revision: string; Path: string; Digest: string; Run: string }
[<CLIMutable>]
type Relation = { Kind: string; Target: string }
/// A concise finding; raw source, logs, attachments and snapshots have no storage field.
[<CLIMutable>]
type Record = {
    SchemaVersion: int; Id: string; Kind: string; Title: string; Summary: string
    Rationale: string; Limits: string; State: string; Basis: string
    Author: string; Created: string; Updated: string; AsOf: string; Scope: string; Applicability: string
    Evidence: Evidence array; Relations: Relation array
}
[<CLIMutable>]
type Version = { Record: Record; Revision: string; GitCommit: string }
[<CLIMutable>]
type Query = { Text: string; Kind: string; Scope: string; State: string; Id: string }
[<CLIMutable>]
type SizeReport = { Bytes: int64; Limit: int64; Growth: int64 }
[<CLIMutable>]
type ExportFile = { Path: string; Digest: string; Text: string }
[<CLIMutable>]
type Export = { Schema: string; HistoryIncluded: bool; Files: ExportFile array; InventoryDigest: string }

/// Shared current-record and Git-history API.
module Store =
    val byteLimit: int64
    val schemaText: string
    val check: root: string -> SizeReport
    val get: root: string -> id: string -> Version
    val all: root: string -> Version list
    val capture: root: string -> expectedRevision: string option -> record: Record -> Version * SizeReport
    val search: root: string -> query: Query -> Version list
    val related: root: string -> id: string -> Version list
    val getVersion: root: string -> id: string -> commit: string -> Version
    val history: root: string -> id: string -> Version list
    val export: root: string -> ids: string array -> byte array
    val restore: root: string -> bytes: byte array -> SizeReport
    val browse: root: string -> string
