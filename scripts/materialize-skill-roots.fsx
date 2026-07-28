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
//       (`.claude/skills/skill-manifest.json`, FS.GG.SDD#771) using `SkillMirror.sha256`, and
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
//
// RETIRED ROOTS ARE NOT WRITTEN, AND THE DECLARATION IS READ RATHER THAN RE-SPELLED (FS.GG.SDD#767).
// This driver used to take `Schemas.agentSkillRoots` verbatim as its write set. On 2026-07-28 that
// became a re-materialization hazard rather than a contract: FS.GG.Kit 0.15.0 carried ADR-0067 §5's
// retirement of `.codex/skills` into this receiver and its materializer SWEPT the four kit-owned
// skills (23 files) out of that root, while the constant still declared three. `--check` therefore
// exited 1 with exactly those 23 paths as DRIFT, and the write mode — the command this file's own
// header documents FIRST, and the one `.github/workflows/skill-union.yml` prints as the repair —
// would have put every one of them back, into the root the transport contract had just removed.
// A receiver would have undone a retirement by running its own documented command.
//
// THE FIX IS A DERIVATION, NOT A SECOND CONSTANT. Hard-coding `[".claude"; ".agents"]` here would
// re-create the exact defect one line lower: two declarations of the root set that agree only by
// coincidence, in a file whose whole thesis is that it re-implements nothing. So the write set is
// DERIVED from the two declarations that already exist, each read from its own authority:
//
//   * `Fsgg.Schemas.agentSkillRoots` — ADR-0014 §Decision 5's declared root set, and still the ONLY
//     place a root is added, renamed or ordered. (It reads three today; FS.GG.SDD#757 owns making it
//     two, on the `api-compatibility-gate`-protected `FS.GG.Contracts` surface. This derivation is
//     correct BEFORE and AFTER that lands — subtracting a root that is no longer in the set is a
//     no-op — so neither item blocks the other.)
//   * `FsggKitRetiredSkillRoots` / `FsggKitSkillRoots` / `FsggKitViewSkillRoots` — the pinned
//     FS.GG.Kit package's own consumer declaration (ADR-0062, ADR-0065 §A root's three dispositions),
//     evaluated through MSBuild from `.config/kit/FS.GG.Kit.receiver.proj`. This is not "some other
//     config that happens to agree": `FsggKitRetiredSkillRoots` is the property whose value PERFORMED
//     the sweep this driver would otherwise undo, so reading it makes disagreeing with the sweep
//     unrepresentable rather than merely unlikely.
//
// EVALUATED BY MSBUILD, NOT PARSED BY US. The property is overridable by the receiver (the kit's
// defaults are all `Condition="'$(X)' == ''"`), so its VALUE is the result of an evaluation, and
// re-deriving that from the package's `build/FS.GG.Kit.props` XML would be a second implementation
// of MSBuild — the same mistake, one layer down. `dotnet build -getProperty:` asks the evaluator.
//
// AND IT FAILS CLOSED. An unrestored receiver project evaluates every one of these properties to the
// EMPTY STRING and exits 0 (measured) — the kit's `build/` props are imported by NuGet's generated
// `.g.props`, which does not exist before a restore. Read naively that says "no roots are retired",
// which is precisely the wrong answer in the wrong direction: it would restore the three-root write
// set and re-create the mirrors. So an empty `FsggKitSkillRoots` is treated as A FAILED EVALUATION,
// never as a declaration, and this driver REFUSES rather than materializing an unmeasured root set.
//
// WHAT HAPPENS TO WHAT IS ALREADY UNDER A RETIRED ROOT: NOTHING, HERE (FS.GG.SDD#767 criterion 4).
// `.codex/skills` still holds 28 skills this repo OWNS (`fs-gg-sdd-*`, `speckit-*`, `spectre-console`)
// — the kit's sweep removes only the four kit-owned ones, exactly as ADR-0065 requires. This driver
// neither writes them, reads them, nor deletes them: a receiver hand-deleting a mirror is what
// ADR-0065 §Retiring a root forbids, and deciding the root has no runtime is a contract migration
// owned by ADR-0067 phase 4 (FS-GG/.github#1676), not by this script. It does print what remains, so
// "nothing writes it and nothing audits it" is stated on every run instead of being inferred.

