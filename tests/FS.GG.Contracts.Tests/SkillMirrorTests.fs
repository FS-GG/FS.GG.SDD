namespace FS.GG.Contracts.Tests

open Fsgg
open Fsgg.Schemas
open Fsgg.SkillMirror
open Xunit

/// Feature 058 / ADR-0014 P1: the one materialize-and-verify library. Pure unit tests over
/// `mirror`/`verify` and the content helpers — the algorithm every SDD lane routes through.
module SkillMirrorTests =

    let private roots = agentSkillRoots // [ ".claude"; ".codex"; ".agents" ]

    // ----- helpers -----

    [<Fact>]
    let ``skillPath is <root>/skills/<id>/SKILL_md`` () =
        Assert.Equal(".claude/skills/fs-gg-elmish/SKILL.md", skillPath ".claude" "fs-gg-elmish")

    [<Fact>]
    let ``skillIdOfPath extracts the id from any root`` () =
        Assert.Equal(Some "fs-gg-elmish", skillIdOfPath ".agents/skills/fs-gg-elmish/SKILL.md")
        Assert.Equal(Some "fs-gg-sdd-plan", skillIdOfPath ".codex/skills/fs-gg-sdd-plan/SKILL.md")

    [<Fact>]
    let ``skillIdOfPath rejects non-skill paths`` () =
        Assert.Equal(None, skillIdOfPath "src/Product/Program.fs")
        Assert.Equal(None, skillIdOfPath ".fsgg/early-stage-guidance.md")
        Assert.Equal(None, skillIdOfPath ".claude/skills/fs-gg-elmish/OTHER.md")

    [<Fact>]
    let ``mirrorTargetRoots drops the provider source root`` () =
        Assert.Equal<string list>([ ".claude"; ".codex" ], mirrorTargetRoots roots)
        Assert.Equal(".agents", providerSourceRoot)

    [<Fact>]
    let ``retargetSkillPath rewrites the tail verbatim into a target root`` () =
        Assert.Equal(
            ".claude/skills/fs-gg-elmish/SKILL.md",
            retargetSkillPath ".claude" ".agents/skills/fs-gg-elmish/SKILL.md"
        )

    [<Fact>]
    let ``sha256 is stable lowercase hex over utf-8 bytes`` () =
        let digest = sha256 "hello\n"
        Assert.Equal(64, digest.Length)
        Assert.Equal(digest, sha256 "hello\n")
        Assert.NotEqual<string>(digest, sha256 "hello")

    // Feature 060 / #70: content-identical bodies must hash the same regardless of line
    // endings, so a CRLF checkout does not spuriously flag skill drift. Agrees with
    // FS.GG.SDD.Artifacts SchemaVersion.sha256Text, which normalizes CRLF->LF the same way.
    [<Fact>]
    let ``sha256 is line-ending insensitive (CRLF equals LF)`` () =
        Assert.Equal(sha256 "a\nb\nc\n", sha256 "a\r\nb\r\nc\r\n")
        Assert.Equal(sha256 "# Title\n\nBody line\n", sha256 "# Title\r\n\r\nBody line\r\n")

    // ----- mirror -----

    [<Fact>]
    let ``mirror yields one write per root at the canonical path`` () =
        let writes = mirror roots [ "s", "body" ]

        Assert.Equal<string list>(
            [ ".claude/skills/s/SKILL.md"
              ".codex/skills/s/SKILL.md"
              ".agents/skills/s/SKILL.md" ],
            writes |> List.map (fun w -> w.Path)
        )

        Assert.True(writes |> List.forall (fun w -> w.Body = "body"))

    [<Fact>]
    let ``mirror sorts skills by id for a deterministic effect order`` () =
        let paths =
            mirror [ ".claude" ] [ "beta", "b"; "alpha", "a" ] |> List.map (fun w -> w.Path)

        Assert.Equal<string list>([ ".claude/skills/alpha/SKILL.md"; ".claude/skills/beta/SKILL.md" ], paths)

    // ----- verify -----

    let private expected id sha =
        { Id = id
          Scope = Process
          Sha256 = sha }

    let private copy root id body : ActualCopy = { Root = root; Id = id; Body = body }

    let private allPresent id body =
        roots |> List.map (fun r -> copy r id (Some body))

    [<Fact>]
    let ``verify returns no drift when every copy is present, identical, and matches the hash`` () =
        let body = "canonical\n"
        let drift = verify roots [ expected "s" (sha256 body) ] (allPresent "s" body)
        Assert.Empty drift

    [<Fact>]
    let ``verify detects a copy missing from one root (skill loss)`` () =
        let body = "canonical\n"

        let actual =
            [ copy ".claude" "s" (Some body)
              copy ".codex" "s" (Some body)
              copy ".agents" "s" None ]

        let drift = verify roots [ expected "s" (sha256 body) ] actual
        let d = List.exactlyOne drift
        Assert.Equal<string list>([ ".agents" ], d.MissingRoots)

    [<Fact>]
    let ``verify detects a byte-divergent copy across roots`` () =
        let body = "canonical\n"

        let actual =
            [ copy ".claude" "s" (Some body)
              copy ".codex" "s" (Some "EDITED\n")
              copy ".agents" "s" (Some body) ]

        // No reference digest ⇒ hash-match skipped, but cross-root divergence is still caught.
        let drift = verify roots [ expected "s" "" ] actual
        let d = List.exactlyOne drift
        Assert.True d.Divergent

    [<Fact>]
    let ``verify detects a copy whose hash does not match the manifest`` () =
        let body = "canonical\n"

        let actual =
            [ copy ".claude" "s" (Some body)
              copy ".codex" "s" (Some body)
              copy ".agents" "s" (Some "TAMPERED\n") ]

        let drift = verify roots [ expected "s" (sha256 body) ] actual
        let d = List.exactlyOne drift
        Assert.Contains(".agents", d.HashMismatchRoots)
        Assert.True d.Divergent // the tampered copy also breaks cross-root identity

    [<Fact>]
    let ``verify returns drifted skills sorted by id`` () =
        let actual = [ copy ".claude" "z" None; copy ".claude" "a" None ]

        let drift =
            verify [ ".claude" ] [ expected "z" ""; expected "a" "" ] actual
            |> List.map (fun d -> d.Id)

        Assert.Equal<string list>([ "a"; "z" ], drift)
