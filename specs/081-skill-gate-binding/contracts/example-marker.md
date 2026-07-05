# Contract: skill example marker grammar

**Feature**: 081-skill-gate-binding · **Status**: proposed (Tier 1 agent-skill contract)

The marker binds a runnable fenced example inside an `fs-gg-sdd-*` stage `SKILL.md` to a file in the gate-passing corpus, so the skill↔gate doctest knows *which* fence to check and *against what*. It adds no authoring content — it is a comment.

## Grammar

A marker is an HTML comment on its own line, immediately preceding a fenced code block (no blank line between marker and fence):

```
<!-- fsgg-sdd:example corpus=<file> [mode=contains|equals] -->
```code-fence
… example body …
```
```

Counter-examples (blocks that demonstrate a rejected form, shown deliberately):

```
<!-- fsgg-sdd:example counter -->
```code-fence
… deliberately-wrong body …
```
```

### Attributes

| Attribute | Required | Values | Meaning |
|---|---|---|---|
| `corpus` | yes (unless `counter`) | a filename under `docs/examples/lifecycle-artifacts/` | the gate-run corpus file this block corresponds to |
| `mode` | no (default `contains`) | `contains` \| `equals` \| `ref` | `contains`: the normalized block is a substring of the normalized corpus file (for a verbatim fragment of a load-bearing grammar). `equals`: the normalized block equals the whole corpus file. `ref`: pointer only — the block is an illustrative example (own theme/ids) and is **not** text-matched; the doctest only asserts the named `corpus` file exists and was gate-exercised. |
| `counter` | — | flag (no value) | the block is a negative example; the doctest asserts it would **not** pass its gate, and it is exempt from `corpus` matching |

**Mode choice.** Load-bearing grammar the gate silently rejects when mistyped (the specify coverage line, the evidence deferral fields) uses `contains`/`equals` so the exact bytes are bound. A stage skill whose example is illustrative uses `ref` — still guaranteeing the corpus file it points at is gate-clean on every build (so "copy this file" never sends the author to a rejected example), without forcing the skill prose to mirror the corpus verbatim. Every stage skill that documents a gated authoring artifact carries at least one marker of some mode (FR-004).

## Normalization (for `contains`/`equals` comparison)

Before comparison, both the block and the corpus file are normalized:
- trailing whitespace stripped per line; CRLF→LF;
- leading/trailing blank lines trimmed;
- no other transformation (indentation and content are significant — the author must be able to copy the block verbatim).

## Doctest obligations (what consumes this contract)

1. **Extraction**: for each stage `SKILL.md`, collect every fenced block preceded by a marker. Unmarked fences are ignored (command lines, prose).
2. **Consistency**: for each non-`counter` marked block, assert `mode` holds against `docs/examples/lifecycle-artifacts/<corpus>`. Failure → build fails naming `(skill, corpus, mode)`.
3. **Gate run**: run the whole corpus through the real gate commands (via `TestSupport`); assert zero blocking diagnostics. Failure → build fails naming the gate + diagnostic id.
4. **Counter-examples**: assert each `counter` block, when run through its stage gate, *does* block (proves it is genuinely the rejected form the skill warns against).
5. **Coverage**: every stage skill that documents a gated artifact MUST contribute at least one non-`counter` marked block, else the build fails (FR-004).

## Non-goals

- The marker does **not** encode line ranges, ids, or any content that could drift from the block it precedes — it names only the corpus file and match mode.
- The marker is **not** a second source of truth for the example; the corpus file is authoritative and is what the gate runs.

## Compatibility

- Markers are HTML comments — invisible in rendered Markdown, inert to every existing skill consumer, and byte-identical across the `.claude`/`.codex`/`.agents` mirror.
- Adding markers changes each edited `SKILL.md` body sha256 → `skill-manifest` regenerated; `SkillMirrorTests`/`SeededSkillsTests`/`ProcessSkillManifestTests` kept green.
