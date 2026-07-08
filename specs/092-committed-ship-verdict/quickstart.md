# Quickstart — validating the committed ship verdict

Feature: `092-committed-ship-verdict`. Each block is runnable and states what it proves.

> **Local build note (research E1)**: this sandbox cannot restore `FSharp.Core` against the committed
> `packages.lock.json`. Prefix `dotnet` invocations with
> `-p:RestorePackagesWithLockFile=false -p:NuGetLockFilePath=$SCRATCH/nolock.json`.
> **Never** commit a regenerated lock file.

## 0. The failure this feature removes (before)

```sh
# In any repo scaffolded by `fsgg-sdd init` before this feature:
git log -p -- 'readiness/*/ship.json'      # -> nothing. The merge-boundary verdict is not in history.
```

## 1. The verdict exists, is compact, and carries the facts

```sh
fsgg-sdd ship --work 003-demo
cat readiness/003-demo/ship-verdict.json
wc -l < readiness/003-demo/ship-verdict.json      # -> 20 (ship-ready item)
```

Proves SC-001. Compare `wc -l < readiness/003-demo/ship.json` → 279.

## 2. `sourcesDigest` binds the verdict to its inputs

Recompute the aggregate independently from `ship.json` and compare:

```sh
python3 - <<'PY'
import json, hashlib
d = json.load(open('readiness/003-demo/ship.json'))
pre = "\n".join(f'{s["path"]}|{s["digest"]["algorithm"]}:{s["digest"]["value"]}'
                for s in sorted(d["sources"], key=lambda s: s["path"]))
print(hashlib.sha256(pre.encode()).hexdigest())
PY
python3 -c "import json;print(json.load(open('readiness/003-demo/ship-verdict.json'))['sourcesDigest']['value'])"
```

The two hashes match. Mutate any one source's path or digest and the aggregate changes. Proves SC-003.

## 3. The `.gitignore` negation actually fires — the load-bearing check

This is the check no string assertion can make. It must run **git**.

```sh
scratch=$(mktemp -d) && cd "$scratch" && git init -q .
fsgg-sdd init
mkdir -p readiness/003-demo/agent-commands/claude
touch readiness/003-demo/{ship.json,ship-verdict.json,verify.json,work-model.json,summary.md} \
      readiness/003-demo/agent-commands/claude/guidance.json

git add -A
git diff --cached --name-only | grep '^readiness/'
# -> readiness/003-demo/ship-verdict.json      (and nothing else)
```

Proves SC-004. Now prove the negation is load-bearing — restore the ADR-0018-era rule and watch the
verdict vanish from the index:

```sh
git rm -r --cached . -q
printf 'readiness/*/\n!readiness/*/ship-verdict.json\n' > .gitignore
git add -A
git diff --cached --name-only | grep '^readiness/' || echo "NOTHING STAGED — the negation is inert"
```

`readiness/*/` excludes the **directory**; git never descends into it, so the negation cannot fire.

## 4. Nested views stay ignored

```sh
git check-ignore -v readiness/003-demo/agent-commands/claude/guidance.json   # -> ignored
git check-ignore -v readiness/003-demo/ship.json                             # -> ignored
git check-ignore    readiness/003-demo/ship-verdict.json || echo "not ignored (correct)"
```

Proves SC-004's "and nothing else beneath `readiness/`".

## 5. This repository's own dogfood rule

SDD's readiness views land under `specs/<feature>/readiness/<work-id>/`, so the same trap applies to
this repo's own `.gitignore`. The root `readiness/<id>/` hand-pinned proofs must stay committed.

```sh
cd <repo-root>
git check-ignore specs/092-committed-ship-verdict/readiness/003/ship.json         # ignored
git check-ignore specs/092-committed-ship-verdict/readiness/003/ship-verdict.json || echo "tracked (correct)"
git check-ignore readiness/019-spectre-rendering/                                 || echo "tracked (correct)"
```

Proves SC-005.

## 6. `refresh` re-projects byte-identically

```sh
sha256sum readiness/003-demo/ship-verdict.json > /tmp/before
fsgg-sdd refresh --work 003-demo --text | grep -i 'ship-verdict'   # -> alreadyCurrent
sha256sum -c /tmp/before                                            # -> OK
```

Proves SC-006. Then make `ship.json` stale (touch an authored source) and confirm `refresh` reports
the verdict under ship's inherited class and does **not** rewrite it.

## 7. A blocked ship writes neither file

```sh
fsgg-sdd ship --work 004-blocked ; echo "exit=$?"
ls readiness/004-blocked/ship.json readiness/004-blocked/ship-verdict.json 2>&1   # -> both absent
```

Proves SC-008.

## 8. Determinism

```sh
fsgg-sdd ship --work 003-demo && sha256sum readiness/003-demo/ship-verdict.json > /tmp/a
fsgg-sdd ship --work 003-demo && sha256sum readiness/003-demo/ship-verdict.json > /tmp/b
diff /tmp/a /tmp/b && echo "byte-identical"
grep -nE '[0-9]{4}-[0-9]{2}-[0-9]{2}T|/home/|\x1b\[' readiness/003-demo/ship-verdict.json \
  || echo "no clock, no absolute path, no ANSI"
```

Proves SC-007.

## 9. The taxonomy doc stays catalog-derived

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter ArtifactTaxonomyTests
```

Flip `durableGenerated` to `false` on the verdict's catalog entry and the guard fails, naming
`ship-verdict.json` as missing from the regenerable block. Proves SC-009.

## 10. The release-boundary guards remain mutually reinforcing

```sh
dotnet test tests/FS.GG.SDD.Commands.Tests --filter ReleaseBoundaryTests
dotnet test tests/FS.GG.SDD.Artifacts.Tests --filter ReleaseReadinessCheckTests
```

Add `GeneratedViewKind.ShipVerdict` without a catalog entry → T019 fails. Add the catalog entry
without amending T024's `known` set → T024 fails. Proves SC-010.

## 11. `validate` enumerates the new view

```sh
fsgg-sdd validate --text | grep -i 'ship-verdict'
```

An unenumerated generated view surfaces as a `CoverageGap` from `reconcileSurface`, which walks the
real `readiness/` tree. Proves SC-011.
