namespace FS.GG.SDD.Commands

open Fsgg.Schemas
open FS.GG.SDD.Commands.Internal

module ProcessSkillManifest =

    let build () : SkillManifest =
        { SchemaVersion = skillManifestVersion
          Skills =
            SeededSkills.seededSkills ()
            |> List.map (fun skill ->
                { Id = skill.Name
                  Scope = Process
                  Sha256 = Fsgg.SkillMirror.sha256 skill.Body
                  Body = None
                  // The materialized location under every agent-skill root
                  // (`.agents/skills/<id>/SKILL.md`), matching the org manifest shape.
                  ResolvablePath = Some(Fsgg.SkillMirror.skillPath Fsgg.SkillMirror.providerSourceRoot skill.Name) }) }
