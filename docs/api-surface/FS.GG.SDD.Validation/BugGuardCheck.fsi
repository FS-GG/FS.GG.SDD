namespace FS.GG.SDD.Validation

open FS.GG.SDD.Artifacts.Diagnostics

/// FS.GG.SDD#654 — a preventive lane check for the gap where an OPEN filed bug can
/// co-exist indefinitely with a GREEN test that pins its reported-wrong behavior.
/// TowerDefense1#14 ("maze ground enemies follow the straight line, not the flow field")
/// sat open for ~7 milestones while an M2 test asserted the buggy output and stayed green;
/// fixing #14 turned that assertion red only by accident of touching the same code.
///
/// Rather than fuzzy prose matching (research: intractable, false-positive prone), the
/// check keys off a STRUCTURED link the test author writes in the test source: a
/// `pins-bug #<n>` or `guards #<n>` marker. The rule warns for every marker whose
/// referenced issue is still OPEN — a green test asserting a not-yet-fixed bug's
/// behavior — and stays silent once that issue closes.
///
/// The rule is a pure classifier over an INJECTED issue-state resolver: it performs no
/// I/O. Gathering the marker corpus (a source scan) and resolving live issue state (a
/// GitHub query, owned by the CI edge that has issue access) are the caller's job; that
/// deliberate seam keeps this module deterministic and testable, and leaves the
/// "who queries GitHub — SDD vs the test-repo's CI" edge decision to the edge that wires
/// `resolve`.
module BugGuardCheck =

    /// Which structured link a test declares.
    type MarkerKind =
        /// The test asserts the *reported-wrong* behavior of issue #n. While #n is OPEN
        /// this is the TD1#14 hazard: a green test locking in an unfixed bug.
        | PinsBug
        /// The test guards issue #n's *fixed* behavior against regression. It only makes
        /// sense once #n is closed; while #n is OPEN the guarded fix has not landed.
        | Guards

    /// One structured test→issue link found in a test source file.
    type BugGuardMarker =
        {
            Kind: MarkerKind
            Issue: int
            /// Source file the marker was found in (as supplied to the scanner).
            Path: string
            /// 1-based line number of the marker within that file.
            Line: int
        }

    /// Resolved open/closed state of a referenced issue, populated by the edge that has
    /// issue access. The pure rule never performs I/O.
    type IssueState =
        | Open
        | Closed
        /// The issue number could not be resolved (e.g. it does not exist) — a dangling
        /// structured link, itself a defect.
        | Unknown

    /// Serialized token for a marker kind (`"pins-bug"` / `"guards"`).
    val markerKindValue: kind: MarkerKind -> string

    /// Marker grammar: the token `pins-bug` or `guards`, optional whitespace, `#`,
    /// optional whitespace, then the issue number. Case-insensitive and matched anywhere
    /// on a line (authors write it in a comment), so it is host-language agnostic across
    /// F#/C#/`//`/`(* *)` test sources. Returns one marker per match in source order
    /// (top-to-bottom, left-to-right); a line may carry several.
    val scanText: path: string -> text: string -> BugGuardMarker list

    /// The pure lane rule. For each marker, resolve its issue and emit a `DiagnosticWarning`
    /// when the issue is still OPEN (a green test pinning/guarding an unfixed bug) or when it
    /// is UNKNOWN (a dangling link). CLOSED yields nothing — silent once the bug is fixed.
    /// Deterministic: diagnostics are ordered by (path, line, issue, kind).
    val check: resolve: (int -> IssueState) -> markers: BugGuardMarker list -> Diagnostic list

    /// Convenience edge for a whole corpus: scan each `(path, text)` test source, then run
    /// `check`. The caller supplies the file corpus and the `resolve` seam.
    val checkSources: resolve: (int -> IssueState) -> sources: (string * string) list -> Diagnostic list
