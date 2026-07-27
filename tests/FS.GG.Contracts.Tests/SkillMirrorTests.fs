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

    // ----- mirrorFiles: the MULTI-FILE skill (#717) -----
    //
    // `mirror` models a skill as `(id, body)` — one id, one body — so a skill whose canonical form
    // is `SKILL.md` + `references/**` + `agents/*.yaml` could not be materialized through the one
    // implementation at all (ADR-0014 §Decision 2). These cover the shape the org's OWN coordination
    // kit actually has: every kit-owned skill in this repository is 5-7 files.

    /// The real shape, measured on `main`: SKILL.md, an `agents/openai.yaml`, and `references/**`.
    let private kitSkill id =
        { Id = id
          Files =
            [ { RelativePath = "SKILL.md"
                Body = $"# {id}\n" }
              { RelativePath = "agents/openai.yaml"
                Body = $"name: {id}\n" }
              { RelativePath = "references/deep-detail.md"
                Body = $"# {id} deep detail\n" }
              { RelativePath = "references/command-contracts.md"
                Body = $"# {id} command contracts\n" } ] }

    [<Fact>]
    let ``mirrorFiles fans a genuine multi-file skill into every root`` () =
        let plan = mirrorFiles roots [ kitSkill "pnext-item" ]

        Assert.Empty plan.Refused

        // One write per (file x root) — 4 files x 3 roots.
        Assert.Equal(12, List.length plan.Writes)

        // Every auxiliary file lands at the SAME tail under each root, verbatim.
        for root in roots do
            let underRoot =
                plan.Writes
                |> List.filter (fun w -> w.Path.StartsWith(root + "/", System.StringComparison.Ordinal))
                |> List.map (fun w -> w.Path)
                |> List.sort

            Assert.Equal<string list>(
                [ root + "/skills/pnext-item/SKILL.md"
                  root + "/skills/pnext-item/agents/openai.yaml"
                  root + "/skills/pnext-item/references/command-contracts.md"
                  root + "/skills/pnext-item/references/deep-detail.md" ]
                |> List.sort,
                underRoot
            )

        // The body travels with the file, not with the skill.
        let bodyAt path =
            plan.Writes
            |> List.tryPick (fun w -> if w.Path = path then Some w.Body else None)

        Assert.Equal(Some "name: pnext-item\n", bodyAt ".codex/skills/pnext-item/agents/openai.yaml")
        Assert.Equal(Some "# pnext-item\n", bodyAt ".agents/skills/pnext-item/SKILL.md")

    [<Fact>]
    let ``mirrorFiles agrees with mirror on the single-file case`` () =
        // AC2: the existing call shape keeps working byte-for-byte, and the new one is a strict
        // generalisation of it — same paths, same bodies, same order.
        let single = mirror roots [ "s", "body" ]

        let plan =
            mirrorFiles
                roots
                [ { Id = "s"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "body" } ] } ]

        Assert.Empty plan.Refused
        Assert.Equal<MirrorWrite list>(single, plan.Writes)

    [<Fact>]
    let ``mirrorFiles is deterministic: skills by id, files by relative path, roots in order`` () =
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "beta"
                    Files =
                      [ { RelativePath = "references/z.md"
                          Body = "z" }
                        { RelativePath = "SKILL.md"
                          Body = "b" } ] }
                  { Id = "alpha"
                    Files =
                      [ { RelativePath = "references/b.md"
                          Body = "b" }
                        { RelativePath = "SKILL.md"
                          Body = "a" }
                        { RelativePath = "agents/openai.yaml"
                          Body = "y" } ] } ]

        Assert.Empty plan.Refused

        Assert.Equal<string list>(
            [ ".claude/skills/alpha/SKILL.md"
              ".claude/skills/alpha/agents/openai.yaml"
              ".claude/skills/alpha/references/b.md"
              ".claude/skills/beta/SKILL.md"
              ".claude/skills/beta/references/z.md" ],
            plan.Writes |> List.map (fun w -> w.Path)
        )

    // IDEMPOTENCE. `mirrorFiles` is pure, so "a second materialization writes nothing" is two
    // facts: the plan does not depend on how many times it is computed, and applying it to a tree
    // that already holds it changes NO path. Simulate the tree as a path->body map and measure the
    // second pass's CHANGED set, which is what a driver actually acts on.
    [<Fact>]
    let ``mirrorFiles is idempotent: a second materialization changes nothing`` () =
        let skills = [ kitSkill "check-board"; kitSkill "pnext-item" ]

        let apply (tree: Map<string, string>) (writes: MirrorWrite list) =
            let changed =
                writes |> List.filter (fun w -> Map.tryFind w.Path tree <> Some w.Body)

            let tree' =
                writes
                |> List.fold (fun (t: Map<string, string>) w -> Map.add w.Path w.Body t) tree

            changed, tree'

        let first = mirrorFiles roots skills
        let changed1, tree1 = apply Map.empty first.Writes
        Assert.Equal(List.length first.Writes, List.length changed1) // empty tree: everything is new

        let second = mirrorFiles roots skills
        Assert.Equal<MirrorWrite list>(first.Writes, second.Writes) // pure: same plan
        let changed2, tree2 = apply tree1 second.Writes

        Assert.Empty changed2 // the whole point: the second pass writes NOTHING
        Assert.Equal<Map<string, string>>(tree1, tree2)

    // ----- mirrorFiles: refusal semantics (#717 AC4, the #185/#337 lexical-escape class) -----
    //
    // A refusal is a FACT reported next to the writes, never an exception and never a silently
    // dropped file. A refused skill contributes NO writes at all: a plan that materialized a
    // multi-file skill's safe files while dropping its unsafe one would place a HALF skill, which
    // is the silent under-materialization this whole item exists to end.

    let private reasonsFor id (plan: MirrorPlan) =
        plan.Refused
        |> List.tryPick (fun r -> if r.Id = id then Some r.Reasons else None)

    let private pathsUnder id (plan: MirrorPlan) =
        plan.Writes |> List.filter (fun w -> w.Path.Contains("/skills/" + id + "/"))

    [<Fact>]
    let ``mirrorFiles refuses a relative path that escapes the skill directory`` () =
        let plan =
            mirrorFiles
                roots
                [ { Id = "evil"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "ok" }
                        { RelativePath = "../../../etc/passwd"
                          Body = "pwned" } ] } ]

        Assert.Equal(Some [ UnsafeRelativePath "../../../etc/passwd" ], reasonsFor "evil" plan)
        // The SAFE sibling is refused too — no half-materialized skill.
        Assert.Empty(pathsUnder "evil" plan)

    [<Fact>]
    let ``mirrorFiles refuses absolute and rooted relative paths`` () =
        for bad in [ "/etc/passwd"; "\\windows\\system32"; "C:/Windows/win.ini"; "a/../../b" ] do
            let plan =
                mirrorFiles
                    [ ".claude" ]
                    [ { Id = "s"
                        Files =
                          [ { RelativePath = "SKILL.md"
                              Body = "ok" }
                            { RelativePath = bad; Body = "x" } ] } ]

            Assert.NotEmpty plan.Refused
            Assert.Empty plan.Writes

    [<Fact>]
    let ``mirrorFiles refuses an id that escapes the skills directory`` () =
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = ".."
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "x" } ] } ]

        Assert.Equal(Some [ UnsafeSkillId ], reasonsFor ".." plan)
        Assert.Empty plan.Writes

    [<Fact>]
    let ``mirrorFiles refuses a skill with no SKILL_md`` () =
        // `skillPath`/`skillIdOfPath` make `<root>/skills/<id>/SKILL.md` the thing that MAKES a
        // directory a skill. Materializing only auxiliaries would place a directory no discovery
        // pass can see.
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "ghost"
                    Files =
                      [ { RelativePath = "references/a.md"
                          Body = "x" } ] } ]

        Assert.Equal(Some [ MissingSkillFile ], reasonsFor "ghost" plan)
        Assert.Empty plan.Writes

    [<Fact>]
    let ``mirrorFiles refuses a duplicated relative path rather than picking a winner`` () =
        // The multi-file model must not become a way to FLATTEN divergence silently: two entries
        // for one path are two producers, and this library never picks between them.
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "s"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "a" }
                        { RelativePath = "SKILL.md"
                          Body = "B DIFFERENT" } ] } ]

        Assert.Equal(Some [ DuplicateRelativePath "SKILL.md" ], reasonsFor "s" plan)
        Assert.Empty plan.Writes

    [<Fact>]
    let ``mirrorFiles refuses a CASE-ONLY duplicate that a case-insensitive filesystem would flatten`` () =
        // `references/A.md` and `references/a.md` are two files on Linux and ONE on macOS/Windows.
        // Emitting both writes would let the second body silently overwrite the first on a
        // case-insensitive checkout — a flattening this library must never perform.
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "s"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "ok" }
                        { RelativePath = "references/A.md"
                          Body = "one" }
                        { RelativePath = "references/a.md"
                          Body = "TWO" } ] } ]

        Assert.Equal(
            Some
                [ DuplicateRelativePath "references/A.md"
                  DuplicateRelativePath "references/a.md" ],
            reasonsFor "s" plan
        )

        Assert.Empty plan.Writes

    [<Fact>]
    let ``mirrorFiles refuses a duplicated skill id rather than picking a winner`` () =
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "s"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "a" } ] }
                  { Id = "s"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "B DIFFERENT" } ] } ]

        Assert.Equal(Some [ DuplicateSkillId ], reasonsFor "s" plan)
        Assert.Empty plan.Writes

    [<Fact>]
    let ``mirrorFiles reports refusal reasons as INDEPENDENT facts, not one verdict`` () =
        // Same shape as `verify`'s missingRoots / divergent / hashMismatchRoots (the reason
        // FS-GG/.github#1506 was findable): one subject can carry several distinct causes, and
        // every one of them is reported.
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "s"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "a" }
                        { RelativePath = "../escape.md"
                          Body = "x" }
                        { RelativePath = "references/a.md"
                          Body = "1" }
                        { RelativePath = "references/a.md"
                          Body = "2" } ] } ]

        let reasons = reasonsFor "s" plan |> Option.defaultValue []
        Assert.Contains(UnsafeRelativePath "../escape.md", reasons)
        Assert.Contains(DuplicateRelativePath "references/a.md", reasons)
        Assert.Equal(2, List.length reasons)

    [<Fact>]
    let ``mirrorFiles keeps a refused skill from contaminating a clean one`` () =
        // A refusal is scoped to its skill. The clean skill in the same batch is still planned —
        // otherwise one bad input silently stops the whole fan-out.
        let plan =
            mirrorFiles
                [ ".claude" ]
                [ { Id = "good"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "ok" } ] }
                  { Id = "bad"
                    Files =
                      [ { RelativePath = "SKILL.md"
                          Body = "ok" }
                        { RelativePath = "../x"; Body = "no" } ] } ]

        Assert.Equal<string list>([ ".claude/skills/good/SKILL.md" ], plan.Writes |> List.map (fun w -> w.Path))
        Assert.Equal<string list>([ "bad" ], plan.Refused |> List.map (fun r -> r.Id))

    [<Fact>]
    let ``skillFilePath places a file inside the skill directory and agrees with skillPath`` () =
        Assert.Equal(
            ".claude/skills/pnext-item/references/deep-detail.md",
            skillFilePath ".claude" "pnext-item" "references/deep-detail.md"
        )

        Assert.Equal(skillPath ".codex" "s", skillFilePath ".codex" "s" "SKILL.md")

    [<Fact>]
    let ``retargetSkillPath carries an AUXILIARY tail verbatim into a target root`` () =
        // AC3: `providerSourceRoot` confinement is unchanged for auxiliary paths — the tail after
        // `<providerSourceRoot>/skills/` moves across verbatim, nested directories and all.
        Assert.Equal(
            ".claude/skills/pnext-item/references/deep-detail.md",
            retargetSkillPath ".claude" ".agents/skills/pnext-item/references/deep-detail.md"
        )

        Assert.Equal(
            ".codex/skills/pnext-item/agents/openai.yaml",
            retargetSkillPath ".codex" ".agents/skills/pnext-item/agents/openai.yaml"
        )

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
