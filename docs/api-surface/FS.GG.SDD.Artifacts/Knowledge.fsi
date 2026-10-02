namespace FS.GG.SDD.Artifacts

/// Cache-independent Git text knowledge. All validation and queries are pure.
module Knowledge =
    type Diagnostic = { Code: string; Path: string; Message: string }
    /// Exact canonical bytes with a slash-separated path relative to the store.
    type Snapshot = { Path: string; Bytes: byte array }
    type Evidence = {
        Repository: string; Revision: string; Path: string; Digest: string
        Run: string; Url: string
    }
    type Record = {
        Id: string; Kind: string; Title: string; Conclusion: string; Limits: string
        Created: string; Updated: string; Scope: string list; Status: string
        Attribution: string list; Evidence: Evidence list
        Related: string list; Supersedes: string list
    }
    type Store = {
        Records: Record list; TotalBytes: int64; RemainingBytes: int64
        Contributors: (string * int64) list
    }
    type Query = { Text: string; Kind: string option; Status: string option; Scope: string option }
    val maxBytes: int64
    /// Supported JSON Schema; structural constraints additionally enforce links, provenance and dates.
    val schemaJson: string
    /// Parse one record, refusing unsupported fields and duplicate JSON properties.
    val parse: path: string -> bytes: byte array -> Result<Record, Diagnostic list>
    /// Validate all canonical files, identities, links and the inclusive byte budget.
    val load: snapshots: Snapshot list -> Result<Store, Diagnostic list>
    val search: query: Query -> store: Store -> Record list
    val get: id: string -> store: Store -> Record option
    /// Return directly linked incoming/outgoing and superseding records in ID order.
    val related: id: string -> store: Store -> Result<Record list, Diagnostic list>
