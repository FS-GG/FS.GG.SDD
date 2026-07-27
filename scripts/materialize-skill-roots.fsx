// materialize-skill-roots.fsx — rematerialize THIS repo's own ADR-0011 agent-skill roots
// through `Fsgg.SkillMirror`, the one materialize/verify implementation (ADR-0014 §Decision 2/3/5)
// that this repository OWNS and that FS-GG/.github's `skill-union` assertion follows
// (aligned in .github#120).
//
//   dotnet build src/FS.GG.Contracts/FS.GG.Contracts.fsproj -c Release
//   dotnet fsi scripts/materialize-skill-roots.fsx            # materialize (writes)
//   dotnet fsi scripts/materialize-skill-roots.fsx --check     # read-only; exit 1 on drift
//
// WHY THIS EXISTS (FS.GG.SDD#716). `Fsgg.SkillMirror` was routed through the CONSUMER lanes
// (`scaffold`/`refresh`/`doctor`/`upgrade`, feature 058) — every one of which materializes a
// scaffolded *workspace*. Nothing drove it over this repository's OWN committed roots, so the
// producer of the three-root contract did not satisfy it in its own tree: `.claude`=32,
// `.codex`=21, `.agents`=4, with 28 skills partitioned and NOTHING divergent. This script is the
// missing driver. It is deliberately a driver and not a second algorithm: every destination path,
// every digest and the whole verdict come from the library.
//
// NOT A `cp -R`. A copy produces the same bytes today and reproduces the defect on the next
// producer change, because nothing then knows the derived roots are derived. This script instead
//   (1) proves, per skill, which root holds the PRODUCER-AUTHORITATIVE body,
//   (2) content-addresses the process set against the producer's own committed manifest
//       (`.agents/skills/skill-manifest.json`) using `SkillMirror.sha256`, and
//   (3) computes the writes with `SkillMirror.mirrorFiles` and the verdict with `SkillMirror.verify`.
//
// MULTI-FILE (FS.GG.SDD#717). This driver originally refused `[unrepresentable]` when a skill
// carrying non-`SKILL.md` files was PARTITIONED across roots, because `SkillMirror.mirror` modelled
// a skill as `(id, body)` and emitted `SKILL.md` and nothing else. That refusal was a guard around a
// LIBRARY limitation, honest only until the library could express the case. #717 made the library
// express it (`MultiFileSkill`/`mirrorFiles`), so the guard is gone and the whole file set —
// `SKILL.md` + `references/**` + `agents/*.yaml`, which is what every kit-owned coordination skill
// here actually is — goes through the one implementation. What did NOT change is the refusal to
// arbitrate: divergence across roots is still refused at the producer, never flattened.

#r "../src/FS.GG.Contracts/bin/Release/net10.0/FS.GG.Contracts.dll"

open System
open System.IO
open System.Text
open System.Text.Json
open Fsgg

let checkOnly = fsi.CommandLineArgs |> Array.exists (fun a -> a = "--check")

let repoRoot =
    let rec find (dir: DirectoryInfo) =
        if File.Exists(Path.Combine(dir.FullName, "FS.GG.SDD.sln")) then
            dir.FullName
        elif isNull dir.Parent then
            failwith "Could not locate repository root (FS.GG.SDD.sln)."
        else
            find dir.Parent

    find (DirectoryInfo __SOURCE_DIRECTORY__)

/// ADR-0014 §Decision 5's one declared root set — read from the library, never re-spelled here.
let roots = Schemas.agentSkillRoots

let abs (relative: string) =
    Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar))

// ---------------------------------------------------------------------------------------------
// Observe the three roots.
// ---------------------------------------------------------------------------------------------

/// Every skill id present under `<root>/skills/` (a directory holding a SKILL.md).
let idsIn (root: string) =
    let dir = abs (root + "/skills")

    if Directory.Exists dir then
        Directory.EnumerateDirectories dir
        |> Seq.map Path.GetFileName
        |> Seq.filter (fun id -> File.Exists(abs (SkillMirror.skillPath root id)))
        |> Set.ofSeq
    else
        Set.empty

