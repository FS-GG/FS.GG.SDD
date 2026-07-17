namespace Fsgg

/// Shared `major.minor.patch` version grammar and total order for the whole repo
/// (feature 052 D3/E3). One grammar exists: the (formerly private) Registry SemVer
/// engine is refactored to delegate here, so scaffold coherence and Registry range
/// checks read versions the same way.
module Version =

    /// A parsed major.minor.patch version. Same grammar as the (formerly private)
    /// Registry SemVer engine, which is refactored to delegate here.
    type Version = { Major: int; Minor: int; Patch: int }

    /// Parse "major.minor.patch"; None when the text is not a valid triple.
    val tryParse: text: string -> Version option

    /// Total order over the string forms. Some -1 / Some 0 / Some 1 when BOTH parse;
    /// None when EITHER side is unparseable (callers degrade honestly — never assert a
    /// false ordering).
    val compare: left: string -> right: string -> int option
