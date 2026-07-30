namespace FS.GG.SDD.Commands

open Fsgg.Schemas
open FS.GG.SDD.Commands.Internal

module ProcessSkillManifest =

    let build () : SkillManifestV2 =
        { SchemaVersion = skillManifestVersion
          Skills =
            SeededSkills.seededSkills ()
            |> List.map (fun skill ->
                let digest = Fsgg.SkillMirror.sha256 skill.Body

                let entry: SkillManifestEntry =
                    { Id = skill.Name
                      Scope = Process
                      Sha256 = digest
                      Body = None
                      // The materialized location in a scaffolded PRODUCT tree under every
                      // agent-skill root (`.agents/skills/<id>/SKILL.md`), matching the org
                      // manifest shape. In this producer checkout `.agents/skills` is a
                      // generated view: it resolves only after `scripts/skill-view generate`,
                      // and is absent from a bare checkout (ADR-0067 §6).
                      ResolvablePath = Some(Fsgg.SkillMirror.skillPath Fsgg.SkillMirror.providerSourceRoot skill.Name) }

                // ADR-0017 v2 (FS.GG.SDD#727): the COMPLETE file set. A seeded skill IS its
                // `SKILL.md` — `SeededSkills` carries one embedded body per id and nothing else —
                // so the declared set is that one file, and it is DERIVED from the same body the
                // digest above is taken from rather than restated. There is no second source to
                // drift against, and none to forget to update.
                { Skill = entry
                  Files =
                    [ { RelativePath = "SKILL.md"
                        Sha256 = digest } ] }) }