#r "../src/FS.GG.Contracts/bin/Release/net10.0/FS.GG.Contracts.dll"

open System
open System.Diagnostics
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

let abs (relative: string) =
    Path.Combine(repoRoot, relative.Replace('/', Path.DirectorySeparatorChar))

// ---------------------------------------------------------------------------------------------
// The write set: DECLARED by ADR-0014 §Decision 5, NARROWED by the receiver's own retirement.
// ---------------------------------------------------------------------------------------------

let kitReceiverProject = ".config/kit/FS.GG.Kit.receiver.proj"

let kitProperties =
    [ "FsggKitSkillRoots"; "FsggKitRetiredSkillRoots"; "FsggKitViewSkillRoots" ]

/// Run `dotnet` under `repoRoot` and return (exit code, stdout, stderr). stdout and stderr are read
/// to completion CONCURRENTLY (`ReadToEndAsync` before `WaitForExit`): draining one stream and only
/// then the other deadlocks the moment the undrained pipe's buffer fills, which is a hang rather
/// than an error and would look exactly like a slow restore.
let runDotnet (args: string list) =
    let psi = ProcessStartInfo("dotnet")
    psi.WorkingDirectory <- repoRoot
    psi.RedirectStandardOutput <- true
    psi.RedirectStandardError <- true
    psi.UseShellExecute <- false

    for a in args do
        psi.ArgumentList.Add a

    use p = Process.Start psi
    let out = p.StandardOutput.ReadToEndAsync()
    let err = p.StandardError.ReadToEndAsync()
    p.WaitForExit()
    p.ExitCode, out.Result, err.Result

/// `dotnet build -getProperty:X -getProperty:Y …` answers with a `{"Properties":{…}}` document on
/// stdout. MSBuild may print warnings ahead of it, so the document is located rather than assumed to
/// start at byte 0 — and a stdout with no `{` at all is a failure, not an empty property set.
let evaluateKitProperties () =
    let projectArg = kitReceiverProject

    let ask (extra: string list) =
        let args =
            [ "build"; projectArg ]
            @ extra
            @ [ for p in kitProperties -> "-getProperty:" + p ]

        let code, out, err = runDotnet args

        match out.IndexOf '{' with
        | -1 -> Error(sprintf "exit %d, no JSON on stdout.\n%s\n%s" code out err)
        | i ->
            try
                use doc = JsonDocument.Parse(out.Substring i)
                let props = doc.RootElement.GetProperty "Properties"

                Ok(
                    kitProperties
                    |> List.map (fun name ->
                        name,
                        match props.TryGetProperty name with
                        | true, v -> (v.GetString() |> Option.ofObj |> Option.defaultValue "")
                        | _ -> "")
                    |> Map.ofList
                )
            with ex ->
                Error(sprintf "exit %d, unparseable evaluation: %s\n%s" code ex.Message out)

    // An unrestored receiver project evaluates EVERY kit property to "" and exits 0, so the restore
    // is attempted before refusing — a driver that died on a fresh clone would just teach people to
    // skip it. `--no-restore` first, because that is the cached path and costs ~0.3s.
    let refuse detail =
        failwith (
            $"cannot evaluate the pinned FS.GG.Kit root declaration from {kitReceiverProject}: {detail}\n"
            + "This driver derives its write set by subtracting that package's FsggKitRetiredSkillRoots "
            + "from Fsgg.Schemas.agentSkillRoots. An unevaluated declaration is not an empty one "
            + "(FS.GG.SDD#767): reading it as empty would restore the retired root to the write set and "
            + "re-create the mirrors the kit swept. Refusing instead. Repair: "
            + $"`dotnet restore {kitReceiverProject}`."
        )

    if not (File.Exists(abs kitReceiverProject)) then
        refuse "the receiver project is not in this tree"
    else
        let evaluated =
            match ask [ "--no-restore" ] with
            | Ok m when m.["FsggKitSkillRoots"] <> "" -> Ok m
            | _ ->
                let code, out, err = runDotnet [ "restore"; projectArg ]

                if code <> 0 then
                    Error(sprintf "restore failed (exit %d).\n%s\n%s" code out err)
                else
                    ask [ "--no-restore" ]

        match evaluated with
        | Error detail -> refuse detail
        | Ok m when m.["FsggKitSkillRoots"] = "" ->
            refuse
                "FsggKitSkillRoots evaluated EMPTY even after a restore — the package's build/ props were not imported"
        | Ok m -> m