let present = roots |> List.map (fun r -> r, idsIn r) |> Map.ofList

/// The union — the subject of the `skill-union` capability (.github#1504): every skill any root
/// holds. `coordination-coherence`'s subject is only the kit-owned subset, which is why it was
/// green throughout on a tree with 28 partitioned skills.
let union =
    present |> Map.values |> Seq.fold Set.union Set.empty |> Set.toList |> List.sort

let readBody (root: string) (id: string) =
    let p = abs (SkillMirror.skillPath root id)
    if File.Exists p then Some(File.ReadAllText p) else None

/// Every file a skill carries in `root`, as paths RELATIVE to `<root>/skills/<id>/` — `SKILL.md`
/// plus whatever `references/**` and `agents/*.yaml` it has. A skill IS this set (FS.GG.SDD#717):
/// `SkillMirror.mirrorFiles` materializes all of it, so the driver observes all of it.
let filesIn (root: string) (id: string) =
    let dir = abs (root + "/skills/" + id)

    if Directory.Exists dir then
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
        |> Seq.map (fun f -> Path.GetRelativePath(dir, f).Replace('\\', '/'))
        |> Set.ofSeq
    else
        Set.empty

// ---------------------------------------------------------------------------------------------
// Which root holds the producer-authoritative body?
// ---------------------------------------------------------------------------------------------
// Confirmed FROM THE REPO, not assumed (FS.GG.SDD#716 criterion 2):
//
//   fs-gg-sdd-*   `src/FS.GG.SDD.Commands/FS.GG.SDD.Commands.fsproj` links
//                 `../../.claude/skills/<id>/SKILL.md` as the `SeededSkill.<id>` EmbeddedResource,
//                 and `SeededSkills.fs` names those files "the canonical bodies". `.claude` is
//                 where the producer's source of record lives.
//   speckit-*     `.specify/integrations/claude.manifest.json` records the spec-kit installer's
//                 own output at `.claude/skills/<id>/SKILL.md` with its digest; the three the
//                 `fsharp-opinionated` preset re-derives carry `metadata.source:
//                 preset:fsharp-opinionated`. `.claude` is where that producer writes.
//   spectre-console  vendored co-tenant, `metadata.source: FS.GG.Governance spec 091`; its
//                 committed `.claude` body is the only copy, hence canonical.
//   kit-owned 4   materialized from the FS.GG.Kit pin by `kit-materialize.yml` into all three
//                 roots already, and gated by `coordination-coherence`.
//
// Every one of those producers writes `.claude` first, and `agentSkillRoots` lists `.claude`
// first — so "first root that has it" IS the producer-authoritative body. It is a derivation and
// not a coin-flip because cross-root identity is asserted below before the pick is used: when a
// skill exists in more than one root, all copies are equal, so the choice cannot matter; when it
// exists in exactly one, that root is by construction the producer's write location.
let canonicalRootOf (id: string) =
    roots |> List.tryFind (fun r -> Set.contains id (present.[r]))

let mutable failures: string list = []
let fail msg = failures <- failures @ [ msg ]

