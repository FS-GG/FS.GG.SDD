# Contract: `init` emits `.fsgg/constitution.md`

**Scope**: behavior contract over the existing `init` command surface — no new external interface.

## Guarantees

1. **Emission (FR-001)**. After `fsgg-sdd init` in a directory, `.fsgg/constitution.md` exists,
   produced by the `initEffects` skeleton planner as a `WriteFile` effect. It is laid alongside
   `.fsgg/project.yml`, `.fsgg/sdd.yml`, `.fsgg/agents.yml`, `CLAUDE.md`, and `AGENTS.md`.

2. **Populated & placeholder-free (FR-002/SC-001)**. The file is non-empty, has a recognizable
   constitution title and principles, and contains no `[BRACKET]`/`TODO`/`FIXME` placeholder
   tokens. Content is exactly [constitution-content.md](./constitution-content.md).

3. **Generic (FR-003/SC-006)**. The content contains no FS.GG.SDD-repo-, provider-, template-,
   or rendering-specific name, path, or URL.

4. **Deterministic (FR-007/SC-003)**. Two `init` runs on identical inputs produce byte-identical
   `.fsgg/constitution.md`. The content is a constant literal with no date/timestamp/randomness/
   environment-derived value.

5. **Report attribution (FR-010)**. The command report's changed-artifacts list includes an entry
   for `.fsgg/constitution.md` with `Kind = "agentGuidance"`, `Ownership = "authored"`, and
   operation `Create` on a fresh directory — attributing it to the SDD skeleton surface, the same
   surface that reports `project.yml`/`sdd.yml`/`agents.yml`/`CLAUDE.md`/`AGENTS.md`.

## Verification (real-filesystem, public surface)

- **US1-AC1**: `init` into a temp empty dir ⇒ `.fsgg/constitution.md` exists, non-empty; the
  report lists it as a created authored skeleton artifact.
- **US1-AC2**: run `init` twice on identical inputs ⇒ the two files are byte-identical.
- **US1-AC3**: scan the emitted content ⇒ none of the forbidden token set
  (`FS.GG.SDD`, `FS.GG.Rendering`, `FS.GG.Governance`, provider/template ids, docs URLs) appears,
  and no `[`…`]` placeholder token remains.
- **Plan-level (clarity)**: `CommandWorkflowTests.fs` `Assert.Contains` that `initEffects` plans a
  `WriteFile(".fsgg/constitution.md", _, AgentGuidanceTarget)`.

## Non-guarantees

- No JSON byte change to `scaffold-provenance.json` or any existing report field shape (the
  changed-artifacts entry uses the existing record shape).
- No exit-code, stream-routing, or `--rich`/`--text`/`--json` precedence change.