let kitRootDeclaration = evaluateKitProperties ()

/// A `;`-separated MSBuild root list, in the kit's spelling (`.claude/skills`), reduced to the bare
/// repo-root name ADR-0014 §Decision 5's constant uses (`.claude`) — consumers append `skills/`.
/// The two vocabularies are the SAME roots; normalizing here is what lets them be compared at all,
/// and doing it in one place is what stops the comparison from being a spelling accident.
let bareRoots (declaration: string) =
    declaration.Split(';', StringSplitOptions.RemoveEmptyEntries)
    |> Array.map (fun r -> r.Trim().Replace('\\', '/').TrimEnd('/'))
    |> Array.filter (fun r -> r <> "")
    |> Array.map (fun r ->
        if r.EndsWith "/skills" then
            r.Substring(0, r.Length - "/skills".Length)
        else
            r)
    |> Array.toList

let retiredRoots =
    bareRoots kitRootDeclaration.["FsggKitRetiredSkillRoots"] |> Set.ofList

/// The kit's own statement of the RUNTIME surface. Its `build/FS.GG.Kit.props` says it outright:
/// *"the runtime surface is `FsggKitSkillRoots` + `FsggKitViewSkillRoots`, and THAT union is what
/// must equal `.agent-skill-roots` / `agentSkillRoots`"* — a materialized root and a generated-view
/// root are both in the contract; only a RETIRED root leaves it.
let kitRuntimeRoots =
    (bareRoots kitRootDeclaration.["FsggKitSkillRoots"]
     @ bareRoots kitRootDeclaration.["FsggKitViewSkillRoots"])
    |> Set.ofList

/// The write set. Order is `agentSkillRoots`' order, unchanged — `canonicalRootOf` below picks "the
/// first root that has it", so re-ordering here would silently re-point the producer-authoritative
/// body. Filtering a list preserves it; re-deriving one from the kit's declaration would not.
let roots =
    Schemas.agentSkillRoots
    |> List.filter (fun r -> not (Set.contains r retiredRoots))

/// The VIEW roots, read from the same pinned declaration as the retired ones (FS.GG.SDD#770).
///
/// A view root is in the runtime contract — `kitRuntimeRoots` above is deliberately the union that
/// INCLUDES it, and the agreement assertion below must keep seeing it. What it is not is a root this
/// driver may WRITE: its content is GENERATED by `scripts/skill-view` at checkout, never transported.
/// So it is subtracted from the write set only, and never folded into `retiredRoots` — those are
/// different states in ADR-0065 §A root's three dispositions, and collapsing them would make the
/// agreement assertion stop seeing a root the contract still names.
let viewRoots =
    bareRoots kitRootDeclaration.["FsggKitViewSkillRoots"] |> Set.ofList