// Guard: `SkillMirror` models a body as a `string`, so a file only survives the library
// byte-exactly if its on-disk bytes are exactly the UTF-8 (no BOM) encoding of its decoded text.
// A BOM is the realistic violation: `ReadAllText` strips it, so the projected copies would lose it
// and the roots would differ by three bytes the library cannot see. Refuse rather than emit a tree
// the gate will fail for a reason this script called clean.
//
// FS.GG.SDD#717 widened this from SKILL.md to EVERY file of the skill: `mirrorFiles` carries the
// auxiliaries through the same `string` model, so they inherit the same constraint. A guard that
// covered only the file the old library could carry would have gone quiet exactly as the library
// grew able to carry the rest.
//
// FS.GG.SDD#721 moved it AHEAD of the divergence guard below, which is now stated in terms of the
// library's `string` bodies. That ordering is load-bearing: two copies differing only by a BOM are
// EQUAL as strings, so a string-level divergence check running first would call them coherent and
// report nothing. With this guard first, any file whose bytes are not exactly `utf8NoBom(text)` is
// refused outright, and for every file that survives it string equality IS byte equality — which is
// what makes the library verdict below a faithful statement about the bytes on disk.
//
// It therefore covers every file in every skill DIRECTORY, and is deliberately NOT gated on
// `present`. `present` requires a `SKILL.md`, so a root holding stray auxiliaries in a SKILL.md-less
// directory would skip the byte check — and those very files reappear in the post-materialization
// observation once the fan-out writes the missing `SKILL.md`. A BOM'd stray would then be compared
// as a STRING, found equal, and reported clean by a verdict that is supposed to be about bytes.
// `filesIn` yields nothing for a directory that does not exist, so dropping the gate costs nothing.
let utf8NoBom = UTF8Encoding(false)

for id in union do
    for root in roots do
        for rel in filesIn root id do
            let p = abs (root + "/skills/" + id + "/" + rel)
            let raw = File.ReadAllBytes p

            if raw <> utf8NoBom.GetBytes(File.ReadAllText p) then
                fail (
                    $"[unrepresentable] {root}/skills/{id}/{rel} does not round-trip through the "
                    + "library's string body model (a UTF-8 BOM, or a non-UTF-8 encoding). "
                    + "`SkillMirror.mirrorFiles` cannot carry these bytes byte-exactly — normalize "
                    + "the file to UTF-8 without a BOM."
                )

/// Every file every root carries for every union skill, as the library's own observation type.
/// `Files = None` means the root has no copy of the skill at all — distinct from a copy that is
/// present but incomplete, which is the distinction the whole verdict below turns on.
///
/// Presence is re-read FROM DISK on every call (the same "a directory holding a SKILL.md" test
/// `idsIn` uses), deliberately NOT from the `present` map. `present` is a snapshot taken before
/// the fan-out; reusing it after the writes would report every root the fan-out just repaired as
/// still missing the skill — a verdict about a tree that no longer exists.
let observe () : SkillMirror.ActualSkillFiles list =
    [ for root in roots do
          for id in union ->
              { Root = root
                Id = id
                Files =
                  if File.Exists(abs (SkillMirror.skillPath root id)) then
                      Some
                          [ for rel in filesIn root id ->
                                { SkillMirror.SkillFile.RelativePath = rel
                                  Body = File.ReadAllText(abs (root + "/skills/" + id + "/" + rel)) } ]
                  else
                      None } ]

// Guard: a skill present in several roots must ALREADY agree — the same file SET, and the same
// BYTES for every file in it — or this is a DIVERGENCE and the repair is a producer question, not
// a fan-out. This driver fans out a canonical copy; it must never pick a winner between two
// producers, and multi-file support must NOT become a way to flatten divergence silently.
//
// #716 asserted this over SKILL.md, and separately over the auxiliary set behind an
// `[unrepresentable]` refusal. #717 unified them into ONE guard over the file set — but as a
// HAND-ROLLED byte-comparison loop sitting next to the library, because `SkillMirror.verify` still
// modelled a skill as one body and could not state it. That was the second implementation of the
// verify half, the exact thing ADR-0014 §Decision 2 exists to end (FS.GG.SDD#721).
//
// It is now the library's verdict. `verifyFiles` compares whole file sets across roots and names
// the offending FILE; this loop only renders what it returns. Only the CROSS-ROOT facts refuse
// here: a skill absent from a root ENTIRELY is `MultiFileSkillDrift.MissingRoots`, and that is not
// a divergence — it is precisely the work the fan-out below is about to do.
let preMaterializeExpected: SkillMirror.ExpectedSkill list =
    union
    |> List.map (fun id ->
        { Id = id
          Scope = Schemas.SkillScope.Process
          // No reference digest at this stage: the manifest hash is asserted separately, below,
          // against the CANONICAL body once that root has been proven. Here the subject is only
          // whether the roots agree WITH EACH OTHER.
          Sha256 = "" })

