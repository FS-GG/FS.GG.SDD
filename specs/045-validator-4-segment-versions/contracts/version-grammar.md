# Contract: Registry Validator Version Grammar (4-segment widening)

**Status**: authoritative for this feature's behavior change. The machine contract is the
`Fsgg.Registry` validator verdict over `version` / `package-version`; this document records the
**grammar** that verdict enforces and its **parity invariant** with the Python authority. The regex in
`src/FS.GG.Contracts/Registry.fs` is the implementation of this contract, not a second source of truth.

## Scope

Governs the `version` and optional `package-version` fields of a `contracts[]` entry, as validated by
`validateDocument` via `isValidVersion`. Does **not** govern the `range` field (separate, unchanged
`rangeRegex`) or the legacy `validate`/`tryParseSemVer` path over `RegistryModel` (research Decision 2).

## Accepted grammar (after this feature)

A `version`/`package-version` value is **valid** iff it matches **either**:

```text
bare integer       :  ^\d+$
SemVer (1–4 numeric segments, optional pre/build)
                   :  ^\d+\.\d+\.\d+(\.\d+)?(-[0-9A-Za-z.-]+)?(\+[0-9A-Za-z.-]+)?$
```

The single change versus feature 042 is the optional `(\.\d+)?` group, placed **before** the prerelease
(`-…`) and build (`+…`) groups so a 4th segment composes with them (`1.2.1.1-preview.1` is valid).

### Conformance vectors

| Input | Verdict | Class |
|---|---|---|
| `1` | valid | bare integer |
| `2` | valid | bare integer |
| `1.0.0` | valid | 3-segment SemVer |
| `0.1.52-preview.1` | valid | 3-segment + prerelease |
| `1.2.1.1` | **valid** | **4-segment numeric (new)** |
| `1.2.1.1-preview.1` | **valid** | **4-segment + prerelease (new)** |
| `1.2.x.4` | `MalformedVersion` | non-numeric 4th-ish segment |
| `abc` | `MalformedVersion` | not a version |
| `1.2.3.4.5` | `MalformedVersion` | more than four numeric segments |
| `` (blank) | `MissingField` | blank required field |

Maps to spec FR-001/002/003/004, SC-002/SC-003.

## Parity invariant (the "cannot disagree" property)

The accepted language MUST equal that of `FS-GG/.github` `scripts/validate-registry.py`:

```python
SEMVER_RE     = re.compile(r"^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$")
INT_VERSION_RE = re.compile(r"^\d+$")
```

The F# capturing groups `(\.\d+)?`, `(-…)?`, `(\+…)?` and the Python non-capturing `(?:…)?` accept the
**same** strings for `IsMatch` / `re.match` (capturing vs. non-capturing affects only group extraction).
Both validators MUST therefore return the **same** verdict on the canonical `registry/dependencies.yml`
(both "valid"). This is the FR-006 / SC-005 invariant that lets FS-GG/.github#49 retire the Python
stand-in. Any future divergence in either grammar MUST be surfaced and reconciled via the
`cross-repo-coordination` protocol, never silently left (spec edge case "Parity drift").

## Compatibility / stability

- **Non-breaking.** The accepted set strictly grows; no previously-accepted value is rejected and no
  public type, `.fsi` signature, `--json` output shape, command, flag, or exit-code changes. The widened
  `semVerRegex` is a `private` binding — ApiCompat (`CP####`) cannot trip.
- **Version impact.** Patch bump of `FS.GG.Contracts` (`1.1.0 → 1.1.1`) because source behavior changes
  (coherence invariant), and patch bump of the SDD product line (`0.2.0 → 0.2.1`) so the republished
  `fsgg-sdd` tool carries the fix. No migration note is owed (additive-only / non-breaking — versioning
  policy).