/// THE WRITE SET — `roots` minus the view roots. `roots` itself stays the OBSERVATION and AGREEMENT
/// set, because both must still see a view root.
///
/// Why this matters when the view is a symlink and the writes appear harmless: `skill-view generate`
/// defaults to `--mode link`, so `.agents/skills` and `.claude/skills` are the SAME OBJECT and every
/// planned write lands on the file it was copied from — `changed : 0`, and the defect is invisible.
/// `--mode auto` falls back to `--mode copy` where the filesystem or OS refuses a symlink, which is
/// its documented Windows path. On a copy-mode view the two roots are genuinely two directories again
/// and this driver's WRITE mode — the command this file's own header documents first — materializes
/// the full union back into the generated root, re-creating exactly the second committed copy the
/// view exists to remove. On a tree where the view has not been generated at all, it CREATES the root
/// as a real directory holding the union. That is FS.GG.SDD#767's re-creation hazard one disposition
/// over.
let writeRoots = roots |> List.filter (fun r -> not (Set.contains r viewRoots))

// The two declarations must AGREE about the runtime surface, and the disagreement is stated rather
// than resolved. This driver is not the place to arbitrate between the published contract constant
// and the pinned transport package: a mismatch means one of them is wrong, and picking a winner is
// how a stale declaration gets laundered into a green run. Today they agree in both directions —
// three roots minus `.codex` equals the kit's two — and they still agree once FS.GG.SDD#757 makes
// the constant two, because subtracting an absent root changes nothing.
if Set.ofList roots <> kitRuntimeRoots then
    failwith (
        sprintf "the declared root set and the pinned FS.GG.Kit declaration disagree about the runtime surface.\n"
        + sprintf "  Schemas.agentSkillRoots minus retired : %s\n" (String.concat " " (List.sort roots))
        + sprintf "  FsggKitSkillRoots + FsggKitViewSkillRoots : %s\n" (String.concat " " (Set.toList kitRuntimeRoots))
        + sprintf "  FsggKitRetiredSkillRoots : %s\n" (String.concat " " (Set.toList retiredRoots))
        + "One of them is stale. FS.GG.SDD#757 owns Schemas.agentSkillRoots; ADR-0062/ADR-0065 own the "
        + "kit declaration. Refusing to pick a winner (FS.GG.SDD#767)."
    )

// ---------------------------------------------------------------------------------------------
// Observe the roots in the write set — and ONLY those. A retired root is not read either: reading it
// would put its contents back in the union, and the very next step fans the union out to every root.
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
// `.claude/skills/skill-manifest.json` is this repo's producer manifest (ADR-0017, schema v1),
// emitted by `fsgg-sdd registry skill-manifest`. Every entry is `materializes-when: always`, so
// each declared id MUST be materialized in every root — the exact claim `.agents`=4 violated.
//
// IT LIVES IN THE TRACKED SOURCE ROOT, NOT THE PROVIDER-SOURCE ROOT (FS.GG.SDD#771). This read used
// to be `SkillMirror.providerSourceRoot + "/skills/skill-manifest.json"` — `.agents/...` — and that
// root is the one a PROVIDER owns in the orchestrated scaffold lane (ADR-0014 §Decision 6), which
// says nothing about where THIS repository's own producer-authoritative files live. Under ADR-0067
// §6 `.agents/skills` becomes a generated VIEW of `.claude/skills`: untracked, git-ignored, absent
// in a bare checkout by construction. A file living only there is deleted by the retirement with a
// CLEAN `git status`, and this very `failwith` then fires on every ordinary run — measured on a
// dry-run retirement of this repo on 2026-07-28, with and without the view generated.
//
// AND THE VIEW WOULD NOT HAVE CARRIED IT ANYWAY. `scripts/skill-view generate --mode copy` — the
// documented fallback for a filesystem or OS that refuses a symlink — copies `<id>/` skill
// directories and nothing else, so a top-level file in the source root has NO counterpart in a
// copy-mode view. Resolving this path "through the view" would have worked in link mode and failed
// in copy mode: a reader whose success depends on which fallback the runner took.
//
// THE ROOT IS DERIVED, NOT RE-SPELLED. `Schemas.agentSkillRoots`' FIRST root is already this repo's
// producer-authoritative root and is already documented as such twice over — `canonicalRootOf`
// below picks "the first root that has it", and `roots` above preserves the constant's order for
// exactly that reason. Reading the same declaration here keeps there being ONE statement of which
// root is authoritative. Deliberately the DECLARATION and not the write set `roots`: `registry
// skill-manifest` in `src/FS.GG.SDD.Cli/RegistrySkillManifest.fs` has no kit declaration to
// subtract with, and the writer and the reader of one file must not be able to disagree about
// where it is.
let manifestPath =
    abs (List.head Schemas.agentSkillRoots + "/skills/skill-manifest.json")

