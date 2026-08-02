namespace FS.GG.SDD.Artifacts

/// Parses a runner-produced test report into an `ObservedRun` receipt (FS.GG.SDD#350, ADR-0035).
///
/// **SDD never runs a test.** ADR-0035 rejected shelling out to `dotnet test` outright: it would put
/// toolchain knowledge inside a generic lifecycle tool that must also serve Rust, TypeScript, and
/// Godot workspaces, and making it configurable would collapse it back into "read an artifact
/// somebody else produced". So SDD reads the artifact somebody else produced.
///
/// The two formats are TRX and JUnit XML — chosen because the org's runners already emit them, and a
/// receipt format nobody produces is a receipt nobody records.
///
/// **Pure.** The parse is a total fold over the report's *text*: the read is a `ReadFile` effect
/// interpreted at the `CommandEffects` edge, and `Artifacts` performs no I/O (Constitution V). It is
/// therefore unit-testable without a filesystem, and it cannot raise — every malformed input,
/// including non-XML and an unrecognised root, returns `Error`.
[<RequireQualifiedAccess>]
module TestReport =

    /// Parse `text` — the bytes of the report at `source` — into a receipt.
    ///
    /// `Outcome` is **derived from the parsed counts** (`failed = failures + errors`; `passed` iff
    /// `failed = 0`), never copied from the report's own summary attribute, which runners disagree
    /// about. A recorded receipt therefore cannot be self-inconsistent by construction — which is
    /// what leaves `Evidence.observedRunInconsistency` policing only *authored* receipts.
    ///
    /// `Digest` is `sha256:<hex>` over the exact supplied report bytes. Decoding is used only to
    /// parse XML; it never changes the bytes being attested.
    val parse: source: string -> text: string -> Result<ObservedRun, string>

    /// Parse decoded report text while attesting the exact bytes read at the effect edge. This is
    /// the command-path entry point; `parse` remains for text-only callers and hashes UTF-8 text.
    val parseBytes: source: string -> bytes: byte array -> text: string -> Result<ObservedRun, string>
