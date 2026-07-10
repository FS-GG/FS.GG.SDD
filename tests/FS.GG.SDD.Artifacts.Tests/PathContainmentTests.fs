namespace FS.GG.SDD.Artifacts.Tests

open FS.GG.SDD.Artifacts
open Xunit

// FS-GG/FS.GG.SDD#337: the direct lock on the single authoritative containment predicate. Before
// this, `escapesRoot` was copy-pasted across four sites (three CLI, one Foundation) and kept coherent
// only by prose comments — a fix to one copy could silently leave the others exploitable. Now there is
// one implementation, and this exercises it directly so it cannot regress silently (ADR-0002 inv. 4).
module PathContainmentTests =
    let escapes = PathContainment.escapesRoot

    [<Theory>]
    [<InlineData("/etc/passwd")>] // absolute (POSIX root)
    [<InlineData("/")>]
    [<InlineData("../secret")>] // parent-dir escape
    [<InlineData("a/../../b")>] // `..` segment mid-path
    [<InlineData("..")>]
    [<InlineData("..\\secret")>] // backslash-normalized `..` segment
    [<InlineData("")>] // empty
    [<InlineData("   ")>] // whitespace-only
    let ``escapesRoot rejects paths that escape the workspace root`` (raw: string) = Assert.True(escapes raw)

    [<Theory>]
    [<InlineData("docs/api-surface/Foo.fsi")>]
    [<InlineData("readiness/337/verify.json")>]
    [<InlineData("a")>]
    [<InlineData("a/b/c")>]
    [<InlineData("..foo/bar")>] // `..` as a filename prefix, not a segment — stays contained
    [<InlineData("foo..bar")>]
    let ``escapesRoot admits repository-relative paths`` (raw: string) = Assert.False(escapes raw)

    [<Fact>]
    let ``escapesRoot runs on the RAW value, not a normalized one`` () =
        // The load-bearing property: the check must see the leading slash BEFORE any TrimStart('/').
        // A normalize-then-test predicate would strip it and let "/etc/passwd" through as "etc/passwd".
        Assert.True(escapes "/etc/passwd")

    [<Fact>]
    let ``escapesRoot is a LEXICAL guard, not a filesystem-containment proof (per #203)`` () =
        // A path that NAMES a location inside the root passes the lexical guard even though a symlink at
        // that name could resolve outside — resolving the effect edge is FS-GG/FS.GG.SDD#203 (ADR-0002),
        // not this predicate. This documents the boundary so a future reader does not over-trust it.
        Assert.False(escapes "some-symlink/inside/target")
