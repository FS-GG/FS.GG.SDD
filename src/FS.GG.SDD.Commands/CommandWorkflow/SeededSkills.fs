namespace FS.GG.SDD.Commands.Internal

open System.IO
open System.Reflection
open FS.GG.SDD.Commands.CommandTypes

/// The consumer-relevant `fs-gg-sdd-*` process skill set the SDD skeleton seeds into
/// every product, on both the Claude and Codex agent surfaces (feature 051).
///
/// The canonical bodies are the repo's authored `.claude/skills/<name>/SKILL.md`
/// files, linked into this assembly as embedded resources (LogicalName
/// `SeededSkill.<name>`), so seeding reads compiled-in bytes and never the FS.GG.SDD
/// repo at runtime. `skillNames` below is the single in-code source of the set
/// (contract §1 / D2); a drift guard pins it to the on-disk authored set.
///
/// Seeded skills reuse the existing `AgentGuidanceTarget` write-kind, so they inherit
/// the same no-clobber, authored-SDD-owned semantics as the seeded constitution and
/// early-stage guidance — no new effect, no new schema (Principle IV/V).
module internal SeededSkills =

    // The 15 in-scope skills (10 stage + 5 cross-cutting), sorted, excluding the
    // product-internal `fs-gg-sdd-project`. This list is the single in-code source of
    // the set; iterating it sorted keeps the emitted effect order deterministic (FR-006).
    let skillNames =
        [ "fs-gg-sdd-analyze"
          "fs-gg-sdd-authoring-contracts"
          "fs-gg-sdd-charter"
          "fs-gg-sdd-checklist"
          "fs-gg-sdd-clarify"
          "fs-gg-sdd-evidence"
          "fs-gg-sdd-getting-started"
          "fs-gg-sdd-lifecycle"
          "fs-gg-sdd-plan"
          "fs-gg-sdd-refresh-agents"
          "fs-gg-sdd-ship"
          "fs-gg-sdd-specify"
          "fs-gg-sdd-tasks"
          "fs-gg-sdd-validate"
          "fs-gg-sdd-verify" ]
        |> List.sort

    let logicalName (name: string) = "SeededSkill." + name

    /// Raised when a declared seeded skill's embedded resource is absent from the assembly. This
    /// is a build/packaging defect — the `<EmbeddedResource>` set drifted from `skillNames` — not
    /// user input (Constitution VIII: distinguish tool defect from user error, fail legibly). It
    /// replaces the former static-init `failwithf`, which surfaced as an opaque
    /// `TypeInitializationException` (feature 068 / US3a / FR-009).
    type SeededSkillResourceMissing(logicalName: string) =
        inherit exn(
            $"Embedded seeded-skill resource '{logicalName}' is missing from the FS.GG.SDD.Commands "
            + "assembly — a build/packaging defect (the embedded-resource set drifted from "
            + "SeededSkills.skillNames), not user input. Rebuild after restoring the resource."
        )

        member _.LogicalName = logicalName

    let loadBody (name: string) =
        let assembly = Assembly.GetExecutingAssembly()

        use stream =
            match assembly.GetManifestResourceStream(logicalName name) with
            | null -> raise (SeededSkillResourceMissing(logicalName name))
            | s -> s

        use reader = new StreamReader(stream)
        reader.ReadToEnd()

    type SeededSkill = { Name: string; Body: string }

    // Non-eager (a function, not a module-level value) so a missing embedded resource surfaces as
    // the legible SeededSkillResourceMissing at the point of use — inside command execution, where
    // it is catchable — rather than as an opaque TypeInitializationException at first module touch
    // (feature 068 / US3a / FR-009).
    let seededSkills () =
        skillNames |> List.map (fun name -> { Name = name; Body = loadBody name })

    // Each seeded skill expands to one additive WriteFile effect per declared agent-skill
    // root (058/ADR-0014 §Decision 5: the root set is the single `agentSkillRoots` constant,
    // not a hardcoded list). All carry the no-clobber AgentGuidanceTarget write-kind; one
    // canonical body to every root makes them byte-identical by construction (FR-002). The
    // shared `SkillMirror.mirror` sorts by id and iterates the roots in order, so the emitted
    // effect stream is deterministic and unchanged. `init` and `scaffold` share this seam.
    let skillEffects () =
        seededSkills ()
        |> List.map (fun skill -> skill.Name, skill.Body)
        |> Fsgg.SkillMirror.mirror Fsgg.Schemas.agentSkillRoots
        |> List.map (fun write -> WriteFile(write.Path, write.Body, AgentGuidanceTarget))