for d in SkillMirror.verifyFiles roots preMaterializeExpected (observe ()) do
    for f in d.Files do
        if not (List.isEmpty f.MissingRoots) then
            fail (
                $"[divergent] {d.Id}: {f.RelativePath} is carried by some roots but MISSING from "
                + $"%A{f.MissingRoots} — the roots carry DIFFERENT file sets. Resolve at the producer."
            )

        if f.Divergent then
            fail (
                $"[divergent] {d.Id}: the roots disagree on {f.RelativePath}. This driver fans out a "
                + "canonical copy; it must not pick a winner between two producers. "
                + "Resolve at the producer."
            )

// ---------------------------------------------------------------------------------------------
// Content-address the process set against the PRODUCER's own committed manifest.
// ---------------------------------------------------------------------------------------------
// `.agents/skills/skill-manifest.json` is this repo's producer manifest (ADR-0017, schema v1),
// emitted by `fsgg-sdd registry skill-manifest`. Every entry is `materializes-when: always`, so
// each declared id MUST be materialized in every root — the exact claim `.agents`=4 violated.
let manifestPath =
    abs (SkillMirror.providerSourceRoot + "/skills/skill-manifest.json")

let declaredDigests =
    if File.Exists manifestPath then
        use doc = JsonDocument.Parse(File.ReadAllText manifestPath)

        doc.RootElement.GetProperty("skills").EnumerateArray()
        |> Seq.map (fun e -> e.GetProperty("id").GetString(), e.GetProperty("sha256").GetString())
        |> Map.ofSeq
    else
        failwith $"producer manifest missing: {manifestPath}"

/// The canonical MULTI-FILE skills `mirrorFiles` fans out, each proven against its authority.
/// FS.GG.SDD#717: this used to be `(id, body)` — one id, one body — which is precisely what could
/// not express a skill carrying `references/**` and `agents/*.yaml`. It is now the whole file set,
/// read from the one root proven canonical above.
let canonicalSkills: SkillMirror.MultiFileSkill list =
    union
    |> List.map (fun id ->
        match canonicalRootOf id with
        | None -> failwith $"no body found for {id}"
        | Some root ->
            match readBody root id with
            | None -> failwith $"no body found for {id}"
            | Some body ->
                // The library's OWN digest function against the producer's OWN declared digest.
                // The manifest content-addresses the SKILL.md body (ADR-0017), so that is what is
                // compared here — the auxiliaries are covered by the cross-root identity guard.
                match Map.tryFind id declaredDigests with
                | Some declared when SkillMirror.sha256 body <> declared ->
                    fail (
                        $"[drifted] {id}: canonical body sha256={SkillMirror.sha256 body} but the producer "
                        + $"manifest declares {declared}. Regenerate with `fsgg-sdd registry skill-manifest --write`."
                    )
                | _ -> ()

                { SkillMirror.MultiFileSkill.Id = id
                  Files =
                    filesIn root id
                    |> Set.toList
                    |> List.map (fun rel ->
                        { SkillMirror.SkillFile.RelativePath = rel
                          Body = File.ReadAllText(abs (root + "/skills/" + id + "/" + rel)) }) })

// The plan, and with it the library's OWN refusals — surfaced verbatim rather than re-derived.
// `[unrepresentable]` used to live in this script as a hand-written guard around `mirror`'s
// SKILL.md-only model (#716). FS.GG.SDD#717 closed that hole, so the only refusals left are the
// library's own lexical-confinement and duplicate guards. On a tree this driver has already proven
// coherent they cannot fire — but they are REPORTED rather than assumed away, because a guard you
// assume cannot fire is a guard nobody notices going quiet.
let plan = SkillMirror.mirrorFiles roots canonicalSkills

for refusal in plan.Refused do
    for reason in refusal.Reasons do
        fail $"[refused] {refusal.Id}: %A{reason} (Fsgg.SkillMirror.mirrorFiles)"

