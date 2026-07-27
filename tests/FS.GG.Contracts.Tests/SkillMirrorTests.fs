namespace FS.GG.Contracts.Tests

open System.IO
open System.Text
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

    // ----- verifyFiles (FS.GG.SDD#721) -----
    //
    // The verify half of `mirrorFiles`. #717 gave the library a multi-file MATERIALIZER while
    // `verify` still modelled a skill as one body, so the library wrote `references/**` it could
    // not check and `scripts/materialize-skill-roots.fsx` hand-rolled the cross-root comparison.
    // These tests pin the three facts staying INDEPENDENT and drift naming the offending FILE.

    let private file rel body : SkillFile = { RelativePath = rel; Body = body }

    let private copyFiles root id files : ActualSkillFiles = { Root = root; Id = id; Files = files }

    /// A coherent 3-file skill in every root: `SKILL.md` + two auxiliaries.
    let private multiFile body =
        [ file "SKILL.md" body
          file "references/deep-detail.md" "detail\n"
          file "agents/openai.yaml" "name: s\n" ]

    [<Fact>]
    let ``verifyFiles returns no drift for a fully coherent multi-file skill`` () =
        let body = "canonical\n"

        let actual = roots |> List.map (fun r -> copyFiles r "s" (Some(multiFile body)))

        Assert.Empty(verifyFiles roots [ expected "s" (sha256 body) ] actual)

    // AC6.1: the defect the item names outright — a `references/deep-detail.md` that differs
    // between `.claude` and `.codex` was INVISIBLE to `SkillMirror.verify`, which only ever saw
    // SKILL.md. Drift must name the FILE, not merely the skill.
    [<Fact>]
    let ``verifyFiles reports a divergent AUXILIARY file and names it`` () =
        let body = "canonical\n"

        let actual =
            [ copyFiles ".claude" "s" (Some(multiFile body))
              copyFiles
                  ".codex"
                  "s"
                  (Some
                      [ file "SKILL.md" body
                        file "references/deep-detail.md" "EDITED\n"
                        file "agents/openai.yaml" "name: s\n" ])
              copyFiles ".agents" "s" (Some(multiFile body)) ]

        let d = List.exactlyOne (verifyFiles roots [ expected "s" (sha256 body) ] actual)
        Assert.Empty d.MissingRoots
        let f = List.exactlyOne d.Files
        Assert.Equal("references/deep-detail.md", f.RelativePath)
        Assert.True f.Divergent
        // INDEPENDENT: divergence did not manufacture a missing root or a hash mismatch.
        Assert.Empty f.MissingRoots
        Assert.Empty f.HashMismatchRoots

    // AC6.2: a MISSING auxiliary is a distinct fact from a DIVERGENT one. Both are per-file, both
    // are reported, and neither is collapsed into the other or into a single verdict.
    [<Fact>]
    let ``verifyFiles reports a missing auxiliary as a fact distinct from divergence`` () =
        let body = "canonical\n"

        let actual =
            [ copyFiles ".claude" "s" (Some(multiFile body))
              // `.codex` lost the reference entirely AND disagrees on the agent card.
              copyFiles ".codex" "s" (Some [ file "SKILL.md" body; file "agents/openai.yaml" "name: OTHER\n" ])
              copyFiles ".agents" "s" (Some(multiFile body)) ]

        let d = List.exactlyOne (verifyFiles roots [ expected "s" (sha256 body) ] actual)
        // The skill is present in every root, so the SKILL-level missing fact stays empty.
        Assert.Empty d.MissingRoots

        let agents = d.Files |> List.find (fun f -> f.RelativePath = "agents/openai.yaml")

        let reference =
            d.Files |> List.find (fun f -> f.RelativePath = "references/deep-detail.md")

        // Missing, and NOT divergent — the two present copies agree with each other.
        Assert.Equal<string list>([ ".codex" ], reference.MissingRoots)
        Assert.False reference.Divergent

        // Divergent, and NOT missing.
        Assert.True agents.Divergent
        Assert.Empty agents.MissingRoots

        // Files are named in a stable, sorted order.
        Assert.Equal<string list>(
            [ "agents/openai.yaml"; "references/deep-detail.md" ],
            d.Files |> List.map (fun f -> f.RelativePath)
        )

    // A root missing the skill ENTIRELY is the skill-level fact, not N per-file absences —
    // otherwise "the whole copy is gone" is buried under its own detail.
    [<Fact>]
    let ``verifyFiles keeps a whole-copy absence at the SKILL level`` () =
        let body = "canonical\n"

        let actual =
            [ copyFiles ".claude" "s" (Some(multiFile body))
              copyFiles ".codex" "s" (Some(multiFile body))
              copyFiles ".agents" "s" None ]

        let d = List.exactlyOne (verifyFiles roots [ expected "s" (sha256 body) ] actual)
        Assert.Equal<string list>([ ".agents" ], d.MissingRoots)
        // The two roots that DO carry it agree on every file, so no file is named.
        Assert.Empty d.Files

    // The file comparison is a UNION over present roots, not the first root's set: a file only the
    // SECOND root carries is drift exactly as one only the first carries. An asymmetric compare
    // would call this coherent.
    [<Fact>]
    let ``verifyFiles is symmetric — an EXTRA file in one root is drift`` () =
        let body = "canonical\n"

        let actual =
            [ copyFiles ".claude" "s" (Some [ file "SKILL.md" body ])
              copyFiles ".codex" "s" (Some [ file "SKILL.md" body; file "references/stale.md" "left over\n" ])
              copyFiles ".agents" "s" (Some [ file "SKILL.md" body ]) ]

        let d = List.exactlyOne (verifyFiles roots [ expected "s" (sha256 body) ] actual)
        let f = List.exactlyOne d.Files
        Assert.Equal("references/stale.md", f.RelativePath)
        Assert.Equal<string list>([ ".claude"; ".agents" ], f.MissingRoots)

    // `ExpectedSkill.Sha256` content-addresses the SKILL.md body (ADR-0017), so hash-match is
    // asserted there and is empty for auxiliaries BY CONSTRUCTION.
    [<Fact>]
    let ``verifyFiles hash-matches SKILL_md against the manifest digest`` () =
        let body = "canonical\n"

        let actual =
            [ copyFiles ".claude" "s" (Some(multiFile body))
              copyFiles ".codex" "s" (Some(multiFile body))
              copyFiles
                  ".agents"
                  "s"
                  (Some
                      [ file "SKILL.md" "TAMPERED\n"
                        file "references/deep-detail.md" "detail\n"
                        file "agents/openai.yaml" "name: s\n" ]) ]

        let d = List.exactlyOne (verifyFiles roots [ expected "s" (sha256 body) ] actual)
        let f = List.exactlyOne d.Files
        Assert.Equal("SKILL.md", f.RelativePath)
        Assert.Contains(".agents", f.HashMismatchRoots)
        Assert.True f.Divergent // the tampered copy also breaks cross-root identity

    [<Fact>]
    let ``verifyFiles skips hash-match when no reference digest is declared`` () =
        let actual =
            roots
            |> List.map (fun r -> copyFiles r "s" (Some [ file "SKILL.md" "whatever\n" ]))

        Assert.Empty(verifyFiles roots [ expected "s" "" ] actual)

    [<Fact>]
    let ``verifyFiles returns drifted skills sorted by id`` () =
        let actual = [ copyFiles ".claude" "z" None; copyFiles ".claude" "a" None ]

        Assert.Equal<string list>(
            [ "a"; "z" ],
            verifyFiles [ ".claude" ] [ expected "z" ""; expected "a" "" ] actual
            |> List.map (fun d -> d.Id)
        )

    // ADDITIVE, and provably so: fed a skill whose file set is exactly `SKILL.md`, `verifyFiles`
    // reports precisely what `verify` reports. `verify` is not reimplemented in terms of it (that
    // would change the shipped signature); this is the equivalence that lets both coexist as ONE
    // algorithm rather than two (ADR-0014 §Decision 2).
    [<Fact>]
    let ``verifyFiles agrees with verify on every single-file case`` () =
        let body = "canonical\n"

        let cases =
            [ "coherent", [ Some body; Some body; Some body ], sha256 body
              "missing", [ Some body; Some body; None ], sha256 body
              "divergent", [ Some body; Some "EDITED\n"; Some body ], ""
              "hash", [ Some body; Some body; Some "TAMPERED\n" ], sha256 body
              "all-absent", [ None; None; None ], "" ]

        for (id, bodies, sha) in cases do
            let pairs = List.zip roots bodies
            let exp = [ expected id sha ]

            let old = verify roots exp (pairs |> List.map (fun (r, b) -> copy r id b))

            let neu =
                verifyFiles
                    roots
                    exp
                    (pairs
                     |> List.map (fun (r, b) -> copyFiles r id (b |> Option.map (fun x -> [ file "SKILL.md" x ]))))

            Assert.Equal(List.length old, List.length neu)

            for (o, n) in List.zip old neu do
                Assert.Equal<string list>(o.MissingRoots, n.MissingRoots)
                let skillMd = n.Files |> List.tryFind (fun f -> f.RelativePath = "SKILL.md")
                Assert.Equal(o.Divergent, skillMd |> Option.map (fun f -> f.Divergent) |> Option.defaultValue false)

                Assert.Equal<string list>(
                    o.HashMismatchRoots,
                    skillMd |> Option.map (fun f -> f.HashMismatchRoots) |> Option.defaultValue []
                )

    // ----- verifyFileSet (FS.GG.SDD#727) -----
    //
    // #721 gave `verifyFiles` three independent facts over a skill's whole file set, but its THIRD
    // fact could only ever address `SKILL.md`, because that is all the ADR-0017 v1 manifest
    // declared. So `HashMismatchRoots = []` on an auxiliary meant "no digest was available" while
    // reading as "hash checked, clean". These tests pin the widened fact — and, deliberately, pin
    // it BY CONTRAST: the headline case asserts what `verifyFiles` reports on the identical input,
    // so the strengthening is stated as a difference rather than asserted in prose.

    let private declared rel sha : SkillManifestFile = { RelativePath = rel; Sha256 = sha }

    let private expectedFiles id files : ExpectedSkillFiles =
        { Id = id
          Scope = Process
          Files = files }

    /// The declaration matching `multiFile body` — every file of the 3-file skill, digested.
    let private declaredMultiFile body =
        [ declared "SKILL.md" (sha256 body)
          declared "references/deep-detail.md" (sha256 "detail\n")
          declared "agents/openai.yaml" (sha256 "name: s\n") ]

    // THE DEFECT THE ITEM NAMES, AND THE ONE THE OLD SURFACE CANNOT SEE.
    //
    // Every root carries the SAME tampered auxiliary. Cross-root identity is therefore CLEAN — the
    // three copies agree with each other perfectly — and `verifyFiles` has no digest for that file,
    // so it reports NOTHING AT ALL. That is the gap in one sentence: consistency is not
    // authenticity, and three roots materialized from one tampered producer copy are byte-identical.
    //
    // The assertion on `verifyFiles` is not decoration. It is the proof that this test fails
    // without the change: run the same input through the pre-#727 surface and it is empty.
    [<Fact>]
    let ``verifyFileSet catches a tampered AUXILIARY that every root agrees on`` () =
        let body = "canonical\n"

        let tampered =
            [ file "SKILL.md" body
              file "references/deep-detail.md" "TAMPERED\n"
              file "agents/openai.yaml" "name: s\n" ]

        let actual = roots |> List.map (fun r -> copyFiles r "s" (Some tampered))

        // The old surface: silent. No digest for the auxiliary, and the roots agree.
        Assert.Empty(verifyFiles roots [ expected "s" (sha256 body) ] actual)

        // The widened one: named, with every root that carries the unauthorised bytes.
        let d =
            List.exactlyOne (verifyFileSet roots [ expectedFiles "s" (declaredMultiFile body) ] actual)

        Assert.Empty d.MissingRoots
        let f = List.exactlyOne d.Files
        Assert.Equal("references/deep-detail.md", f.RelativePath)
        Assert.Equal<string list>(roots, f.HashMismatchRoots)
        // INDEPENDENT: a hash mismatch did not manufacture a missing root, a divergence, or an
        // undeclared-file finding. The roots really do agree with each other — they agree on the
        // WRONG bytes, for a file the manifest DOES declare.
        Assert.Empty f.MissingRoots
        Assert.False f.Divergent
        Assert.Empty f.UndeclaredRoots

    // A DECLARED file that NO root carries. `verifyFiles` compares the OBSERVED union, so a file
    // deleted from every root leaves nothing to compare and the skill reads coherent — the shape
    // that let a skill deleted from all three roots pass in FS.GG.SDD#726. The declaration is an
    // authority, so its absence is drift.
    [<Fact>]
    let ``verifyFileSet reports a declared file missing from EVERY root`` () =
        let body = "canonical\n"

        let actual =
            roots |> List.map (fun r -> copyFiles r "s" (Some [ file "SKILL.md" body ]))

        // The old surface: silent — there is nothing in the observed union to disagree about.
        Assert.Empty(verifyFiles roots [ expected "s" (sha256 body) ] actual)

        let d =
            List.exactlyOne (verifyFileSet roots [ expectedFiles "s" (declaredMultiFile body) ] actual)

        Assert.Empty d.MissingRoots // the skill itself is present everywhere

        Assert.Equal<string list>(
            [ "agents/openai.yaml"; "references/deep-detail.md" ],
            d.Files |> List.map (fun f -> f.RelativePath)
        )

        for f in d.Files do
            Assert.Equal<string list>(roots, f.MissingRoots)
            Assert.False f.Divergent
            Assert.Empty f.HashMismatchRoots
            Assert.Empty f.UndeclaredRoots

    // An UNDECLARED file that every root carries. At v2 the declared set is COMPLETE, so no
    // declared digest authorises these bytes. Reporting nothing would rebuild the exact misreading
    // this item closed, one level up: a file that cannot be verified must not read as coherent.
    [<Fact>]
    let ``verifyFileSet reports an UNDECLARED file rather than passing it over`` () =
        let body = "canonical\n"

        let actual =
            roots
            |> List.map (fun r -> copyFiles r "s" (Some [ file "SKILL.md" body; file "references/extra.md" "new\n" ]))

        let d =
            List.exactlyOne (verifyFileSet roots [ expectedFiles "s" [ declared "SKILL.md" (sha256 body) ] ] actual)

        let f = List.exactlyOne d.Files
        Assert.Equal("references/extra.md", f.RelativePath)
        Assert.Equal<string list>(roots, f.UndeclaredRoots)
        // INDEPENDENT, and this is the point of the separate field: "no declaration covers this
        // file" is NOT "its bytes contradict a declared digest". Reporting it through
        // `HashMismatchRoots` would make one field mean two things — the very ambiguity this
        // amendment removes, pointing the other way.
        Assert.Empty f.HashMismatchRoots
        Assert.Empty f.MissingRoots
        Assert.False f.Divergent

    // An EMPTY declaration is "no authority held", not "nothing is expected" — the exact analogue
    // of `ExpectedSkill.Sha256 = ""`. A co-tenant skill whose manifest lives in another producer's
    // repo must not have an expectation invented for it, and must not be flooded with per-file
    // mismatches for files nobody here declared.
    [<Fact>]
    let ``verifyFileSet with an EMPTY declaration skips hash-match entirely`` () =
        let body = "canonical\n"

        let actual = roots |> List.map (fun r -> copyFiles r "s" (Some(multiFile body)))

        Assert.Empty(verifyFileSet roots [ expectedFiles "s" [] ] actual)

    // …and still asserts the other two facts. An empty declaration weakens fact 3 ALONE.
    [<Fact>]
    let ``verifyFileSet with an EMPTY declaration still reports divergence and absence`` () =
        let body = "canonical\n"

        let actual =
            [ copyFiles ".claude" "s" (Some(multiFile body))
              copyFiles
                  ".codex"
                  "s"
                  (Some
                      [ file "SKILL.md" body
                        file "references/deep-detail.md" "EDITED\n"
                        file "agents/openai.yaml" "name: s\n" ])
              copyFiles ".agents" "s" None ]

        let d = List.exactlyOne (verifyFileSet roots [ expectedFiles "s" [] ] actual)
        Assert.Equal<string list>([ ".agents" ], d.MissingRoots)
        let f = List.exactlyOne d.Files
        Assert.Equal("references/deep-detail.md", f.RelativePath)
        Assert.True f.Divergent

    // A declared file that IS present and DOES match is silent — the check can pass, which is the
    // other half of "a check that cannot fail is not a check".
    [<Fact>]
    let ``verifyFileSet is clean when every declared file matches`` () =
        let body = "canonical\n"

        let actual = roots |> List.map (fun r -> copyFiles r "s" (Some(multiFile body)))

        Assert.Empty(verifyFileSet roots [ expectedFiles "s" (declaredMultiFile body) ] actual)

    // ADDITIVE, and provably so — the same equivalence #721 pinned for `verifyFiles` against
    // `verify`, one level up. Fed a single-file skill, `verifyFileSet` with a one-row declaration
    // reports precisely what `verifyFiles` reports for the same digest, and with an empty
    // declaration precisely what it reports for `Sha256 = ""`. Neither shipped function is
    // reimplemented in terms of the other; this is what lets all three coexist as ONE algorithm
    // (ADR-0014 §Decision 2).
    [<Fact>]
    let ``verifyFileSet agrees with verifyFiles on every single-file case`` () =
        let body = "canonical\n"

        let cases =
            [ "coherent", [ Some body; Some body; Some body ], sha256 body
              "missing", [ Some body; Some body; None ], sha256 body
              "divergent", [ Some body; Some "EDITED\n"; Some body ], ""
              "hash", [ Some body; Some body; Some "TAMPERED\n" ], sha256 body
              "all-absent", [ None; None; None ], "" ]

        for (id, bodies, sha) in cases do
            let observed =
                List.zip roots bodies
                |> List.map (fun (r, b) -> copyFiles r id (b |> Option.map (fun x -> [ file "SKILL.md" x ])))

            let old = verifyFiles roots [ expected id sha ] observed

            let declaration = if sha = "" then [] else [ declared "SKILL.md" sha ]

            let neu = verifyFileSet roots [ expectedFiles id declaration ] observed

            Assert.Equal(List.length old, List.length neu)

            for (o, n) in List.zip old neu do
                Assert.Equal<string list>(o.MissingRoots, n.MissingRoots)
                Assert.Equal(List.length o.Files, List.length n.Files)

                for (fo, fn) in List.zip o.Files n.Files do
                    Assert.Equal(fo.RelativePath, fn.RelativePath)
                    Assert.Equal(fo.Divergent, fn.Divergent)
                    Assert.Equal<string list>(fo.HashMismatchRoots, fn.HashMismatchRoots)
                    Assert.Equal<string list>(fo.MissingRoots, fn.MissingRoots)
                    // The fourth fact is silent on a single-file skill whose one file IS declared,
                    // which is what makes the equivalence an equivalence and not a coincidence.
                    Assert.Empty fn.UndeclaredRoots

    [<Fact>]
    let ``verifyFileSet returns drifted skills sorted by id`` () =
        let actual = [ copyFiles ".claude" "z" None; copyFiles ".claude" "a" None ]

        Assert.Equal<string list>(
            [ "a"; "z" ],
            verifyFileSet [ ".claude" ] [ expectedFiles "z" []; expectedFiles "a" [] ] actual
            |> List.map (fun d -> d.Id)
        )

    // Path spelling is normalized on BOTH sides, so a declaration written `references\deep.md`
    // and an observation read `references/deep.md` are the same file. Without this a Windows-
    // authored manifest would report every auxiliary as both missing and undeclared at once.
    [<Fact>]
    let ``verifyFileSet normalizes declared and observed relative paths alike`` () =
        let body = "canonical\n"

        let actual =
            roots
            |> List.map (fun r -> copyFiles r "s" (Some [ file "SKILL.md" body; file "references/deep.md" "d\n" ]))

        let declaration =
            [ declared "SKILL.md" (sha256 body)
              declared "references\\deep.md" (sha256 "d\n") ]

        Assert.Empty(verifyFileSet roots [ expectedFiles "s" declaration ] actual)

    // ---------------------------------------------------------------------------------------
    // THE BYTE SEAM (FS.GG.SDD#737).
    // ---------------------------------------------------------------------------------------

    /// Round-trip a byte sequence through a real file and `File.ReadAllText`, which is what every
    /// caller of `sha256` actually does. These tests assert against THAT, not against a decoder
    /// spelled out here, because "what the read seam produces" is the fact under test.
    let private readAllTextOf (bytes: byte array) =
        let path =
            Path.Combine(Path.GetTempPath(), "fsgg-737-" + string (System.Guid.NewGuid()))

        try
            File.WriteAllBytes(path, bytes)
            File.ReadAllText path
        finally
            File.Delete path

    let private utf8 (text: string) = Encoding.UTF8.GetBytes text
    let private utf8Bom = [| 0xEFuy; 0xBBuy; 0xBFuy |]

    // THE FAIL-OPEN, PINNED. Two files differing ONLY in one invalid byte are reported EQUAL by the
    // string entry point, because `File.ReadAllText` substituted U+FFFD for each before `sha256`
    // ever saw them. The literal digest is recorded so this test states the exact collision rather
    // than merely "they are equal": if the decode path ever stops mangling, this reds and says so.
    [<Fact>]
    let ``sha256 collides on two files differing only in an invalid byte`` () =
        let oneFf = readAllTextOf [| 0xFFuy |]
        let oneFe = readAllTextOf [| 0xFEuy |]

        Assert.Equal("�", oneFf)
        Assert.Equal("�", oneFe)
        Assert.Equal("83d544ccc223c057d2bf80d3f2a32982c32c3c0db8e2674820da5064783fb097", sha256 oneFf)
        Assert.Equal(sha256 oneFf, sha256 oneFe)

    // AC1/AC4, the red direction: fed the SAME two files' bytes, the byte seam refuses BOTH. It does
    // not report them equal, and it does not report them as a digest MISMATCH either — a refusal is
    // its own outcome, which is the whole point of `Error` carrying a named reason.
    [<Fact>]
    let ``sha256Bytes refuses both files the string entry point collided`` () =
        Assert.Equal<Result<string, BodyRefusalReason>>(Error(NotDecodable 0), sha256Bytes [| 0xFFuy |])
        Assert.Equal<Result<string, BodyRefusalReason>>(Error(NotDecodable 0), sha256Bytes [| 0xFEuy |])

    // THE SAME COLLISION, BEHIND A BOM — the case an earlier draft of this seam let through, on the
    // false premise that `File.ReadAllText` "detects a UTF-16/32 BOM and decodes correctly, so there
    // is nothing to refuse". It mangles those too: an odd byte length, an unpaired surrogate and a
    // UTF-32 scalar above U+10FFFF each produce the SAME U+FFFD and therefore the SAME 83d544cc…
    // digest. A refusal that fired only on UTF-8 would have closed the front door and left this open.
    [<Fact>]
    let ``a MANGLED UTF-16 or UTF-32 body is refused, not collided`` () =
        let collidingPairs =
            [ // UTF-16 BE, odd length: the trailing byte has no partner.
              "utf-16 BE odd", [| 0xFEuy; 0xFFuy; 0x41uy |], [| 0xFEuy; 0xFFuy; 0x42uy |]
              // UTF-16 LE, odd length.
              "utf-16 LE odd", [| 0xFFuy; 0xFEuy; 0x41uy |], [| 0xFFuy; 0xFEuy; 0x42uy |]
              // UTF-16 LE, unpaired HIGH surrogate (D800/D801) with nothing following it.
              "utf-16 LE lone surrogate", [| 0xFFuy; 0xFEuy; 0x00uy; 0xD8uy |], [| 0xFFuy; 0xFEuy; 0x01uy; 0xD8uy |]
              // UTF-32 LE, scalar above U+10FFFF.
              "utf-32 LE out of range",
              [| 0xFFuy; 0xFEuy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x11uy; 0x00uy |],
              [| 0xFFuy; 0xFEuy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x12uy; 0x00uy |] ]

        for (label, a, b) in collidingPairs do
            // The read seam really does collide them — this is the defect, restated per case rather
            // than assumed, so the test cannot silently stop testing anything.
            let textA = readAllTextOf a
            let textB = readAllTextOf b

            Assert.True(
                sha256 textA = sha256 textB,
                sprintf "%s: expected File.ReadAllText to collide these, got %A vs %A" label textA textB
            )

            // And the byte seam refuses both rather than reproducing the collision.
            Assert.True(
                (match decodeBody a, decodeBody b with
                 | Error _, Error _ -> true
                 | _ -> false),
                sprintf "%s: decodeBody %A / %A" label (decodeBody a) (decodeBody b)
            )

    // The offset names a byte of the FILE, and it is the byte the first invalid SEQUENCE begins at —
    // not the byte the decoder happened to stop on. Pinned past a BOM too, since the preamble is
    // stripped before the decoder runs and an un-corrected offset would point one file-position per
    // preamble byte too early.
    [<Fact>]
    let ``NotDecodable names the offset the first invalid sequence begins at`` () =
        Assert.Equal<Result<string, BodyRefusalReason>>(
            Error(NotDecodable 3),
            decodeBody (Array.append (utf8 "abc") [| 0x80uy; 0x64uy |])
        )

        // A truncated multi-byte sequence: the offset is its FIRST byte, not its last.
        Assert.Equal<Result<string, BodyRefusalReason>>(Error(NotDecodable 0), decodeBody [| 0xE2uy; 0x82uy |])

        // Past a UTF-8 BOM the offset still counts from the start of the file.
        Assert.Equal<Result<string, BodyRefusalReason>>(
            Error(NotDecodable 4),
            decodeBody (Array.concat [ utf8Bom; utf8 "a"; [| 0xC0uy; 0xAFuy |] ])
        )

        // Whatever encoding the preamble selected, the offset is inside the file and everything
        // before it decodes — the invariant that makes the number usable in a diagnostic.
        for bytes in
            [ Array.append (utf8 "abc") [| 0x80uy |]
              Array.concat [ utf8Bom; utf8 "ok"; [| 0xFFuy |] ]
              [| 0xFEuy; 0xFFuy; 0x41uy |]
              [| 0xFFuy; 0xFEuy; 0x00uy; 0xD8uy |] ] do
            match decodeBody bytes with
            | Error(NotDecodable offset) ->
                Assert.InRange(offset, 0, bytes.Length - 1)
                Assert.True((decodeBody bytes[.. offset - 1]) |> Result.isOk, sprintf "prefix of %A must decode" bytes)
            | Ok text -> Assert.Fail(sprintf "expected a refusal for %A, got %A" bytes text)

    // AC4, the green direction, stated as the equivalence that makes it true: for every body that
    // decodes, the byte seam produces CHARACTER-FOR-CHARACTER what the read seam produces, so its
    // digest is BYTE-IDENTICAL to today's. CRLF normalization, the empty body and multi-byte
    // sequences all ride on that one equivalence rather than each needing its own digest literal.
    [<Fact>]
    let ``decodeBody is File_ReadAllText for every body that decodes`` () =
        let bodies =
            [ "decodeBody: empty", [||]
              "decodeBody: ascii", utf8 "# Skill\n\nbody\n"
              "decodeBody: multibyte", utf8 "héllo — naïve ✅ 🚀\n"
              "decodeBody: crlf", utf8 "line one\r\nline two\r\n"
              "decodeBody: lone cr", utf8 "line one\rline two\n"
              "decodeBody: nul", [| 0x61uy; 0x00uy; 0x62uy |]
              "decodeBody: utf-8 BOM", Array.append utf8Bom (utf8 "# Skill\n")
              "decodeBody: utf-16 LE BOM", Array.append [| 0xFFuy; 0xFEuy |] (Encoding.Unicode.GetBytes "hi\n")
              "decodeBody: utf-16 BE BOM", Array.append [| 0xFEuy; 0xFFuy |] (Encoding.BigEndianUnicode.GetBytes "hi\n")
              "decodeBody: utf-32 LE BOM",
              Array.append [| 0xFFuy; 0xFEuy; 0x00uy; 0x00uy |] (UTF32Encoding(false, false).GetBytes "hi\n")
              "decodeBody: utf-32 BE BOM",
              Array.append [| 0x00uy; 0x00uy; 0xFEuy; 0xFFuy |] (UTF32Encoding(true, false).GetBytes "hi\n") ]

        for (label, bytes) in bodies do
            let expected = readAllTextOf bytes

            Assert.True(
                Ok expected = decodeBody bytes,
                sprintf "%s: decodeBody %A, File.ReadAllText %A" label (decodeBody bytes) expected
            )

            Assert.True(
                Ok(sha256 expected) = sha256Bytes bytes,
                sprintf "%s: sha256Bytes %A, sha256 (File.ReadAllText …) %A" label (sha256Bytes bytes) (sha256 expected)
            )

    // The other half of the previous test, and the boundary that keeps the refusal honest: a
    // WELL-FORMED UTF-16/UTF-32 body still decodes and is still NOT refused. A stricter
    // strict-UTF-8-only implementation would refuse all four of these — `FF FE` is not valid UTF-8 —
    // turning files that read fine today into hard failures, which is a migration nobody authorised
    // and is NOT what FS-GG/.github#1589's separate BOM disagreement asks for.
    [<Fact>]
    let ``a well-formed UTF-16 or UTF-32 body decodes rather than being refused`` () =
        let wellFormed =
            [ Array.append [| 0xFFuy; 0xFEuy |] (Encoding.Unicode.GetBytes "# Skill\n")
              Array.append [| 0xFEuy; 0xFFuy |] (Encoding.BigEndianUnicode.GetBytes "# Skill\n")
              Array.append [| 0xFFuy; 0xFEuy; 0x00uy; 0x00uy |] (UTF32Encoding(false, false).GetBytes "# Skill\n")
              Array.append [| 0x00uy; 0x00uy; 0xFEuy; 0xFFuy |] (UTF32Encoding(true, false).GetBytes "# Skill\n") ]

        for bytes in wellFormed do
            Assert.Equal<Result<string, BodyRefusalReason>>(Ok "# Skill\n", decodeBody bytes)

        // Including a surrogate PAIR, which is well-formed UTF-16 and must not be mistaken for the
        // unpaired-surrogate case the previous test refuses.
        Assert.Equal<Result<string, BodyRefusalReason>>(
            Ok "🚀",
            decodeBody (Array.append [| 0xFFuy; 0xFEuy |] (Encoding.Unicode.GetBytes "🚀"))
        )

    // AC4 over the REAL TREE rather than over fixtures: every file this repository actually fans out
    // across the three skill roots keeps EXACTLY the digest it has today. This is the "stays green,
    // byte-identical" half, and it is measured, not asserted in the abstract — when this landed it
    // covered 100+ files and refused none of them.
    [<Fact>]
    let ``every committed skill file keeps its digest under the byte seam`` () =
        let files =
            roots
            |> List.map (fun root -> Path.Combine(TestSupport.repoRoot, root, "skills"))
            |> List.filter Directory.Exists
            |> List.collect (fun dir -> Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories) |> List.ofSeq)

        Assert.NotEmpty files

        for file in files do
            let bytes = File.ReadAllBytes file
            Assert.Equal<Result<string, BodyRefusalReason>>(Ok(File.ReadAllText file), decodeBody bytes)
            Assert.Equal<Result<string, BodyRefusalReason>>(Ok(sha256 (File.ReadAllText file)), sha256Bytes bytes)