// FS.GG.SDD#727: the manifest now declares a digest for a skill's COMPLETE FILE SET, not its
// `SKILL.md` alone. BOTH schema versions are read, and the reader is what makes the v1 tolerance in
// the amendment's acceptance criteria real rather than asserted:
//
//   v2 — `files: [{ path, sha256 }]` is the complete declared set, verbatim.
//   v1 — `sha256` content-addresses `SKILL.md` and NOTHING ELSE, so a v1 document declares exactly
//        one file. That is not a degraded reading of v1, it is v1's actual claim; the auxiliaries
//        genuinely had no declared authority, and the reader must not invent one for them.
//
// The version is read rather than assumed, so an unrecognized FUTURE version refuses instead of
// being silently reinterpreted through today's rules.
let manifestSchemaVersion, declaredFiles =
    if not (File.Exists manifestPath) then
        failwith $"producer manifest missing: {manifestPath}"
    else

        use doc = JsonDocument.Parse(File.ReadAllText manifestPath)
        let root = doc.RootElement

        let version =
            match root.TryGetProperty "schemaVersion" with
            | true, v -> v.GetInt32()
            | _ -> failwith $"producer manifest has no schemaVersion: {manifestPath}"

        if version <> 1 && version <> 2 then
            failwith (
                $"producer manifest {manifestPath} declares schemaVersion {version}, which this driver "
                + "does not know how to read. Refusing rather than reinterpreting it as v2."
            )

        let declaredFile (path: string) (sha: string) : Schemas.SkillManifestFile =
            { RelativePath = path; Sha256 = sha }

        let sets =
            root.GetProperty("skills").EnumerateArray()
            |> Seq.map (fun e ->
                let id = e.GetProperty("id").GetString()

                let files =
                    match e.TryGetProperty "files" with
                    | true, arr ->
                        [ for f in arr.EnumerateArray() ->
                              declaredFile (f.GetProperty("path").GetString()) (f.GetProperty("sha256").GetString()) ]
                    | _ -> [ declaredFile "SKILL.md" (e.GetProperty("sha256").GetString()) ]

                id, files)
            |> Map.ofSeq

        version, sets

