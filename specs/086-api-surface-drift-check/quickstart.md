# Quickstart: `fsgg-sdd surface`

`fsgg-sdd surface` enforces the API-surface baseline convention in a **scaffolded workspace**:
every authored `src/<Pkg>/<Name>.fsi` signature has a byte-identical committed baseline at
`docs/api-surface/<Pkg>/<Name>.fsi`.

## Establish and check baselines

```sh
# Seed (or refresh) the committed baselines from the authored .fsi signatures:
fsgg-sdd surface --update      # writes docs/api-surface/**/*.fsi, exit 0

# Verify no drift (read-only). Exit 1 on any missing/differing baseline, 0 when coherent:
fsgg-sdd surface --check       # or just `fsgg-sdd surface` — check is the default
```

Workflow: after an intentional public-API change, re-run `fsgg-sdd surface --update`, review the
`docs/api-surface/**` diff (the surface change is now a reviewable part of the PR), and commit it.
A stray baseline with no source is reported as an orphan **warning** (it never fails the check and
is not auto-removed — delete it by hand if the source was intentionally dropped).

## Wire it into the workspace CI gate

`surface --check` is the honest replacement for the hand-written per-package `.fsi` drift test.
Add a step to the **workspace's** own `gate.yml` (this is the consumer repo's CI, not the FS.GG.SDD
component repo — the component uses a separate internal reflection baseline):

```yaml
      - name: API-surface drift check
        run: |
          if ! fsgg-sdd surface --check; then
            echo "::error::API-surface drift — run 'fsgg-sdd surface --update' and commit docs/api-surface/**."
            exit 1
          fi
```

## Non-default roots

The roots default to `src/` and `docs/api-surface/`. Override each when a workspace differs; the
`<Pkg>/<Name>.fsi` mirroring rule is unchanged:

```sh
fsgg-sdd surface --check --param sourceRoot=lib --param baselineRoot=docs/surface
```

## Follow-up (cross-repo)

Shipping this as a reusable **Templates MSBuild target**, so every scaffolded repo inherits the
`--check` gate without hand-editing its `gate.yml`, is a Templates/Rendering-repo concern tracked
under the FS-GG coordination epic (FS-GG/.github#235). This feature delivers the generic
`fsgg-sdd surface` command the target would invoke.
