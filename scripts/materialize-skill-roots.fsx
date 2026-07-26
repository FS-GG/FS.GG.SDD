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
//   (3) computes the writes with `SkillMirror.mirror` and the verdict with `SkillMirror.verify`.

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

// Guard: a skill present in several roots must already agree, or this is a DIVERGENCE and the
// repair is a producer question, not a fan-out. (#716 measured none; this keeps it that way.)
for id in union do
    let bodies =
        roots |> List.choose (fun r -> readBody r id |> Option.map (fun b -> r, b))

    match bodies with
    | [] -> ()
    | (r0, b0) :: rest ->
        for (r, b) in rest do
            if b <> b0 then
                fail (
                    $"[divergent] {id}: {r0} and {r} disagree. This driver fans out a canonical body; "
                    + "it must not pick a winner between two producers. Resolve at the producer."
                )

// Guard: `SkillMirror.mirror` materializes `<root>/skills/<id>/SKILL.md` and NOTHING ELSE, so a
// multi-file skill (SKILL.md + references/**) cannot be projected through the library. The
// kit-owned four are multi-file and ALREADY coherent in all three roots (FS.GG.Kit materializes
// them), so they are pass-through here. A multi-file skill that is *partitioned* would be
// unrepresentable — refuse loudly rather than hand-rolling a second copier around the library
// (FS.GG.SDD#717).
let auxFilesOf (root: string) (id: string) =
    let dir = abs (root + "/skills/" + id)

    if Directory.Exists dir then
        Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
        |> Seq.map (fun f -> Path.GetRelativePath(dir, f).Replace('\\', '/'))
        |> Seq.filter (fun rel -> rel <> "SKILL.md")
        |> Set.ofSeq
    else
        Set.empty

for id in union do
    let rootsHaving = roots |> List.filter (fun r -> Set.contains id present.[r])
    let aux = rootsHaving |> List.map (fun r -> auxFilesOf r id)

    if aux |> List.exists (Set.isEmpty >> not) then
        if List.length rootsHaving <> List.length roots then
            fail (
                $"[unrepresentable] {id} carries non-SKILL.md files and is NOT present in every root. "
                + "`Fsgg.SkillMirror.mirror` emits SKILL.md only, so this skill cannot be materialized "
                + "through the library — escalate to the library owner (see FS.GG.SDD#717)."
            )
        else
            let first = List.head aux

            if aux |> List.exists (fun s -> s <> first) then
                fail $"[divergent] {id}: auxiliary file SETS differ across roots {rootsHaving}."
            else
                // Multi-file and coherent — assert the aux bytes agree too, then leave alone.
                for rel in first do
                    let bytesOf (r: string) =
                        File.ReadAllBytes(abs (r + "/skills/" + id + "/" + rel))

                    let b0 = bytesOf (List.head rootsHaving)

                    for r in List.tail rootsHaving do
                        if bytesOf r <> b0 then
                            fail $"[divergent] {id}: auxiliary file {rel} differs between roots."

// Guard: `SkillMirror.mirror` models a body as a `string`, so a body only survives the library
// byte-exactly if its on-disk bytes are exactly the UTF-8 (no BOM) encoding of its decoded text.
// A BOM is the realistic violation: `ReadAllText` strips it, so the projected copies would lose it
// and the roots would differ by three bytes the library cannot see. Refuse rather than emit a tree
// the gate will fail for a reason this script called clean.
let utf8NoBom = UTF8Encoding(false)

for id in union do
    for root in roots do
        if Set.contains id present.[root] then
            let p = abs (SkillMirror.skillPath root id)
            let raw = File.ReadAllBytes p

            if raw <> utf8NoBom.GetBytes(File.ReadAllText p) then
                fail (
                    $"[unrepresentable] {root}/skills/{id}/SKILL.md does not round-trip through the "
                    + "library's string body model (a UTF-8 BOM, or a non-UTF-8 encoding). "
                    + "`SkillMirror.mirror` cannot carry these bytes byte-exactly — normalize the file "
                    + "to UTF-8 without a BOM."
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

/// The canonical (id, body) pairs `mirror` fans out, each proven against its authority.
let canonicalSkills =
    union
    |> List.map (fun id ->
        match canonicalRootOf id |> Option.bind (fun r -> readBody r id) with
        | None -> failwith $"no body found for {id}"
        | Some body ->
            // The library's OWN digest function against the producer's OWN declared digest.
            match Map.tryFind id declaredDigests with
            | Some declared when SkillMirror.sha256 body <> declared ->
                fail (
                    $"[drifted] {id}: canonical body sha256={SkillMirror.sha256 body} but the producer "
                    + $"manifest declares {declared}. Regenerate with `fsgg-sdd registry skill-manifest --write`."
                )
            | _ -> ()

            id, body)

if not (List.isEmpty failures) then
    eprintfn "materialize-skill-roots: REFUSED — %d precondition failure(s):" (List.length failures)

    for f in failures do
        eprintfn "  %s" f

    exit 2

// ---------------------------------------------------------------------------------------------
// Materialize: every write comes from `SkillMirror.mirror`.
// ---------------------------------------------------------------------------------------------
let writes = SkillMirror.mirror roots canonicalSkills

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
    |> List.map (fun (id, _) ->
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

let actual: SkillMirror.ActualCopy list =
    [ for root in roots do
          for (id, _) in canonicalSkills ->
              { Root = root
                Id = id
                Body = readBody root id } ]

let drift = SkillMirror.verify roots expected actual

printfn "materialize-skill-roots (%s)" (if checkOnly then "--check" else "write")
printfn "  roots        : %s" (String.concat " " roots)
printfn "  union        : %d skills" (List.length union)
printfn "  writes        : %d planned by SkillMirror.mirror" (List.length writes)
printfn "  changed      : %d" (List.length changed)

for p in changed do
    printfn "      %s %s" (if checkOnly then "DRIFT" else "wrote") p

if not (List.isEmpty drift) then
    eprintfn "  verify       : %d skill(s) still drifted" (List.length drift)

    for d in drift do
        eprintfn "      %s missing=%A divergent=%b hashMismatch=%A" d.Id d.MissingRoots d.Divergent d.HashMismatchRoots

    exit 1

printfn "  verify       : clean — every union skill present in every root, byte-identical, hash-matched"

if checkOnly && not (List.isEmpty changed) then
    eprintfn "  --check: %d path(s) would change; run without --check to materialize." (List.length changed)
    exit 1