if not (List.isEmpty failures) then
    eprintfn "materialize-skill-roots: REFUSED — %d precondition failure(s):" (List.length failures)

    for f in failures do
        eprintfn "  %s" f

    exit 2

// ---------------------------------------------------------------------------------------------
// Materialize: every write comes from `SkillMirror.mirrorFiles`.
// ---------------------------------------------------------------------------------------------
let writes = plan.Writes

let mutable changed: string list = []

// Compare and write BYTES, never decoded strings. `File.ReadAllText` silently strips a UTF-8 BOM, so
// a string comparison would call a BOM'd `.claude` and a BOM-less `.codex` copy "unchanged" while the
// gate — which uses `diff -r` — fails them `[divergent]`. A verdict weaker than the invariant it
// asserts is exactly the defect class this whole item exists to close (cf. FS-GG/.github#1506), so
// this driver's notion of "unchanged" is byte equality, the same notion the gate uses.
for w in writes do
    let target = abs w.Path
    let desired = utf8NoBom.GetBytes w.Body

    let current =
        if File.Exists target then
            Some(File.ReadAllBytes target)
        else
            None

    if current <> Some desired then
        changed <- changed @ [ w.Path ]

        if not checkOnly then
            Directory.CreateDirectory(Path.GetDirectoryName target: string) |> ignore
            File.WriteAllBytes(target, desired)

// ---------------------------------------------------------------------------------------------
// Verify: the verdict comes from `SkillMirror.verify`.
// ---------------------------------------------------------------------------------------------
let expected: SkillMirror.ExpectedSkill list =
    canonicalSkills
    |> List.map (fun skill ->
        let id = skill.Id

        { Id = id
          // `scope` here is only carried through to the drift report; the process set is what the
          // producer manifest declares, everything else in the union is a co-tenant product/process
          // skill this repo vendors.
          Scope =
            (if Map.containsKey id declaredDigests then
                 Schemas.SkillScope.Process
             else
                 Schemas.SkillScope.Product)
          // Reference digest only where the producer declares one; "" means skip hash-match and
          // assert presence + cross-root identity only (the library's documented semantics).
          Sha256 = (Map.tryFind id declaredDigests |> Option.defaultValue "") })

// FS.GG.SDD#721: the verdict is taken over the skill's WHOLE FILE SET, not just `SKILL.md`.
// Before this, the driver materialized `references/**` and `agents/*.yaml` through `mirrorFiles`
// and then verified only the one file the old `verify` could see — a verdict weaker than the
// invariant it claimed, the same defect class as FS-GG/.github#1506. `verifyFiles` closes it, and
// the observation is RE-READ from disk after the writes, so this measures the tree that now exists
// rather than restating the plan that produced it.
let actual = observe ()

let drift = SkillMirror.verifyFiles roots expected actual

printfn "materialize-skill-roots (%s)" (if checkOnly then "--check" else "write")
printfn "  roots        : %s" (String.concat " " roots)
printfn "  union        : %d skills" (List.length union)
printfn "  files        : %d across the union" (canonicalSkills |> List.sumBy (fun s -> List.length s.Files))
printfn "  writes        : %d planned by SkillMirror.mirrorFiles" (List.length writes)
printfn "  changed      : %d" (List.length changed)

for p in changed do
    printfn "      %s %s" (if checkOnly then "DRIFT" else "wrote") p

if not (List.isEmpty drift) then
    eprintfn "  verify       : %d skill(s) still drifted" (List.length drift)

    for d in drift do
        eprintfn "      %s missingRoots=%A" d.Id d.MissingRoots

        for f in d.Files do
            eprintfn
                "        %s missing=%A divergent=%b hashMismatch=%A"
                f.RelativePath
                f.MissingRoots
                f.Divergent
                f.HashMismatchRoots

    exit 1

printfn
    "  verify       : clean — every union skill present in every root, every FILE byte-identical across roots, SKILL.md hash-matched"

if checkOnly && not (List.isEmpty changed) then
    eprintfn "  --check: %d path(s) would change; run without --check to materialize." (List.length changed)
    exit 1