/// The `SKILL.md` digest per declared id — the canonical-body authority, read from the ROW-LEVEL
/// `sha256` exactly as before this amendment. v2 retains that property with its v1 meaning, so this
/// map is byte-for-byte what it always was at both schema versions.
///
/// Deliberately NOT derived from `declaredFiles`'s `SKILL.md` row, though the two agree in every
/// document this repo emits: a hand-edited v2 entry that omitted its `SKILL.md` row would then drop
/// out of this map, and the `[drifted]` canonical-body assertion below would silently skip that
/// skill. Reading the field that has always carried this fact keeps the guard's reach unchanged.
/// (The omission is still caught — `verifyFileSet` reports the undeclared `SKILL.md` — but a guard
/// must not go quiet just because another one would notice.)
let declaredDigests =
    if not (File.Exists manifestPath) then
        failwith $"producer manifest missing: {manifestPath}"
    else

        use doc = JsonDocument.Parse(File.ReadAllText manifestPath)

        doc.RootElement.GetProperty("skills").EnumerateArray()
        |> Seq.map (fun e -> e.GetProperty("id").GetString(), e.GetProperty("sha256").GetString())
        |> Map.ofSeq

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
let plan = SkillMirror.mirrorFiles writeRoots canonicalSkills

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
// Verify: the verdict comes from `SkillMirror.verifyFileSet`.
// ---------------------------------------------------------------------------------------------
let expected: SkillMirror.ExpectedSkillFiles list =
    canonicalSkills
    |> List.map (fun skill ->
        let id = skill.Id

        { Id = id
          // `scope` here is only carried through to the drift report; the process set is what the
          // producer manifest declares, everything else in the union is a co-tenant product/process
          // skill this repo vendors.
          Scope =
            (if Map.containsKey id declaredFiles then
                 Schemas.SkillScope.Process
             else
                 Schemas.SkillScope.Product)
          // FS.GG.SDD#727: the producer's declared FILE SET, not one digest for the whole skill.
          // An empty list means this producer declares nothing about this skill — hash-match is
          // skipped and presence + cross-root identity carry it, exactly as `Sha256 = ""` did.
          // That is the honest state for a CO-TENANT skill whose manifest lives in another
          // producer's repo; inventing a digest for it would be a fabricated authority.
          Files = (Map.tryFind id declaredFiles |> Option.defaultValue []) })

// FS.GG.SDD#721: the verdict is taken over the skill's WHOLE FILE SET, not just `SKILL.md`.
// Before this, the driver materialized `references/**` and `agents/*.yaml` through `mirrorFiles`
// and then verified only the one file the old `verify` could see — a verdict weaker than the
// invariant it claimed, the same defect class as FS-GG/.github#1506. `verifyFiles` closed the
// cross-root half; FS.GG.SDD#727 closes the AUTHORITY half, so a declared file is now hash-matched
// wherever it lives rather than only when it is called `SKILL.md`. The observation is RE-READ from
// disk after the writes, so this measures the tree that now exists rather than restating the plan
// that produced it.
let actual = observe ()

let drift = SkillMirror.verifyFileSet writeRoots expected actual

// COVERAGE, REPORTED RATHER THAN ASSUMED (FS.GG.SDD#727). The whole defect this item closed is a
// verdict reading stronger than its evidence, so the driver states how much of the tree the
// digests actually reach. Without these two lines a reader would take "verify: clean" over 51
// files as "51 files hash-matched", which is exactly the misreading that made
// `HashMismatchRoots = []` on an auxiliary look like a passed check.
let declaredFor (id: string) =
    Map.tryFind id declaredFiles |> Option.defaultValue []

let coveredFiles =
    canonicalSkills
    |> List.sumBy (fun skill ->
        let declared =
            declaredFor skill.Id |> List.map (fun f -> f.RelativePath) |> Set.ofList

        skill.Files
        |> List.filter (fun f -> Set.contains f.RelativePath declared)
        |> List.length)

let totalFiles = canonicalSkills |> List.sumBy (fun s -> List.length s.Files)

let undeclaredSkills =
    canonicalSkills
    |> List.filter (fun s -> List.isEmpty (declaredFor s.Id))
    |> List.map (fun s -> s.Id)

printfn "materialize-skill-roots (%s)" (if checkOnly then "--check" else "write")
printfn "  roots        : %s" (String.concat " " roots)

// THE RETIRED ROOTS ARE REPORTED, NOT SILENTLY SKIPPED (FS.GG.SDD#767 criterion 4). Dropping a root
// from the write set makes it invisible to every line below, and an invisible root with 28 committed
// skills in it is exactly the state this item was filed about. So each retirement is named, with what
// is still on disk under it and who owns removing it — a NOTICE, never a failure: failing here would
// leave a receiver no legal move, because ADR-0065 §Retiring a root forbids hand-deleting a mirror.
for retired in Set.toList retiredRoots |> List.sort do
    let dir = abs (retired + "/skills")

    let remaining =
        if Directory.Exists dir then
            Directory.EnumerateDirectories dir
            |> Seq.filter (fun d -> File.Exists(Path.Combine(d, "SKILL.md")))
            |> Seq.length
        else
            0

    printfn
        "  retired      : %s — not written, not read, not swept here (FsggKitRetiredSkillRoots); %d skill(s) remain on disk, owned by ADR-0067 phase 4 (FS-GG/.github#1676). No gate audits this root."
        retired
        remaining

// THE VIEW ROOTS ARE REPORTED IN THE SAME SHAPE, AND FOR THE SAME REASON (FS.GG.SDD#770 criterion 3).
// A view root is in the runtime contract and IS observed above — what it is not is written. Saying
// only "roots: .claude .agents" would leave a reader to assume both are materialized, which is the
// belief that put the view root in the write set in the first place. So it is named, with the reason
// it is not written and whose verdict its visibility is.
for view in Set.toList viewRoots |> List.sort do
    let dir = abs (view + "/skills")

    let disposition =
        if not (Directory.Exists dir) then "not generated in this tree"
        else
            let info = DirectoryInfo dir
            if info.LinkTarget <> null then sprintf "generated, symlink -> %s" info.LinkTarget
            else "generated, copy-mode (a real directory)"

    printfn
        "  view         : %s — in the runtime contract and OBSERVED, but NOT written: its content is generated by `scripts/skill-view`, never transported (%s). Its visibility is `skill-view check`'s verdict, not this driver's."
        view
        disposition

printfn "  write set    : %s" (if List.isEmpty writeRoots then "EMPTY — every declared root is the source or a generated view" else String.concat " " writeRoots)
printfn "  union        : %d skills" (List.length union)
printfn "  files        : %d across the union" totalFiles
printfn "  manifest     : ADR-0017 schema v%d, %d skill(s) declared" manifestSchemaVersion (Map.count declaredFiles)

printfn
    "  digests      : %d of %d union file(s) carry a declared digest; %d skill(s) declare none (co-tenant producers: %s)"
    coveredFiles
    totalFiles
    (List.length undeclaredSkills)
    (if List.isEmpty undeclaredSkills then
         "—"
     else
         String.concat " " undeclaredSkills)

printfn "  writes        : %d planned by SkillMirror.mirrorFiles" (List.length writes)
printfn "  changed      : %d" (List.length changed)

for p in changed do
    printfn "      %s %s" (if checkOnly then "DRIFT" else "wrote") p

if not (List.isEmpty drift) then
    eprintfn "  verify       : %d skill(s) still drifted" (List.length drift)

    for d in drift do
        eprintfn "      %s missingRoots=%A" d.Id d.MissingRoots

        // All FOUR facts are rendered, each named. `undeclared` is not a weaker `hashMismatch`:
        // it says the producer manifest does not cover this file at all, and its repair is
        // `fsgg-sdd registry skill-manifest --write`, not restoring bytes.
        for f in d.Files do
            eprintfn
                "        %s missing=%A divergent=%b hashMismatch=%A undeclared=%A"
                f.RelativePath
                f.MissingRoots
                f.Divergent
                f.HashMismatchRoots
                f.UndeclaredRoots

    exit 1

// The verdict states its own REACH. "clean" over 51 files where 16 carry a digest is a true
// statement about three different subjects, and collapsing them into one sentence is how a
// verdict comes to read stronger than its evidence (FS-GG/.github#1506, FS.GG.SDD#727).
printfn
    "  verify       : clean — every union skill present in every root, every FILE byte-identical across roots, and %d of %d file(s) matched against a declared digest"
    coveredFiles
    totalFiles

if coveredFiles < totalFiles then
    printfn
        "                 the remaining %d file(s) are held by presence + cross-root identity ALONE — a consistency guarantee, not an authenticity one. Their producer's manifest is not this repo's."
        (totalFiles - coveredFiles)

if checkOnly && not (List.isEmpty changed) then
    eprintfn "  --check: %d path(s) would change; run without --check to materialize." (List.length changed)
    exit 1
